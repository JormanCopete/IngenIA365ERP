package httpapi

import (
	"fmt"
	"log/slog"
	"math"
	"net"
	"net/http"
	"regexp"
	"runtime/debug"
	"strconv"
	"strings"
	"time"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// CabeceraCorrelacion viaja en la petición y en la respuesta; el cliente del ERP la reenvía.
const CabeceraCorrelacion = "X-Correlation-Id"

var correlacionValida = regexp.MustCompile(`^[A-Za-z0-9._-]{1,64}$`)

// correlacion toma la X-Correlation-Id entrante si es válida o genera una (UUID v7), y la devuelve en la respuesta.
func correlacion(siguiente http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		id := r.Header.Get(CabeceraCorrelacion)
		if !correlacionValida.MatchString(id) {
			id = nuevaCorrelacion()
		}
		w.Header().Set(CabeceraCorrelacion, id)
		siguiente.ServeHTTP(w, r.WithContext(contexto.ConCorrelacion(r.Context(), id)))
	})
}

func nuevaCorrelacion() string {
	if id, err := uuid.NewV7(); err == nil {
		return id.String()
	}
	return uuid.NewString()
}

// ipYAgente pone en el contexto la IP real del cliente y su agente. Detrás de Cloudflare la IP real llega en
// CF-Connecting-IP (el pod sólo se alcanza por el túnel); sin esa cabecera, la dirección de la conexión.
func ipYAgente(siguiente http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ctx := contexto.ConIP(r.Context(), IPReal(r))
		ctx = contexto.ConAgente(ctx, r.UserAgent())
		siguiente.ServeHTTP(w, r.WithContext(ctx))
	})
}

// IPReal devuelve la IP del cliente: CF-Connecting-IP si es una IP válida, si no la de la conexión.
func IPReal(r *http.Request) string {
	if cf := strings.TrimSpace(r.Header.Get("CF-Connecting-IP")); cf != "" {
		if ip := net.ParseIP(cf); ip != nil {
			return ip.String()
		}
	}
	host, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil {
		return r.RemoteAddr
	}
	return host
}

// Política de contenido: todo del mismo origen; sin marcos, sin plugins, formularios sólo al propio origen.
const politicaDeContenido = "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; " +
	"form-action 'self'; img-src 'self' data:"

func cabecerasDeSeguridad(siguiente http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		h := w.Header()
		h.Set("Content-Security-Policy", politicaDeContenido)
		h.Set("Strict-Transport-Security", "max-age=31536000; includeSubDomains")
		h.Set("X-Content-Type-Options", "nosniff")
		h.Set("X-Frame-Options", "DENY")
		// Ni la ruta ni el host del canal salen hacia la pasarela u otros sitios.
		h.Set("Referrer-Policy", "same-origin")
		h.Set("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()")
		h.Set("Cross-Origin-Opener-Policy", "same-origin")
		siguiente.ServeHTTP(w, r)
	})
}

// respuesta registra el estado y los bytes escritos.
type respuesta struct {
	http.ResponseWriter
	estado  int
	bytes   int64
	escrito bool
}

func (r *respuesta) WriteHeader(estado int) {
	if !r.escrito {
		r.estado = estado
		r.escrito = true
	}
	r.ResponseWriter.WriteHeader(estado)
}

func (r *respuesta) Write(b []byte) (int, error) {
	if !r.escrito {
		r.estado = http.StatusOK
		r.escrito = true
	}
	n, err := r.ResponseWriter.Write(b)
	r.bytes += int64(n)
	return n, err
}

// Unwrap deja que http.ResponseController llegue al ResponseWriter original (Flush, plazos).
func (r *respuesta) Unwrap() http.ResponseWriter { return r.ResponseWriter }

// registrarYRecuperar registra cada petición (sin la cadena de consulta: puede llevar un documento) y convierte
// un pánico en un 500 con el sobre, dejando la pila en el log.
func registrarYRecuperar(logger *slog.Logger, siguiente http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		inicio := time.Now()
		rw := &respuesta{ResponseWriter: w}
		defer func() {
			if v := recover(); v != nil {
				if v == http.ErrAbortHandler {
					panic(v)
				}
				logger.ErrorContext(r.Context(), "pánico atendiendo la petición",
					"panico", fmt.Sprint(v), "pila", string(debug.Stack()), "metodo", r.Method, "ruta", r.URL.Path)
				if !rw.escrito {
					sobre.Escribir(rw, sobre.ErrInesperado)
				}
			}
			nivel := slog.LevelInfo
			switch {
			case rw.estado >= 500:
				nivel = slog.LevelError
			case !esSuperficieDeAPI(r.URL.Path):
				nivel = slog.LevelDebug // sondas y archivos de la PWA
			}
			logger.Log(r.Context(), nivel, "petición",
				"metodo", r.Method, "ruta", r.URL.Path, "estado", rw.estado, "bytes", rw.bytes,
				"duracion_ms", time.Since(inicio).Milliseconds())
		}()
		siguiente.ServeHTTP(rw, r)
	})
}

func esSuperficieDeAPI(ruta string) bool {
	return strings.HasPrefix(ruta, "/api/") || strings.HasPrefix(ruta, "/webhooks/") || strings.HasPrefix(ruta, "/internal/")
}

// limitarPorIP aplica la cubeta de fichas por IP. No limita las sondas (/salud) ni /internal (el ERP llama desde
// pocos pods y la red ya lo restringe).
func limitarPorIP(l *Limitador, siguiente http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ruta := r.URL.Path
		if ruta == "/salud" || strings.HasPrefix(ruta, "/salud/") || strings.HasPrefix(ruta, "/internal/") {
			siguiente.ServeHTTP(w, r)
			return
		}
		if ok, espera := l.Permitir("ip:" + contexto.IP(r.Context())); !ok {
			w.Header().Set("Retry-After", strconv.Itoa(int(math.Ceil(espera.Seconds()))))
			sobre.Escribir(w, sobre.ErrDemasiadasSolicitudes)
			return
		}
		siguiente.ServeHTTP(w, r)
	})
}
