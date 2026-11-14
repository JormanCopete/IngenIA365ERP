package pagos

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T106, T107 (FR-044, FR-047, FR-048): al quedar aprobado un intento, en la misma transacción del cambio de estado se encola
// send_payment_notice. El trabajo avisa al ERP y guarda el resultado (aplicado o en revisión) y el recibo. Si el ERP no responde,
// River reintenta con espera creciente; al llegar al umbral, alerta una vez. El aviso es idempotente por referencia en el ERP:
// reintentar nunca duplica.

// AvisadorDelERP es lo que el aviso y el comprobante piden al ERP (lo cumple *erp.Cliente).
type AvisadorDelERP interface {
	AvisarPago(ctx context.Context, cooperativa, persona uuid.UUID, a erp.AvisoDePago) (erp.ResultadoDelAviso, error)
	Comprobante(ctx context.Context, cooperativa, persona, recibo uuid.UUID) ([]byte, string, error)
}

// Resultados del aviso al ERP (payment_attempts.erp_outcome).
const (
	AvisoSinEnviar        = "not_sent"
	AvisoEnviando         = "sending"
	AvisoAplicado         = "applied"
	AvisoEnRevision       = "in_review"
	AvisoReintentando     = "failed_retrying"
	UmbralPorDefecto      = 5
	AccionAvisoSinAplicar = "Canal.Pago.AvisoSinAplicar"
)

// ArgsAvisarPago es el trabajo send_payment_notice.
type ArgsAvisarPago struct {
	Intento uuid.UUID `json:"attemptId"`
}

// Kind del trabajo.
func (ArgsAvisarPago) Kind() string { return "send_payment_notice" }

// InsertOpts: un aviso por intento a la vez.
func (ArgsAvisarPago) InsertOpts() river.InsertOpts {
	return river.InsertOpts{UniqueOpts: river.UniqueOpts{ByArgs: true}}
}

// encolarAviso es el AlAprobar por defecto: encola el aviso en la transacción de la aprobación y marca el intento «enviando».
func (s *Servicio) encolarAviso(ctx context.Context, tx pgx.Tx, intento gen.PaymentAttempt) error {
	if _, err := s.o.Encolador.InsertTx(ctx, tx, ArgsAvisarPago{Intento: intento.ID}, nil); err != nil {
		return fmt.Errorf("pagos: encolar el aviso al ERP: %w", err)
	}
	return s.q.WithTx(tx).SetAttemptErpOutcome(ctx, gen.SetAttemptErpOutcomeParams{ID: intento.ID, ErpOutcome: AvisoEnviando})
}

// AvisoDelIntento arma el cuerpo del aviso con lo guardado en el intento (contracts/erp-api-de-servicio.md).
func AvisoDelIntento(a gen.PaymentAttempt) (erp.AvisoDePago, error) {
	var items []Item
	if err := json.Unmarshal(a.Items, &items); err != nil {
		return erp.AvisoDePago{}, fmt.Errorf("pagos: leer los conceptos del intento: %w", err)
	}
	var ap pasarela.Aprobacion
	if len(a.Approval) == 0 || json.Unmarshal(a.Approval, &ap) != nil || a.GatewayRequestID == nil {
		return erp.AvisoDePago{}, errors.New("pagos: el intento aprobado no tiene los datos de la aprobación")
	}
	aviso := erp.AvisoDePago{
		Referencia: a.Reference, Destino: a.DestinationPublicID, Total: a.Total, Moneda: a.Currency, Aprobado: ap.Fecha,
		Pasarela: erp.AprobacionDelAviso{
			RequestID: *a.GatewayRequestID, Autorizacion: ap.Autorizacion, Recibo: ap.Recibo, Franquicia: ap.Franquicia, Medio: ap.Medio,
			Emisor: ap.Emisor,
		},
		Pagador: erp.PagadorDelAviso{IP: a.BuyerIp, Agente: a.BuyerUserAgent, Cuenta: a.AccountID},
	}
	if ap.ReferenciaInterna != 0 {
		aviso.Pasarela.ReferenciaInterna = &ap.ReferenciaInterna
	}
	if aviso.Aprobado.IsZero() {
		aviso.Aprobado = a.UpdatedAt
	}
	for _, i := range items {
		aviso.Items = append(aviso.Items, erp.ItemDelAviso{Clave: i.Clave, Modalidad: i.Modalidad, Valor: i.Valor, Eleccion: i.Eleccion})
	}
	return aviso, nil
}

