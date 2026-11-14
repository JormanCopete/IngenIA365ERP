package pagos

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/riverqueue/river"
	"github.com/riverqueue/river/rivertype"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// US4 (T090–T093): el asociado arma un pago con conceptos del mismo destino, el canal crea el intento y la sesión en la
// pasarela, y el estado converge por tres caminos que pueden llegar en cualquier orden y repetirse —el retorno del
// asociado, la notificación de la pasarela y la consulta periódica— sin duplicar nada: todos pasan por Actualizar, que
// relee el intento con cerrojo y sólo aplica transiciones válidas (data-model.md, «payment_attempts»).

// LectorDelERP es lo que los pagos necesitan del ERP.
type LectorDelERP interface {
	Conceptos(ctx context.Context, cooperativa, persona uuid.UUID) (erp.Conceptos, error)
	TextosLegales(ctx context.Context, cooperativa uuid.UUID) ([]erp.TextoLegal, error)
}

// Sitios entrega un sitio de pasarela con su secreto descifrado (lo cumple *pasarela.Sitios).
type Sitios interface {
	Cargar(ctx context.Context, id uuid.UUID) (pasarela.Sitio, pasarela.Estado, error)
}

// CodigosDePago toma, en la transacción del pago, el código verificado de la cuenta (lo cumple *identidad.Servicio).
type CodigosDePago interface {
	TomarCodigoDePago(ctx context.Context, q *gen.Queries, cuenta, desafio uuid.UUID) error
}

// Encolador inserta trabajos de River en una transacción (lo cumple *river.Client[pgx.Tx]).
type Encolador interface {
	InsertTx(ctx context.Context, tx pgx.Tx, args river.JobArgs, opts *river.InsertOpts) (*rivertype.JobInsertResult, error)
	Insert(ctx context.Context, args river.JobArgs, opts *river.InsertOpts) (*rivertype.JobInsertResult, error)
}

// AlAprobar corre en la transacción en que un intento queda aprobado (la fase 7 encola ahí el aviso al ERP, FR-044).
type AlAprobar func(ctx context.Context, tx pgx.Tx, intento gen.PaymentAttempt) error

// Opciones del servicio de pagos.
type Opciones struct {
	Pool      *pgxpool.Pool
	ERP       LectorDelERP
	Avisador  AvisadorDelERP
	Sitios    Sitios
	Pasarela  pasarela.Pasarela
	Codigos   CodigosDePago
	Auditoria *auditoria.Registro
	Encolador Encolador
	Logger    *slog.Logger
	// HostPublico arma la URL de retorno: https://{host}/pagos/{id}/retorno.
	HostPublico string
	// EsquemaDeRetorno es https salvo en pruebas.
	EsquemaDeRetorno string
	// ConsultaFrecuente es cada cuánto se consulta un pendiente en sus primeras 24 h (FR-040; 15 min por defecto).
	ConsultaFrecuente time.Duration
	// AlAprobar corre en la transacción de la aprobación; nil = encolar el aviso al ERP (send_payment_notice).
	AlAprobar AlAprobar
	// UmbralDeAlertaDelAviso: en este intento fallido del aviso al ERP se alerta a la cooperativa (FR-047; 5 por defecto).
	UmbralDeAlertaDelAviso int
	Ahora                  func() time.Time
}

// Servicio atiende los intentos de pago.
type Servicio struct {
	o Opciones
	q *gen.Queries
}

// Nuevo crea el servicio. Falla si falta una dependencia.
func Nuevo(o Opciones) (*Servicio, error) {
	switch {
	case o.Pool == nil, o.ERP == nil, o.Avisador == nil, o.Sitios == nil, o.Pasarela == nil, o.Codigos == nil, o.Auditoria == nil, o.Encolador == nil:
		return nil, errors.New("pagos: faltan dependencias")
	case strings.TrimSpace(o.HostPublico) == "":
		return nil, errors.New("pagos: falta el host público")
	}
	if o.Logger == nil {
		o.Logger = slog.Default()
	}
	if o.EsquemaDeRetorno == "" {
		o.EsquemaDeRetorno = "https"
	}
	if o.ConsultaFrecuente <= 0 {
		o.ConsultaFrecuente = 15 * time.Minute
	}
	if o.Ahora == nil {
		o.Ahora = time.Now
	}
	if o.UmbralDeAlertaDelAviso <= 0 {
		o.UmbralDeAlertaDelAviso = UmbralPorDefecto
	}
	s := &Servicio{o: o, q: gen.New(o.Pool)}
	if s.o.AlAprobar == nil {
		s.o.AlAprobar = s.encolarAviso
	}
	return s, nil
}

func (s *Servicio) ahora() time.Time { return s.o.Ahora().UTC() }

