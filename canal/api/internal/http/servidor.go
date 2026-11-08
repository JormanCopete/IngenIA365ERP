package httpapi

import (
	"context"
	"io/fs"
	"log/slog"
	"net/http"
	"time"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Opciones del servidor.
type Opciones struct {
	Logger *slog.Logger
	// Estaticos es el build de la PWA (raíz = dist). nil si el binario se compiló sin él.
	Estaticos fs.FS
	// LimitePorMinuto y LimiteRafaga: cubeta de fichas por IP para todo menos /salud e /internal.
	LimitePorMinuto int
	LimiteRafaga    int
	// Lista comprueba las dependencias para /salud/lista (la base). nil = siempre lista.
	Lista func(context.Context) error
	// Interno protege /internal/v1 (el verificador del token del ERP). Obligatorio.
	Interno func(http.Handler) http.Handler
}

// Servidor arma las rutas y la cadena de middleware del canal.
//
// Tres superficies en el mismo proceso (contracts/canal-api.md): /api/v1 (PWA y app), /webhooks (pasarela) e
// /internal/v1 (sólo el ERP, con su token). Todo lo demás es la PWA embebida.
type Servidor struct {
	mux       *http.ServeMux
	opciones  Opciones
	limitador *Limitador
}

// Nuevo crea el servidor con las rutas base: /salud, /salud/lista, los comodines con sobre de error de /api,
// /webhooks e /internal, y la PWA.
func Nuevo(o Opciones) *Servidor {
	if o.Logger == nil {
		o.Logger = slog.Default()
	}
	if o.Interno == nil {
		panic("httpapi: falta el middleware de /internal (verificador del token del ERP)")
	}
	s := &Servidor{
		mux:       http.NewServeMux(),
		opciones:  o,
		limitador: NuevoLimitador(o.LimitePorMinuto, o.LimiteRafaga),
	}

	s.mux.HandleFunc("GET /salud", func(w http.ResponseWriter, _ *http.Request) {
		sobre.JSON(w, http.StatusOK, map[string]string{"estado": "ok"})
	})
	s.mux.HandleFunc("GET /salud/lista", s.lista)

	noExiste := http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		sobre.Escribir(w, sobre.ErrRutaInexistente)
	})
	s.mux.Handle("/api/", noExiste)
	s.mux.Handle("/webhooks/", noExiste)
	s.mux.Handle("/internal/", o.Interno(noExiste))
	s.mux.Handle("/", Estaticos(o.Estaticos, o.Logger))
	return s
}

// Manejar registra una ruta pública (/api/v1/… o /webhooks/…) con el patrón de net/http («POST /api/v1/x»).
func (s *Servidor) Manejar(patron string, h http.Handler) {
	s.mux.Handle(patron, h)
}

// ManejarInterno registra una ruta de /internal/v1 protegida por el token del ERP.
func (s *Servidor) ManejarInterno(patron string, h http.Handler) {
	s.mux.Handle(patron, s.opciones.Interno(h))
}

// Handler devuelve el mux envuelto en la cadena de middleware, de afuera hacia adentro: correlación, IP real y
// agente, cabeceras de seguridad, registro de acceso con recuperación de pánicos, límite por IP.
func (s *Servidor) Handler() http.Handler {
	var h http.Handler = s.mux
	h = limitarPorIP(s.limitador, h)
	h = registrarYRecuperar(s.opciones.Logger, h)
	h = cabecerasDeSeguridad(h)
	h = ipYAgente(h)
	h = correlacion(h)
	return h
}

func (s *Servidor) lista(w http.ResponseWriter, r *http.Request) {
	if s.opciones.Lista != nil {
		ctx, cancelar := context.WithTimeout(r.Context(), 2*time.Second)
		defer cancelar()
		if err := s.opciones.Lista(ctx); err != nil {
			s.opciones.Logger.ErrorContext(r.Context(), "el canal no está listo", "error", err)
			sobre.Escribir(w, sobre.Nuevo(http.StatusServiceUnavailable, "Canal.Salud.NoLista",
				"El servicio no está listo: la base del canal no responde."))
			return
		}
	}
	sobre.JSON(w, http.StatusOK, map[string]string{"estado": "ok"})
}

// NuevoServidorHTTP crea el http.Server con tiempos límite y el log de errores del propio net/http en slog.
func NuevoServidorHTTP(direccion string, h http.Handler, logger *slog.Logger) *http.Server {
	return &http.Server{
		Addr:              direccion,
		Handler:           h,
		ReadHeaderTimeout: 10 * time.Second,
		ReadTimeout:       30 * time.Second,
		WriteTimeout:      60 * time.Second,
		IdleTimeout:       120 * time.Second,
		MaxHeaderBytes:    64 << 10,
		ErrorLog:          slog.NewLogLogger(logger.Handler(), slog.LevelWarn),
	}
}
