// Package push entrega notificaciones al teléfono o al navegador (feature 021, US10; FR-056; research R15): FCM HTTP v1 para la
// app de tiendas y Web Push con VAPID (RFC 8291 y 8292) para la PWA instalada. Sin credenciales, el tipo no está disponible y el
// aviso sale sólo por correo. Una suscripción que el proveedor da por vencida se revoca.
package push

import (
	"context"
	"errors"
	"log/slog"
	"sync"

	"github.com/google/uuid"
)

// Tipos de suscripción (push_subscriptions.kind).
const (
	FCM     = "fcm"
	WebPush = "webpush"
)

// Errores.
var (
	// ErrNoDisponible: el tipo de suscripción no tiene proveedor configurado.
	ErrNoDisponible = errors.New("push: el proveedor no está configurado")
	// ErrVencida: el proveedor dice que la suscripción ya no existe (la app se desinstaló, el permiso se retiró).
	ErrVencida = errors.New("push: la suscripción ya no existe")
)

// Suscripcion es un destino: el token de FCM, o el endpoint de Web Push con sus llaves (base64url).
type Suscripcion struct {
	ID       uuid.UUID
	Tipo     string
	Endpoint string
	P256dh   string
	Auth     string
}

// Notificacion es lo que se muestra: título, texto, a dónde lleva al tocarla y su clase.
type Notificacion struct {
	Titulo string `json:"title"`
	Cuerpo string `json:"body"`
	URL    string `json:"url"`
	Tipo   string `json:"kind"`
}

// Enviador entrega una notificación a una suscripción.
type Enviador interface {
	Enviar(ctx context.Context, s Suscripcion, n Notificacion) error
}

// Compuesto enruta por tipo de suscripción.
type Compuesto struct {
	PorFCM     Enviador
	PorWebPush Enviador
}

func (c Compuesto) Enviar(ctx context.Context, s Suscripcion, n Notificacion) error {
	var e Enviador
	switch s.Tipo {
	case FCM:
		e = c.PorFCM
	case WebPush:
		e = c.PorWebPush
	}
	if e == nil {
		return ErrNoDisponible
	}
	return e.Enviar(ctx, s, n)
}

// Consola escribe la notificación en el log (desarrollo).
type Consola struct{ Logger *slog.Logger }

func (c Consola) Enviar(ctx context.Context, s Suscripcion, n Notificacion) error {
	c.Logger.InfoContext(ctx, "notificación (consola)", "tipo", s.Tipo, "suscripcion", s.ID, "titulo", n.Titulo, "cuerpo", n.Cuerpo, "url", n.URL)
	return nil
}

// Memoria guarda lo enviado (pruebas). Vencidas son los endpoints que responden ErrVencida.
type Memoria struct {
	mu       sync.Mutex
	enviadas []Enviada
	Vencidas map[string]bool
}

// Enviada es una notificación entregada a una suscripción.
type Enviada struct {
	Suscripcion Suscripcion
	Notificacion
}

func (m *Memoria) Enviar(_ context.Context, s Suscripcion, n Notificacion) error {
	m.mu.Lock()
	defer m.mu.Unlock()
	if m.Vencidas[s.Endpoint] {
		return ErrVencida
	}
	m.enviadas = append(m.enviadas, Enviada{Suscripcion: s, Notificacion: n})
	return nil
}

// Enviadas devuelve una copia de lo enviado.
func (m *Memoria) Enviadas() []Enviada {
	m.mu.Lock()
	defer m.mu.Unlock()
	return append([]Enviada(nil), m.enviadas...)
}