// Registrador recibe las rutas (lo cumple *httpapi.Servidor).
type Registrador interface {
	Manejar(patron string, h http.Handler)
}

// Registrar pone las rutas del asociado (dentro de la cooperativa elegida) y el webhook de la pasarela.
func (s *Servicio) Registrar(m Registrador, acotada func(http.Handler) http.Handler) {
	m.Manejar("POST /api/v1/payments", acotada(http.HandlerFunc(s.crear)))
	m.Manejar("GET /api/v1/payments/{id}", acotada(http.HandlerFunc(s.consultar)))
	m.Manejar("GET /api/v1/payments", acotada(http.HandlerFunc(s.historial)))
	m.Manejar("GET /api/v1/payments/pending", acotada(http.HandlerFunc(s.pendientes)))
	m.Manejar("GET /api/v1/payments/{id}/receipt", acotada(http.HandlerFunc(s.comprobante)))
	m.Manejar("POST /webhooks/placetopay/{siteId}", http.HandlerFunc(s.webhook))
}

// ------------------------------------------------------------------------------------------------------ errores --

// Errores que ve el asociado (contracts/canal-api.md §A).
var (
	ErrPedidoInvalido    = sobre.Nuevo(http.StatusBadRequest, "Canal.Pedido.Invalido", "La solicitud no tiene la forma esperada.")
	ErrSinLlave          = sobre.Nuevo(http.StatusBadRequest, "Canal.Pago.SinLlave", "Falta la cabecera Idempotency-Key.")
	ErrNoExiste          = sobre.Nuevo(http.StatusNotFound, "Canal.Pago.NoExiste", "No encontramos ese pago.")
	ErrEntidadNoDisp     = sobre.Nuevo(http.StatusServiceUnavailable, "Canal.Entidad.NoDisponible", "La información de tu cooperativa no está disponible en este momento. Intenta de nuevo en unos minutos.")
	ErrCoopNoDisponible  = sobre.Nuevo(http.StatusForbidden, "Canal.Cooperativa.NoDisponible", "Esta cooperativa ya no está disponible para ti en el canal. Comunícate con ella si crees que es un error.")
	ErrPasarelaNoDisp    = sobre.Nuevo(http.StatusServiceUnavailable, "Canal.Pago.PasarelaNoDisponible", "El pago en línea no está disponible en este momento. Intenta de nuevo más tarde.")
	ErrDestinosDistintos = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Pago.DestinosDistintos",
		"Estos productos se pagan por separado: arma un pago para cada grupo.")
	ErrTextosPorAceptar = sobre.Nuevo(http.StatusConflict, "Canal.Pago.TextosPorAceptar",
		"Tu cooperativa actualizó sus términos o su política de datos. Léelos y acéptalos para continuar.")
)

func errConcepto(codigo, mensaje, clave string, datos map[string]any) *sobre.Error {
	if datos == nil {
		datos = map[string]any{}
	}
	datos["conceptKey"] = clave
	return sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Pago."+codigo, mensaje).ConDatos(datos)
}

// ---------------------------------------------------------------------------------------------------- respuesta --

// Item es un concepto del pago tal como se guarda en payment_attempts.items y vuelve al asociado.
type Item struct {
	Clave       string    `json:"conceptKey"`
	Producto    uuid.UUID `json:"productPublicId"`
	Tipo        string    `json:"kind"`
	Modalidad   string    `json:"modality"`
	Valor       int64     `json:"amount"`
	Eleccion    *string   `json:"prepaymentChoice"`
	Descripcion string    `json:"description"`
}

// Respuesta es un intento visto por el asociado (FR-038: referencia, estado, fecha y hora, valor, moneda,
// autorización o CUS).
type Respuesta struct {
	ID           uuid.UUID            `json:"attemptId"`
	Referencia   string               `json:"reference"`
	Estado       string               `json:"status"`
	Total        int64                `json:"total"`
	Moneda       string               `json:"currency"`
	Items        []Item               `json:"items"`
	Creado       time.Time            `json:"createdAt"`
	Actualizado  time.Time            `json:"updatedAt"`
	Vence        time.Time            `json:"expiresAt"`
	ProcessURL   *string              `json:"processUrl"`
	Aprobacion   *pasarela.Aprobacion `json:"approval"`
	Mensaje      *string              `json:"message"`
	ResultadoERP string               `json:"erpOutcome"`
	ReciboDelERP *uuid.UUID           `json:"erpReceiptPublicId"`
	Soporte      json.RawMessage      `json:"support"`
	// Reversado y TipoReverso: el pago se reversó por la pasarela (gateway) o se devolvió por Tesorería (treasury) (FR-053).
	Reversado   *time.Time `json:"reversedAt"`
	TipoReverso *string    `json:"reversalKind"`
}

