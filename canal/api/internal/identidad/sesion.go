package identidad

import (
	"context"
	"errors"
	"net/http"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Reglas de la sesión (FR-027, T066).
const (
	InactividadPorDefecto = 5  // minutos
	InactividadMaxima     = 15 // lo más que puede subirla una cooperativa
	DuracionMaxima        = 8 * time.Hour
	// Cada cuánto se anota la actividad: escribir en cada petición no cambia nada con 5 minutos de margen.
	anotarActividadCada = 15 * time.Second

	CookieDeSesion      = "canal_sesion"
	CookieDeDispositivo = "canal_dispositivo"
	// La credencial de dispositivo de la PWA vive un año en su cookie; en la app, en el almacén seguro.
	vidaDeCookieDeDispositivo = 365 * 24 * time.Hour

	// CabeceraPlataforma la manda la app («android», «ios»); sin ella es el navegador.
	CabeceraPlataforma = "X-Canal-Plataforma"
)

// Plataforma desde donde llega la petición.
type Plataforma string

const (
	Navegador Plataforma = "browser"
	Android   Plataforma = "android"
	IOS       Plataforma = "ios"
)

// PlataformaDe lee la cabecera de la app; todo lo demás es navegador.
func PlataformaDe(r *http.Request) Plataforma {
	switch strings.ToLower(strings.TrimSpace(r.Header.Get(CabeceraPlataforma))) {
	case "android":
		return Android
	case "ios":
		return IOS
	}
	return Navegador
}

func (p Plataforma) esApp() bool { return p == Android || p == IOS }

// SesionActiva es la sesión de la petición.
type SesionActiva struct {
	ID              uuid.UUID
	CuentaID        uuid.UUID
	DispositivoID   *uuid.UUID
	Documento       Documento
	CooperativaID   *uuid.UUID
	Inactividad     time.Duration
	UltimaActividad time.Time
	Creada          time.Time
	Vence           time.Time
}

// VenceSinActividad es el momento en que la sesión se corta si no hay otra petición.
func (s SesionActiva) VenceSinActividad() time.Time {
	limite := s.UltimaActividad.Add(s.Inactividad)
	if limite.After(s.Vence) {
		return s.Vence
	}
	return limite
}

type claveSesion struct{}

// SesionDe devuelve la sesión que puso el middleware.
func SesionDe(ctx context.Context) (SesionActiva, bool) {
	v, ok := ctx.Value(claveSesion{}).(SesionActiva)
	return v, ok
}

// RespuestaDeSesion es lo que recibe el cliente al abrir una sesión. Token y DeviceCredential sólo van a la app (en
// la PWA viajan en cookies HttpOnly que el JavaScript no ve).
type RespuestaDeSesion struct {
	Session          EstadoDeSesion `json:"session"`
	Token            string         `json:"token,omitempty"`
	DeviceCredential string         `json:"deviceCredential,omitempty"`
}

// EstadoDeSesion: los dos relojes de la sesión.
type EstadoDeSesion struct {
	InactivityMinutes int       `json:"inactivityMinutes"`
	IdleExpiresAt     time.Time `json:"idleExpiresAt"`
	ExpiresAt         time.Time `json:"expiresAt"`
	TenantSelected    bool      `json:"tenantSelected"`
}

func estadoDe(s SesionActiva) EstadoDeSesion {
	return EstadoDeSesion{
		InactivityMinutes: int(s.Inactividad / time.Minute),
		IdleExpiresAt:     s.VenceSinActividad(),
		ExpiresAt:         s.Vence,
		TenantSelected:    s.CooperativaID != nil,
	}
}

// abrirSesion crea la sesión en la transacción y responde el token: cookie en la PWA, cuerpo en la app.
func (s *Servicio) abrirSesion(ctx context.Context, q *gen.Queries, w http.ResponseWriter, r *http.Request, cuenta gen.Account,
	dispositivo *uuid.UUID) (RespuestaDeSesion, error) {
	token, hash, err := NuevoSecreto()
	if err != nil {
		return RespuestaDeSesion{}, err
	}
	ahora := s.ahora()
	id := nuevoID()
	vence := ahora.Add(DuracionMaxima)
	ip, agente := contexto.IP(ctx), contexto.Agente(ctx)
	if err := q.InsertSession(ctx, gen.InsertSessionParams{
		ID: id, AccountID: cuenta.ID, DeviceID: dispositivo, TokenHash: hash, InactivityMinutes: InactividadPorDefecto,
		CreatedAt: ahora, ExpiresAt: vence, Ip: recortar(ip, 64), UserAgent: recortar(agente, 512),
	}); err != nil {
		return RespuestaDeSesion{}, err
	}
	activa := SesionActiva{ID: id, CuentaID: cuenta.ID, DispositivoID: dispositivo, Inactividad: InactividadPorDefecto * time.Minute,
		UltimaActividad: ahora, Creada: ahora, Vence: vence}
	resp := RespuestaDeSesion{Session: estadoDe(activa)}
	if PlataformaDe(r).esApp() {
		resp.Token = token
	} else {
		http.SetCookie(w, s.cookie(CookieDeSesion, token, "/api/", vence))
	}
	return resp, nil
}

// entregarCredencialDeDispositivo: cookie larga en la PWA, cuerpo en la app.
func (s *Servicio) entregarCredencialDeDispositivo(w http.ResponseWriter, r *http.Request, resp *RespuestaDeSesion, credencial string) {
	if credencial == "" {
		return
	}
	if PlataformaDe(r).esApp() {
		resp.DeviceCredential = credencial
		return
	}
	http.SetCookie(w, s.cookie(CookieDeDispositivo, credencial, "/api/v1/auth/", s.ahora().Add(vidaDeCookieDeDispositivo)))
}

func (s *Servicio) cookie(nombre, valor, ruta string, vence time.Time) *http.Cookie {
	return &http.Cookie{
		Name: nombre, Value: valor, Path: ruta, Expires: vence, MaxAge: int(time.Until(vence).Seconds()),
		HttpOnly: true, Secure: s.o.CookiesSeguras, SameSite: http.SameSiteStrictMode,
	}
}

func (s *Servicio) borrarCookieDeSesion(w http.ResponseWriter) {
	http.SetCookie(w, &http.Cookie{Name: CookieDeSesion, Value: "", Path: "/api/", MaxAge: -1, HttpOnly: true,
		Secure: s.o.CookiesSeguras, SameSite: http.SameSiteStrictMode})
}

// tokenDe lee el token: Authorization Bearer (app) o la cookie (PWA).
func tokenDe(r *http.Request) string {
	if a := r.Header.Get("Authorization"); len(a) > 7 && strings.EqualFold(a[:7], "Bearer ") {
		return strings.TrimSpace(a[7:])
	}
	if c, err := r.Cookie(CookieDeSesion); err == nil {
		return c.Value
	}
	return ""
}

// credencialDeDispositivoDe lee la credencial: la que manda la app en el cuerpo, o la cookie de la PWA.
func credencialDeDispositivoDe(r *http.Request, delCuerpo string) string {
	if delCuerpo != "" {
		return delCuerpo
	}
	if c, err := r.Cookie(CookieDeDispositivo); err == nil {
		return c.Value
	}
	return ""
}

// autenticar valida el token y anota la actividad si anotar es true. Una sesión vencida queda terminada con su motivo.
func (s *Servicio) autenticar(ctx context.Context, r *http.Request, anotar bool) (SesionActiva, *sobre.Error) {
	token := tokenDe(r)
	if token == "" || len(token) > 200 {
		return SesionActiva{}, ErrSesionRequerida
	}
	fila, err := s.q.SessionByToken(ctx, HashDeSecreto(token))
	if errors.Is(err, pgx.ErrNoRows) {
		return SesionActiva{}, ErrSesionRequerida
	}
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo leer la sesión", "error", err)
		return SesionActiva{}, sobre.ErrInesperado
	}
	activa := SesionActiva{
		ID: fila.ID, CuentaID: fila.AccountID, DispositivoID: fila.DeviceID,
		Documento:     Documento{Tipo: fila.DocumentType, Numero: fila.DocumentNumber},
		CooperativaID: fila.TenantID, Inactividad: time.Duration(fila.InactivityMinutes) * time.Minute,
		UltimaActividad: fila.LastActivityAt, Creada: fila.CreatedAt, Vence: fila.ExpiresAt,
	}
	if fila.EndedAt != nil {
		return SesionActiva{}, ErrSesionRequerida
	}
	ahora := s.ahora()
	motivo := ""
	switch {
	case !ahora.Before(activa.Vence):
		motivo = "expired"
	case !ahora.Before(activa.UltimaActividad.Add(activa.Inactividad)):
		motivo = "inactivity"
	case fila.AccountStatus != "active":
		motivo = "revoked"
	}
	if motivo != "" {
		if _, err := s.q.EndSession(ctx, gen.EndSessionParams{ID: activa.ID, At: ahora, EndReason: &motivo}); err != nil {
			s.o.Logger.WarnContext(ctx, "no se pudo terminar la sesión vencida", "error", err)
		}
		return SesionActiva{}, ErrSesionRequerida
	}
	if anotar && ahora.Sub(activa.UltimaActividad) >= anotarActividadCada {
		if err := s.q.TouchSession(ctx, gen.TouchSessionParams{ID: activa.ID, At: ahora}); err != nil {
			s.o.Logger.WarnContext(ctx, "no se pudo anotar la actividad de la sesión", "error", err)
		} else {
			activa.UltimaActividad = ahora
		}
	}
	return activa, nil
}

