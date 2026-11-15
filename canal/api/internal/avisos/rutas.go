package avisos

import (
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"net/url"
	"strings"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria/push"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T132, T134 (contracts/canal-api.md §A): las preferencias de aviso de la cuenta y el registro del dispositivo para push. Van con
// sesión pero sin cooperativa elegida: los avisos son de la persona, en todas sus cooperativas.

// Errores.
var (
	ErrPedidoInvalido = sobre.Nuevo(http.StatusBadRequest, "Canal.Avisos.PedidoInvalido", "La solicitud no tiene la forma esperada.")
	ErrNoConfigurable = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Avisos.NoConfigurable", "Los avisos de seguridad no se pueden apagar.")
	ErrPushApagado    = sobre.Nuevo(http.StatusServiceUnavailable, "Canal.Avisos.PushNoDisponible",
		"Las notificaciones en este navegador no están disponibles todavía. Te seguiremos avisando por correo.")
)

// Registrador es el servidor donde se ponen las rutas.
type Registrador interface {
	Manejar(patron string, h http.Handler)
}

// Registrar pone las rutas; conSesion exige sesión y deja la cuenta en el contexto (identidad.Servicio.ConSesion).
func (s *Servicio) Registrar(m Registrador, conSesion func(http.Handler) http.Handler) {
	m.Manejar("GET /api/v1/notifications/preferences", conSesion(http.HandlerFunc(s.leerPreferencias)))
	m.Manejar("PUT /api/v1/notifications/preferences", conSesion(http.HandlerFunc(s.guardarPreferencias)))
	m.Manejar("GET /api/v1/notifications/webpush-key", conSesion(http.HandlerFunc(s.llaveWebPush)))
	m.Manejar("POST /api/v1/devices/push-token", conSesion(http.HandlerFunc(s.registrarDispositivo)))
}

func (s *Servicio) leerPreferencias(w http.ResponseWriter, r *http.Request) {
	cuenta, ok := contexto.Cuenta(r.Context())
	if !ok {
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	ps, err := s.Preferencias(r.Context(), cuenta)
	if err != nil {
		s.o.Logger.ErrorContext(r.Context(), "no se pudieron leer las preferencias de aviso", "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"preferences": ps, "webPush": s.o.LlaveWebPush != ""})
}

func (s *Servicio) guardarPreferencias(w http.ResponseWriter, r *http.Request) {
	cuenta, ok := contexto.Cuenta(r.Context())
	if !ok {
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	var cuerpo struct {
		Preferencias []Preferencia `json:"preferences"`
	}
	if !leer(w, r, &cuerpo) || len(cuerpo.Preferencias) == 0 || len(cuerpo.Preferencias) > len(Configurables) {
		sobre.Escribir(w, ErrPedidoInvalido)
		return
	}
	if err := s.GuardarPreferencias(r.Context(), cuenta, cuerpo.Preferencias); err != nil {
		if errors.Is(err, ErrClaseNoConfigurable) {
			sobre.Escribir(w, ErrNoConfigurable)
			return
		}
		s.o.Logger.ErrorContext(r.Context(), "no se pudieron guardar las preferencias de aviso", "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	s.leerPreferencias(w, r)
}

func (s *Servicio) llaveWebPush(w http.ResponseWriter, _ *http.Request) {
	if s.o.LlaveWebPush == "" {
		sobre.Escribir(w, ErrPushApagado)
		return
	}
	sobre.JSON(w, http.StatusOK, map[string]string{"publicKey": s.o.LlaveWebPush})
}

// PedidoDeDispositivo es el cuerpo de POST /api/v1/devices/push-token: el token de FCM (app) o la suscripción de Web Push (PWA).
type PedidoDeDispositivo struct {
	Tipo        string `json:"kind"`
	Token       string `json:"token"`
	Suscripcion *struct {
		Endpoint string `json:"endpoint"`
		Llaves   struct {
			P256dh string `json:"p256dh"`
			Auth   string `json:"auth"`
		} `json:"keys"`
	} `json:"subscription"`
}

func (s *Servicio) registrarDispositivo(w http.ResponseWriter, r *http.Request) {
	cuenta, ok := contexto.Cuenta(r.Context())
	if !ok {
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	var p PedidoDeDispositivo
	if !leer(w, r, &p) {
		sobre.Escribir(w, ErrPedidoInvalido)
		return
	}
	var endpoint string
	var p256dh, auth *string
	switch p.Tipo {
	case push.FCM:
		endpoint = strings.TrimSpace(p.Token)
		if endpoint == "" || len(endpoint) > 2048 {
			sobre.Escribir(w, ErrPedidoInvalido)
			return
		}
	case push.WebPush:
		if p.Suscripcion == nil || s.o.LlaveWebPush == "" {
			sobre.Escribir(w, ErrPedidoInvalido)
			return
		}
		u, err := url.Parse(p.Suscripcion.Endpoint)
		if err != nil || u.Scheme != "https" || len(p.Suscripcion.Endpoint) > 2048 || p.Suscripcion.Llaves.P256dh == "" || p.Suscripcion.Llaves.Auth == "" ||
			len(p.Suscripcion.Llaves.P256dh) > 200 || len(p.Suscripcion.Llaves.Auth) > 100 {
			sobre.Escribir(w, ErrPedidoInvalido)
			return
		}
		endpoint, p256dh, auth = p.Suscripcion.Endpoint, &p.Suscripcion.Llaves.P256dh, &p.Suscripcion.Llaves.Auth
	default:
		sobre.Escribir(w, ErrPedidoInvalido)
		return
	}
	sub, err := s.RegistrarSuscripcion(r.Context(), cuenta, p.Tipo, endpoint, p256dh, auth)
	if err != nil {
		s.o.Logger.ErrorContext(r.Context(), "no se pudo registrar el dispositivo para push", "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"subscriptionId": sub.ID})
}

// leer decodifica el cuerpo JSON estricto (con Content-Type JSON, segunda defensa contra CSRF).
func leer(w http.ResponseWriter, r *http.Request, destino any) bool {
	if !strings.HasPrefix(strings.ToLower(r.Header.Get("Content-Type")), "application/json") {
		return false
	}
	d := json.NewDecoder(http.MaxBytesReader(w, r.Body, 8<<10))
	d.DisallowUnknownFields()
	if err := d.Decode(destino); err != nil {
		return false
	}
	_, err := d.Token()
	return errors.Is(err, io.EOF)
}
