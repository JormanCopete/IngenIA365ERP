// Package avisos manda los avisos al asociado (feature 021, US10; FR-012, FR-056): cuota próxima, pago aplicado, pago rechazado,
// pendiente resuelto y seguridad, por correo y por notificación en la app o el navegador. La persona apaga cada clase por correo o
// por notificación, salvo los de seguridad. Un aviso es un trabajo de River (send_notification): si el correo no sale, se reintenta;
// una suscripción de push vencida se revoca y no detiene el resto.
package avisos

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/riverqueue/river"
	"github.com/riverqueue/river/rivertype"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria/push"
)

// Clases de aviso. Las de seguridad no tienen preferencia: siempre salen.
const (
	CuotaProxima      = "due_reminder"
	PagoAplicado      = "payment_applied"
	PagoRechazado     = "payment_rejected"
	PendienteResuelto = "pending_resolved"
	Seguridad         = "security"
)

// Configurables son las clases que la persona puede apagar, en el orden en que se muestran.
var Configurables = []string{CuotaProxima, PagoAplicado, PagoRechazado, PendienteResuelto}

// EsConfigurable dice si la clase admite preferencia.
func EsConfigurable(clase string) bool {
	for _, c := range Configurables {
		if c == clase {
			return true
		}
	}
	return false
}

// Preferencia de una clase: por correo y por notificación (sin fila, las dos encendidas).
type Preferencia struct {
	Clase     string `json:"kind"`
	Correo    bool   `json:"email"`
	Push      bool   `json:"push"`
	Bloqueada bool   `json:"locked"`
}

// ArgsEnviarAviso es el trabajo send_notification.
type ArgsEnviarAviso struct {
	Cuenta uuid.UUID `json:"accountId"`
	Clase  string    `json:"kind"`
	Titulo string    `json:"title"`
	Cuerpo string    `json:"body"`
	URL    string    `json:"url"`
	// Llave hace único el aviso (la misma cuota del mismo día, el mismo intento): repetir el encolado no lo duplica.
	Llave string `json:"key"`
}

// Kind del trabajo.
func (ArgsEnviarAviso) Kind() string { return "send_notification" }

// InsertOpts: un aviso por llave (mientras no se haya completado hace menos de un día).
func (ArgsEnviarAviso) InsertOpts() river.InsertOpts {
	return river.InsertOpts{MaxAttempts: 8, UniqueOpts: river.UniqueOpts{ByArgs: true, ByPeriod: 24 * time.Hour}}
}

// Encolador inserta trabajos de River (lo cumple *river.Client[pgx.Tx]).
type Encolador interface {
	InsertTx(ctx context.Context, tx pgx.Tx, args river.JobArgs, opts *river.InsertOpts) (*rivertype.JobInsertResult, error)
	Insert(ctx context.Context, args river.JobArgs, opts *river.InsertOpts) (*rivertype.JobInsertResult, error)
}

// Opciones del servicio.
type Opciones struct {
	Q         *gen.Queries
	Mensajero mensajeria.Mensajero
	Push      push.Enviador
	Auditoria *auditoria.Registro
	Logger    *slog.Logger
	// LlaveWebPush es la llave VAPID pública que usa la PWA para suscribirse; vacía = Web Push apagado.
	LlaveWebPush string
}

// Servicio de avisos.
type Servicio struct{ o Opciones }

// Nuevo crea el servicio.
func Nuevo(o Opciones) *Servicio {
	if o.Logger == nil {
		o.Logger = slog.Default()
	}
	return &Servicio{o: o}
}

// Preferencias de la cuenta, con las de seguridad al final y bloqueadas.
func (s *Servicio) Preferencias(ctx context.Context, cuenta uuid.UUID) ([]Preferencia, error) {
	filas, err := s.o.Q.ListNotificationPreferences(ctx, cuenta)
	if err != nil {
		return nil, err
	}
	guardadas := map[string]gen.NotificationPreference{}
	for _, f := range filas {
		guardadas[f.Kind] = f
	}
	salida := make([]Preferencia, 0, len(Configurables)+1)
	for _, c := range Configurables {
		p := Preferencia{Clase: c, Correo: true, Push: true}
		if f, ok := guardadas[c]; ok {
			p.Correo, p.Push = f.ByEmail, f.ByPush
		}
		salida = append(salida, p)
	}
	return append(salida, Preferencia{Clase: Seguridad, Correo: true, Push: true, Bloqueada: true}), nil
}

// ErrClaseNoConfigurable: los avisos de seguridad no se apagan.
var ErrClaseNoConfigurable = errors.New("avisos: esa clase no se puede apagar")