// ConSesion exige una sesión vigente, anota la actividad y pone la cuenta en el contexto (actor «member»).
func (s *Servicio) ConSesion(siguiente http.Handler) http.Handler {
	return s.conSesion(siguiente, true)
}

func (s *Servicio) conSesion(siguiente http.Handler, anotar bool) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		activa, e := s.autenticar(r.Context(), r, anotar)
		if e != nil {
			if e == ErrSesionRequerida && !PlataformaDe(r).esApp() {
				s.borrarCookieDeSesion(w)
			}
			sobre.Escribir(w, e)
			return
		}
		ctx := context.WithValue(r.Context(), claveSesion{}, activa)
		ctx = contexto.ConCuenta(ctx, activa.CuentaID)
		ctx = contexto.ConActor(ctx, contexto.ActorAsociado)
		siguiente.ServeHTTP(w, r.WithContext(ctx))
	})
}

func recortar(s string, maximo int) *string {
	s = strings.TrimSpace(s)
	if s == "" {
		return nil
	}
	if len(s) > maximo {
		s = s[:maximo]
	}
	return &s
}

// etiquetaDeDispositivo arma un nombre legible desde el agente: «Chrome en Windows».
func etiquetaDeDispositivo(p Plataforma, agente string) string {
	switch p {
	case Android:
		return "App en Android"
	case IOS:
		return "App en iPhone"
	}
	navegador := "Navegador"
	switch {
	case strings.Contains(agente, "Edg/"):
		navegador = "Edge"
	case strings.Contains(agente, "OPR/"):
		navegador = "Opera"
	case strings.Contains(agente, "Firefox/"):
		navegador = "Firefox"
	case strings.Contains(agente, "Chrome/"):
		navegador = "Chrome"
	case strings.Contains(agente, "Safari/"):
		navegador = "Safari"
	}
	sistema := ""
	switch {
	case strings.Contains(agente, "Android"):
		sistema = "Android"
	case strings.Contains(agente, "iPhone"), strings.Contains(agente, "iPad"):
		sistema = "iPhone o iPad"
	case strings.Contains(agente, "Windows"):
		sistema = "Windows"
	case strings.Contains(agente, "Mac OS"):
		sistema = "Mac"
	case strings.Contains(agente, "Linux"):
		sistema = "Linux"
	}
	if sistema == "" {
		return navegador
	}
	return navegador + " en " + sistema
}