func (s *Servicio) respuesta(ctx context.Context, a gen.PaymentAttempt) Respuesta {
	r := s.respuestaSinSoporte(a)
	if t, err := s.q.GetTenantByID(ctx, a.TenantID); err == nil && len(t.Support) > 0 {
		r.Soporte = t.Support
	}
	return r
}

// respuestaSinSoporte arma la respuesta sin leer el contacto de la cooperativa (el historial lo manda una sola vez).
func (s *Servicio) respuestaSinSoporte(a gen.PaymentAttempt) Respuesta {
	r := Respuesta{
		ID: a.ID, Referencia: a.Reference, Estado: a.Status, Total: a.Total, Moneda: a.Currency, Creado: a.CreatedAt,
		Actualizado: a.UpdatedAt, Vence: a.ExpiresAt, Mensaje: a.StatusMessage, ResultadoERP: a.ErpOutcome,
		ReciboDelERP: a.ErpReceiptPublicID, Items: []Item{}, Soporte: json.RawMessage("{}"),
		Reversado: a.ReversedAt, TipoReverso: a.ReversalKind,
	}
	_ = json.Unmarshal(a.Items, &r.Items)
	if len(a.Approval) > 0 {
		var ap pasarela.Aprobacion
		if json.Unmarshal(a.Approval, &ap) == nil {
			r.Aprobacion = &ap
		}
	}
	// La URL de la pasarela sólo sirve mientras la sesión sigue abierta.
	if a.Status == "pending" && s.ahora().Before(a.ExpiresAt) {
		r.ProcessURL = a.ProcessUrl
	}
	return r
}

// -------------------------------------------------------------------------------------------------- utilidades --

const maximoCuerpo = 16 << 10

// leer decodifica el cuerpo JSON estricto. Exige Content-Type JSON (segunda defensa contra CSRF, como la identidad).
func leer(w http.ResponseWriter, r *http.Request, destino any) *sobre.Error {
	if !strings.HasPrefix(strings.ToLower(r.Header.Get("Content-Type")), "application/json") {
		return ErrPedidoInvalido
	}
	d := json.NewDecoder(http.MaxBytesReader(w, r.Body, maximoCuerpo))
	d.DisallowUnknownFields()
	if err := d.Decode(destino); err != nil {
		return ErrPedidoInvalido
	}
	if _, err := d.Token(); !errors.Is(err, io.EOF) {
		return ErrPedidoInvalido
	}
	return nil
}

// alcance lee cooperativa, persona y cuenta que dejó la sesión acotada.
func (s *Servicio) alcance(w http.ResponseWriter, r *http.Request) (contexto.Cooperativa, uuid.UUID, uuid.UUID, bool) {
	c, okC := contexto.CooperativaDe(r.Context())
	p, okP := contexto.Persona(r.Context())
	cuenta, okA := contexto.Cuenta(r.Context())
	if !okC || !okP || !okA || c.PublicID == uuid.Nil {
		s.o.Logger.ErrorContext(r.Context(), "ruta de pagos sin cooperativa, persona o cuenta en el contexto")
		sobre.Escribir(w, sobre.ErrInesperado)
		return contexto.Cooperativa{}, uuid.Nil, uuid.Nil, false
	}
	return c, p, cuenta, true
}

func (s *Servicio) fallo(w http.ResponseWriter, r *http.Request, que string, err error) {
	s.o.Logger.ErrorContext(r.Context(), que, "error", err)
	sobre.Escribir(w, sobre.ErrInesperado)
}

func (s *Servicio) auditar(ctx context.Context, accion, sujeto string, datos any) {
	if err := s.o.Auditoria.Registrar(ctx, accion, sujeto, datos); err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo auditar", "accion", accion, "error", err)
	}
}

// enTransaccion corre fn en una transacción.
func (s *Servicio) enTransaccion(ctx context.Context, fn func(q *gen.Queries, tx pgx.Tx) error) error {
	tx, err := s.o.Pool.Begin(ctx)
	if err != nil {
		return fmt.Errorf("pagos: abrir la transacción: %w", err)
	}
	defer func() {
		if err := tx.Rollback(context.WithoutCancel(ctx)); err != nil && !errors.Is(err, pgx.ErrTxClosed) {
			s.o.Logger.ErrorContext(ctx, "no se pudo deshacer la transacción de pagos", "error", err)
		}
	}()
	if err := fn(s.q.WithTx(tx), tx); err != nil {
		return err
	}
	return tx.Commit(ctx)
}

func nuevoID() uuid.UUID {
	id, err := uuid.NewV7()
	if err != nil {
		return uuid.New()
	}
	return id
}

func recortar(s string, n int) string {
	r := []rune(s)
	if len(r) <= n {
		return s
	}
	return string(r[:n])
}

func ptr[T any](v T) *T { return &v }