// GuardarPreferencias cambia las preferencias de la cuenta.
func (s *Servicio) GuardarPreferencias(ctx context.Context, cuenta uuid.UUID, ps []Preferencia) error {
	for _, p := range ps {
		if !EsConfigurable(p.Clase) {
			return ErrClaseNoConfigurable
		}
	}
	for _, p := range ps {
		if err := s.o.Q.UpsertNotificationPreference(ctx, gen.UpsertNotificationPreferenceParams{AccountID: cuenta, Kind: p.Clase, ByEmail: p.Correo,
			ByPush: p.Push}); err != nil {
			return err
		}
	}
	if s.o.Auditoria != nil {
		_ = s.o.Auditoria.Registrar(ctx, "Canal.Avisos.PreferenciasCambiadas", cuenta.String(), ps)
	}
	return nil
}

// Enviar entrega el aviso según las preferencias. Devuelve error sólo si el correo pedido no salió (River reintenta); el push es
// el mejor esfuerzo.
func (s *Servicio) Enviar(ctx context.Context, a ArgsEnviarAviso) error {
	cuenta, err := s.o.Q.GetAccount(ctx, a.Cuenta)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil
	}
	if err != nil {
		return err
	}
	porCorreo, porPush := true, true
	if EsConfigurable(a.Clase) {
		ps, err := s.Preferencias(ctx, a.Cuenta)
		if err != nil {
			return err
		}
		for _, p := range ps {
			if p.Clase == a.Clase {
				porCorreo, porPush = p.Correo, p.Push
			}
		}
	}
	if porPush && s.o.Push != nil {
		s.empujar(ctx, a)
	}
	if porCorreo && cuenta.Email != nil && s.o.Mensajero != nil {
		texto := a.Cuerpo
		if a.URL != "" {
			texto += "\n\nÁbrelo en el canal: " + a.URL
		}
		if err := s.o.Mensajero.Enviar(ctx, mensajeria.Mensaje{Canal: mensajeria.Correo, Destino: *cuenta.Email, Asunto: a.Titulo, Texto: texto}); err != nil {
			if errors.Is(err, mensajeria.ErrNoDisponible) {
				return nil
			}
			return fmt.Errorf("avisos: correo: %w", err)
		}
	}
	return nil
}

func (s *Servicio) empujar(ctx context.Context, a ArgsEnviarAviso) {
	subs, err := s.o.Q.ListPushSubscriptions(ctx, a.Cuenta)
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudieron leer las suscripciones de push", "error", err)
		return
	}
	n := push.Notificacion{Titulo: a.Titulo, Cuerpo: a.Cuerpo, URL: a.URL, Tipo: a.Clase}
	for _, sub := range subs {
		destino := push.Suscripcion{ID: sub.ID, Tipo: sub.Kind, Endpoint: sub.Endpoint}
		if sub.P256dh != nil && sub.Auth != nil {
			destino.P256dh, destino.Auth = *sub.P256dh, *sub.Auth
		}
		switch err := s.o.Push.Enviar(ctx, destino, n); {
		case err == nil:
			_ = s.o.Q.TouchPushSubscription(ctx, sub.ID)
		case errors.Is(err, push.ErrVencida):
			_ = s.o.Q.RevokePushSubscription(ctx, sub.ID)
		case errors.Is(err, push.ErrNoDisponible):
		default:
			s.o.Logger.WarnContext(ctx, "el aviso por push no salió", "suscripcion", sub.ID, "error", err)
		}
	}
}

// RegistrarSuscripcion guarda el token de FCM o la suscripción de Web Push del dispositivo de la cuenta.
func (s *Servicio) RegistrarSuscripcion(ctx context.Context, cuenta uuid.UUID, tipo, endpoint string, p256dh, auth *string) (gen.PushSubscription, error) {
	sub, err := s.o.Q.UpsertPushSubscription(ctx, gen.UpsertPushSubscriptionParams{ID: uuid.Must(uuid.NewV7()), AccountID: cuenta, Kind: tipo,
		Endpoint: endpoint, P256dh: p256dh, Auth: auth})
	if err == nil && s.o.Auditoria != nil {
		_ = s.o.Auditoria.Registrar(ctx, "Canal.Avisos.DispositivoRegistrado", cuenta.String(), map[string]any{"kind": tipo})
	}
	return sub, err
}

// TrabajadorDeAvisos corre send_notification.
type TrabajadorDeAvisos struct {
	river.WorkerDefaults[ArgsEnviarAviso]
	Avisos *Servicio
}

func (t *TrabajadorDeAvisos) Work(ctx context.Context, j *river.Job[ArgsEnviarAviso]) error {
	return t.Avisos.Enviar(ctx, j.Args)
}
