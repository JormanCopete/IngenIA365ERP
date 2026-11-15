package pagos

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T116 (FR-052, FR-053; contracts/canal-api.md §C, «reverse»): el ERP pide reversar un pago aprobado. Por la pasarela sólo
// antes del corte diario del destino (hora de Colombia, el mismo día de la aprobación): el canal vuelve a mirar el corte con
// su propio reloj y, pasado, responde 409 Canal.Reverso.FueraDeCorte para que el ERP lo lleve a una devolución por Tesorería.
// Con mode = treasury el canal sólo marca el intento como devuelto, para que el asociado lo vea en su historial. Repetirlo
// sobre un intento ya reversado responde lo mismo, sin volver a llamar a la pasarela. El canal no llama de vuelta al ERP:
// el ERP termina el reverso con la identidad del funcionario que lo pidió.

// Modos del reverso (payment_attempts.reversal_kind).
const (
	ReversoPorLaPasarela = "gateway"
	ReversoPorTesoreria  = "treasury"
	FuenteReverso        = "reversal"
	CortePorDefecto      = "20:00"
	AccionPagoReversado  = "Canal.Pago.Reversado"
)

// Errores del reverso.
var (
	ErrFueraDeCorte = sobre.Nuevo(http.StatusConflict, "Canal.Reverso.FueraDeCorte",
		"Ya pasó el corte diario de la pasarela para este pago: el reverso va como devolución por Tesorería.")
	ErrReversoNoAprobado = sobre.Nuevo(http.StatusConflict, "Canal.Reverso.NoAprobado", "Sólo se reversa un pago aprobado.")
	ErrReversoSinDatos   = sobre.Nuevo(http.StatusConflict, "Canal.Reverso.SinReferenciaInterna",
		"La pasarela no informó la referencia interna de este pago: el reverso va como devolución por Tesorería.")
	ErrReversoInvalido = sobre.Nuevo(http.StatusBadRequest, "Canal.Reverso.Invalido",
		"El reverso necesita quién lo pide, el motivo, el corte (HH:mm) y el modo (gateway o treasury).")
)

// PedidoDeReverso es el cuerpo de POST /internal/v1/tenants/{t}/attempts/{reference}/reverse.
type PedidoDeReverso struct {
	SolicitadoPor uuid.UUID `json:"requestedBy"`
	Motivo        string    `json:"reason"`
	Corte         string    `json:"cutoffLocalTime"`
	Modo          string    `json:"mode"`
}

// ResultadoDelReverso es la respuesta (ReversoEnPasarelaDto del ERP).
type ResultadoDelReverso struct {
	Reversado   bool       `json:"reversed"`
	Motivo      *string    `json:"reason"`
	Modo        *string    `json:"mode"`
	ReversadoEn *time.Time `json:"reversedAt"`
}

// Validar revisa el pedido y devuelve el corte como hora y minutos.
func (p PedidoDeReverso) Validar() (time.Duration, *sobre.Error) {
	if p.SolicitadoPor == uuid.Nil || strings.TrimSpace(p.Motivo) == "" || len(p.Motivo) > 300 ||
		(p.Modo != ReversoPorLaPasarela && p.Modo != ReversoPorTesoreria) {
		return 0, ErrReversoInvalido
	}
	corte := p.Corte
	if corte == "" {
		corte = CortePorDefecto
	}
	t, err := time.Parse("15:04", corte)
	if err != nil {
		return 0, ErrReversoInvalido
	}
	return time.Duration(t.Hour())*time.Hour + time.Duration(t.Minute())*time.Minute, nil
}

// DentroDelCorte dice si un pago aprobado en aprobado todavía se reversa por la pasarela en ahora: el mismo día (hora de
// Colombia) y antes de la hora del corte.
func DentroDelCorte(aprobado, ahora time.Time, corte time.Duration) bool {
	a, h := aprobado.In(horaDeColombia), ahora.In(horaDeColombia)
	if a.Year() != h.Year() || a.YearDay() != h.YearDay() {
		return false
	}
	medianoche := time.Date(h.Year(), h.Month(), h.Day(), 0, 0, 0, 0, horaDeColombia)
	return h.Sub(medianoche) < corte
}