// Avisar envía el aviso del intento al ERP y guarda el resultado. Devuelve error si hay que reintentar.
func (s *Servicio) Avisar(ctx context.Context, id uuid.UUID) error {
	a, err := s.q.GetPaymentAttempt(ctx, id)
	if err != nil {
		return fmt.Errorf("pagos: leer el intento %s: %w", id, err)
	}
	if a.Status != "approved" || a.ErpOutcome == AvisoAplicado || a.ErpOutcome == AvisoEnRevision {
		return nil
	}
	if a.ReversedAt != nil {
		// T116: reversado antes de que el aviso llegara al ERP; avisarlo ahora aplicaría un pago que la pasarela devolvió.
		s.auditar(ctx, "Canal.Pago.AvisoOmitidoPorReverso", id.String(), map[string]any{"reference": a.Reference})
		return nil
	}
	tenant, err := s.q.GetTenantByID(ctx, a.TenantID)
	if err != nil {
		return fmt.Errorf("pagos: leer la cooperativa del intento: %w", err)
	}
	aviso, err := AvisoDelIntento(a)
	if err != nil {
		return river.JobCancel(err)
	}
	ctx = contexto.ConCooperativa(contexto.ConIntento(contexto.ConCuenta(ctx, a.AccountID), a.ID),
		contexto.Cooperativa{ID: a.TenantID, PublicID: tenant.TenantPublicID})
	if a.BuyerIp != nil {
		ctx = contexto.ConIP(ctx, *a.BuyerIp)
	}
	if a.BuyerUserAgent != nil {
		ctx = contexto.ConAgente(ctx, *a.BuyerUserAgent)
	}
	r, err := s.o.Avisador.AvisarPago(ctx, tenant.TenantPublicID, a.PersonPublicID, aviso)
	if err != nil {
		_ = s.q.SetAttemptErpOutcome(ctx, gen.SetAttemptErpOutcomeParams{ID: id, ErpOutcome: AvisoReintentando})
		var e *erp.ErrorDelERP
		if errors.As(err, &e) && (e.Estado == http.StatusConflict || e.Estado == http.StatusBadRequest) {
			// El ERP no lo aplicará nunca así (total que no cuadra, forma inválida): reintentar no sirve; una persona lo mira.
			s.alertarAviso(ctx, a, e.Codigo, e.Mensaje)
			return river.JobCancel(err)
		}
		return err
	}
	resultado := AvisoAplicado
	if r.Resultado != "Applied" {
		resultado = AvisoEnRevision
	}
	if err := s.q.SetAttemptErpOutcome(ctx, gen.SetAttemptErpOutcomeParams{ID: id, ErpOutcome: resultado, ErpReceiptPublicID: r.Recibo}); err != nil {
		return err
	}
	s.auditar(ctx, "Canal.Pago.AvisoAplicado", id.String(), map[string]any{"reference": a.Reference, "outcome": r.Resultado, "receiptPublicId": r.Recibo})
	return nil
}

func (s *Servicio) alertarAviso(ctx context.Context, a gen.PaymentAttempt, codigo, mensaje string) {
	s.o.Logger.ErrorContext(ctx, "ALERTA: el ERP no ha aplicado un pago aprobado; la cooperativa debe revisarlo", "intento", a.ID,
		"referencia", a.Reference, "codigo", codigo, "mensaje", mensaje)
	s.auditar(ctx, AccionAvisoSinAplicar, a.ID.String(), map[string]any{"reference": a.Reference, "code": codigo})
}

// TrabajadorDeAviso corre send_payment_notice.
type TrabajadorDeAviso struct {
	river.WorkerDefaults[ArgsAvisarPago]
	Pagos *Servicio
}

// NextRetry: espera creciente, de un minuto a una hora (FR-047).
func (t *TrabajadorDeAviso) NextRetry(job *river.Job[ArgsAvisarPago]) time.Time {
	espera := time.Duration(1<<min(job.Attempt, 6)) * time.Minute
	return time.Now().Add(min(espera, time.Hour))
}

// Work avisa; al llegar al umbral de intentos alerta una vez y sigue reintentando.
func (t *TrabajadorDeAviso) Work(ctx context.Context, job *river.Job[ArgsAvisarPago]) error {
	ctx = contexto.ConActor(ctx, contexto.ActorSistema)
	err := t.Pagos.Avisar(ctx, job.Args.Intento)
	if err != nil && job.Attempt == t.Pagos.o.UmbralDeAlertaDelAviso {
		if a, e := t.Pagos.q.GetPaymentAttempt(ctx, job.Args.Intento); e == nil {
			t.Pagos.alertarAviso(ctx, a, "Canal.ERP.NoDisponible", err.Error())
		}
	}
	return err
}

// comprobante atiende GET /api/v1/payments/{id}/receipt: el PDF del ERP para un intento aplicado de la cuenta (FR-048).
func (s *Servicio) comprobante(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	coop, persona, cuenta, ok := s.alcance(w, r)
	if !ok {
		return
	}
	id, err := uuid.Parse(r.PathValue("id"))
	if err != nil {
		sobre.Escribir(w, ErrNoExiste)
		return
	}
	a, err := s.q.GetPaymentAttempt(ctx, id)
	if err != nil || a.AccountID != cuenta || a.TenantID != coop.ID {
		sobre.Escribir(w, ErrNoExiste)
		return
	}
	if a.ErpReceiptPublicID == nil {
		sobre.Escribir(w, ErrSinComprobante)
		return
	}
	pdf, tipo, err := s.o.Avisador.Comprobante(ctx, coop.PublicID, persona, *a.ErpReceiptPublicID)
	if err != nil {
		s.o.Logger.WarnContext(ctx, "el ERP no dio el comprobante", "intento", id, "error", err)
		sobre.Escribir(w, ErrEntidadNoDisp)
		return
	}
	if tipo == "" {
		tipo = "application/pdf"
	}
	w.Header().Set("Content-Type", tipo)
	w.Header().Set("Content-Disposition", fmt.Sprintf(`attachment; filename="comprobante-%s.pdf"`, a.Reference))
	w.Header().Set("Cache-Control", "no-store")
	_, _ = w.Write(pdf)
}

// ErrSinComprobante: el pago todavía no tiene recibo del ERP (en proceso o en revisión sin recibo).
var ErrSinComprobante = sobre.Nuevo(http.StatusConflict, "Canal.Pago.SinComprobante",
	"El comprobante todavía no está disponible: tu cooperativa está registrando el pago.")
