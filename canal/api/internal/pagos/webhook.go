package pagos

import (
	"context"
	"errors"
	"io"
	"net/http"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
)

// T092 (FR-039, R11): POST /webhooks/placetopay/{siteId}. Responde 200 enseguida —la pasarela no reintenta— y encola
// la consulta del intento: la notificación sólo dice «mira este intento», y lo que cuenta es lo que responda la
// consulta. Una firma inválida responde también 200, sin efecto ni pista para el emisor, y queda auditada; si se repite,
// alerta.

// AccionFirmaInvalida es la acción de auditoría de una notificación con firma inválida.
const AccionFirmaInvalida = "Canal.Webhook.FirmaInvalida"

// UmbralDeFirmasInvalidas en una hora por sitio dispara la alerta.
const UmbralDeFirmasInvalidas = 3

func (s *Servicio) webhook(w http.ResponseWriter, r *http.Request) {
	ctx := contexto.ConActor(r.Context(), contexto.ActorSistema)
	cuerpo, err := io.ReadAll(http.MaxBytesReader(w, r.Body, maximoCuerpo))
	// Siempre 200: el emisor no aprende nada de la respuesta.
	defer w.WriteHeader(http.StatusOK)
	sitioID, errID := uuid.Parse(r.PathValue("siteId"))
	if err != nil || errID != nil {
		s.firmaInvalida(ctx, r.PathValue("siteId"), "cuerpo o sitio ilegible")
		return
	}
	sitio, _, err := s.o.Sitios.Cargar(ctx, sitioID)
	if errors.Is(err, pasarela.ErrSitioNoExiste) {
		s.firmaInvalida(ctx, sitioID.String(), "sitio desconocido")
		return
	}
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo cargar el sitio de la notificación", "sitio", sitioID, "error", err)
		return
	}
	n, err := s.o.Pasarela.VerificarNotificacion(sitio, cuerpo)
	if err != nil {
		s.firmaInvalida(ctx, sitioID.String(), "firma")
		return
	}
	a, err := s.q.GetPaymentAttemptByRequest(ctx, gen.GetPaymentAttemptByRequestParams{SiteID: sitioID, GatewayRequestID: &n.RequestID})
	if errors.Is(err, pgx.ErrNoRows) {
		s.o.Logger.WarnContext(ctx, "notificación firmada de una sesión que el canal no conoce", "sitio", sitioID, "requestId", n.RequestID)
		s.auditar(ctx, "Canal.Webhook.SesionDesconocida", sitioID.String(), map[string]any{"requestId": n.RequestID, "reference": n.Referencia})
		return
	}
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo buscar el intento de la notificación", "error", err)
		return
	}
	ctx = contexto.ConIntento(ctx, a.ID)
	s.auditar(ctx, "Canal.Webhook.Recibida", a.ID.String(), map[string]any{"reference": a.Reference, "gatewayStatus": n.Estado})
	if _, err := s.o.Encolador.Insert(ctx, ArgsConsultarIntento{Intento: a.ID, Fuente: FuenteWebhook}, nil); err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo encolar la consulta de la notificación; la sonda la recogerá", "intento", a.ID, "error", err)
	}
}

func (s *Servicio) firmaInvalida(ctx context.Context, sitio, motivo string) {
	s.auditar(ctx, AccionFirmaInvalida, sitio, map[string]any{"reason": motivo})
	n, err := s.q.CountRecentAuditForSubject(ctx, gen.CountRecentAuditForSubjectParams{Action: AccionFirmaInvalida, Subject: sitio,
		Since: s.ahora().Add(-time.Hour)})
	if err == nil && n >= UmbralDeFirmasInvalidas {
		s.o.Logger.ErrorContext(ctx, "ALERTA: notificaciones con firma inválida repetidas para el sitio", "sitio", sitio, "en_la_ultima_hora", n)
	}
}

// ------------------------------------------------------------------------------------------------------ trabajos --

// ArgsConsultarIntento: consultar un intento en la pasarela (tras una notificación).
type ArgsConsultarIntento struct {
	Intento uuid.UUID `json:"attemptId"`
	Fuente  string    `json:"source"`
}

// Kind del trabajo.
func (ArgsConsultarIntento) Kind() string { return "check_payment_attempt" }

// InsertOpts: una notificación repetida no encola dos consultas a la vez.
func (ArgsConsultarIntento) InsertOpts() river.InsertOpts {
	return river.InsertOpts{UniqueOpts: river.UniqueOpts{ByArgs: true, ByPeriod: time.Minute}}
}

// TrabajadorDeConsulta corre ArgsConsultarIntento.
type TrabajadorDeConsulta struct {
	river.WorkerDefaults[ArgsConsultarIntento]
	Pagos *Servicio
}

// Work consulta el intento. Si la pasarela no responde, River reintenta.
func (t *TrabajadorDeConsulta) Work(ctx context.Context, job *river.Job[ArgsConsultarIntento]) error {
	ctx = contexto.ConActor(contexto.ConIntento(ctx, job.Args.Intento), contexto.ActorSistema)
	_, err := t.Pagos.Actualizar(ctx, job.Args.Intento, job.Args.Fuente)
	return err
}