// Reversar atiende el pedido del ERP sobre un intento de la cooperativa (Id del canal).
func (s *Servicio) Reversar(ctx context.Context, cooperativa uuid.UUID, referencia string, p PedidoDeReverso) (ResultadoDelReverso, error) {
	corte, e := p.Validar()
	if e != nil {
		return ResultadoDelReverso{}, e
	}
	a, err := s.q.GetPaymentAttemptByReference(ctx, referencia)
	if errors.Is(err, pgx.ErrNoRows) || (err == nil && a.TenantID != cooperativa) {
		return ResultadoDelReverso{}, ErrNoExiste
	}
	if err != nil {
		return ResultadoDelReverso{}, err
	}
	if a.ReversedAt != nil {
		return yaReversado(a), nil
	}
	if a.Status != "approved" {
		return ResultadoDelReverso{}, ErrReversoNoAprobado
	}
	if p.Modo == ReversoPorTesoreria {
		return s.marcarReverso(ctx, a.ID, ReversoPorTesoreria, p, json.RawMessage(`{"mode":"treasury"}`), "")
	}

	var ap pasarela.Aprobacion
	if len(a.Approval) == 0 || json.Unmarshal(a.Approval, &ap) != nil || ap.ReferenciaInterna == 0 {
		return ResultadoDelReverso{}, ErrReversoSinDatos
	}
	aprobado := ap.Fecha
	if aprobado.IsZero() {
		aprobado = a.UpdatedAt
	}
	if !DentroDelCorte(aprobado, s.ahora(), corte) {
		return ResultadoDelReverso{}, ErrFueraDeCorte
	}
	sitio, _, err := s.o.Sitios.Cargar(ctx, a.SiteID)
	if err != nil {
		return ResultadoDelReverso{}, fmt.Errorf("pagos: cargar el sitio del intento: %w", err)
	}
	r, err := s.o.Pasarela.Reversar(ctx, sitio, ap.ReferenciaInterna)
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "la pasarela no respondió el reverso", "intento", a.ID, "error", err)
		return ResultadoDelReverso{}, ErrPasarelaNoDisp
	}
	respuesta := r.Respuesta
	if len(respuesta) == 0 {
		respuesta = json.RawMessage("{}")
	}
	if !r.Exitoso {
		// La pasarela no lo reversó: queda el rastro y el intento sigue aprobado.
		if err := s.q.InsertAttemptEvent(ctx, gen.InsertAttemptEventParams{ID: nuevoID(), AttemptID: a.ID, Source: FuenteReverso,
			GatewayStatus: ptr(recortar(r.Estado, 40)), Status: a.Status, Raw: respuesta, At: s.ahora()}); err != nil {
			return ResultadoDelReverso{}, err
		}
		s.auditar(ctx, "Canal.Pago.ReversoRechazado", a.ID.String(), map[string]any{"reference": a.Reference, "gatewayStatus": r.Estado,
			"requestedBy": p.SolicitadoPor})
		motivo := r.Mensaje
		if motivo == "" {
			motivo = "La pasarela respondió " + r.Estado + "."
		}
		return ResultadoDelReverso{Reversado: false, Motivo: &motivo}, nil
	}
	return s.marcarReverso(ctx, a.ID, ReversoPorLaPasarela, p, respuesta, r.Estado)
}

// marcarReverso anota el reverso con cerrojo, una sola vez, con su evento y su auditoría.
func (s *Servicio) marcarReverso(ctx context.Context, id uuid.UUID, modo string, p PedidoDeReverso, raw json.RawMessage,
	estadoPasarela string) (ResultadoDelReverso, error) {
	var resultado ResultadoDelReverso
	err := s.enTransaccion(ctx, func(q *gen.Queries, tx pgx.Tx) error {
		a, err := q.GetPaymentAttemptForUpdate(ctx, id)
		if err != nil {
			return err
		}
		if a.ReversedAt != nil {
			resultado = yaReversado(a)
			return nil
		}
		ahora := s.ahora()
		if _, err := q.MarkAttemptReversed(ctx, gen.MarkAttemptReversedParams{ID: id, ReversedAt: ahora, ReversalKind: &modo}); err != nil {
			return err
		}
		var crudo *string
		if estadoPasarela != "" {
			crudo = ptr(recortar(estadoPasarela, 40))
		}
		if err := q.InsertAttemptEvent(ctx, gen.InsertAttemptEventParams{ID: nuevoID(), AttemptID: id, Source: FuenteReverso,
			GatewayStatus: crudo, Status: a.Status, Raw: raw, At: ahora}); err != nil {
			return err
		}
		if err := s.o.Auditoria.EnTransaccion(tx).Registrar(ctx, AccionPagoReversado, id.String(), map[string]any{
			"reference": a.Reference, "mode": modo, "requestedBy": p.SolicitadoPor, "reason": strings.TrimSpace(p.Motivo),
		}); err != nil {
			return err
		}
		resultado = ResultadoDelReverso{Reversado: true, Modo: &modo, ReversadoEn: &ahora}
		return nil
	})
	return resultado, err
}

func yaReversado(a gen.PaymentAttempt) ResultadoDelReverso {
	return ResultadoDelReverso{Reversado: true, Modo: a.ReversalKind, ReversadoEn: a.ReversedAt}
}
