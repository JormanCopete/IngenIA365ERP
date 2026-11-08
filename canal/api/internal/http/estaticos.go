package httpapi

import (
	"bytes"
	"crypto/sha256"
	"encoding/base64"
	"errors"
	"io"
	"io/fs"
	"log/slog"
	"net/http"
	"path"
	"regexp"
	"strings"
	"sync"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Caché de la PWA (contracts/canal-api.md y docs/operaciones/canal-digital-despliegue.md):
//   - los archivos con huella —los que Vite deja en assets/ como nombre-HUELLA.ext— no cambian nunca: un año,
//     immutable;
//   - todo lo demás (index.html, sw.js, manifiesto, íconos) se revalida siempre: no-cache. Si index.html quedara en
//     caché, tras un despliegue el navegador pediría huellas que ya no existen.
const (
	cacheInmutable = "public, max-age=31536000, immutable"
	cacheRevalidar = "no-cache"
)

var conHuella = regexp.MustCompile(`^assets/.+-[A-Za-z0-9_-]{8,}\.[A-Za-z0-9]+$`)

// Prefijos que nunca son rutas de la PWA: un 404 ahí es un 404, no index.html.
var prefijosReservados = []string{"/api/", "/webhooks/", "/internal/", "/salud/", "/.well-known/"}

// Tipos explícitos: no se depende de la tabla de tipos del sistema operativo.
var tiposDeContenido = map[string]string{
	".html":        "text/html; charset=utf-8",
	".js":          "text/javascript; charset=utf-8",
	".mjs":         "text/javascript; charset=utf-8",
	".css":         "text/css; charset=utf-8",
	".json":        "application/json",
	".webmanifest": "application/manifest+json",
	".svg":         "image/svg+xml",
	".png":         "image/png",
	".ico":         "image/x-icon",
	".webp":        "image/webp",
	".woff2":       "font/woff2",
	".txt":         "text/plain; charset=utf-8",
	".wasm":        "application/wasm",
}

// Estaticos sirve el build embebido de la PWA con el respaldo de SPA: una ruta del navegador sin archivo
// (/pagos/{id}/retorno) devuelve index.html. Con archivos nil (binario sin la PWA) responde 503 con el sobre.
func Estaticos(archivos fs.FS, logger *slog.Logger) http.Handler {
	e := &estaticos{archivos: archivos, logger: logger}
	return e
}

type estaticos struct {
	archivos fs.FS
	logger   *slog.Logger
	etags    sync.Map // nombre → etag
}

var errNoEncontrado = errors.New("no encontrado")

func (e *estaticos) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet && r.Method != http.MethodHead {
		w.Header().Set("Allow", "GET, HEAD")
		sobre.Escribir(w, sobre.ErrMetodoNoAdmitido)
		return
	}
	for _, p := range prefijosReservados {
		if r.URL.Path == strings.TrimSuffix(p, "/") || strings.HasPrefix(r.URL.Path, p) {
			sobre.Escribir(w, sobre.ErrRutaInexistente)
			return
		}
	}
	if e.archivos == nil {
		sobre.Escribir(w, sobre.Nuevo(http.StatusServiceUnavailable, "Canal.Web.NoDisponible",
			"La interfaz del canal no está incluida en esta compilación."))
		return
	}

	nombre := strings.TrimPrefix(path.Clean("/"+r.URL.Path), "/")
	if nombre == "" {
		nombre = "index.html"
	}
	err := e.servir(w, r, nombre)
	if errors.Is(err, errNoEncontrado) {
		if path.Ext(nombre) != "" {
			// Un archivo que no existe (p. ej. una huella de un despliegue anterior) no es una ruta del navegador.
			sobre.Escribir(w, sobre.ErrRutaInexistente)
			return
		}
		err = e.servir(w, r, "index.html")
	}
	if err != nil {
		e.logger.ErrorContext(r.Context(), "no se pudo servir la PWA", "archivo", nombre, "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
	}
}

func (e *estaticos) servir(w http.ResponseWriter, r *http.Request, nombre string) error {
	if !fs.ValidPath(nombre) {
		return errNoEncontrado
	}
	f, err := e.archivos.Open(nombre)
	if errors.Is(err, fs.ErrNotExist) {
		return errNoEncontrado
	}
	if err != nil {
		return err
	}
	defer f.Close()
	info, err := f.Stat()
	if err != nil {
		return err
	}
	if info.IsDir() {
		return errNoEncontrado
	}

	etag, err := e.etag(nombre)
	if err != nil {
		return err
	}
	cuerpo, ok := f.(io.ReadSeeker)
	if !ok {
		contenido, err := io.ReadAll(f)
		if err != nil {
			return err
		}
		cuerpo = bytes.NewReader(contenido)
	}

	h := w.Header()
	if conHuella.MatchString(nombre) {
		h.Set("Cache-Control", cacheInmutable)
	} else {
		h.Set("Cache-Control", cacheRevalidar)
	}
	if tipo, ok := tiposDeContenido[strings.ToLower(path.Ext(nombre))]; ok {
		h.Set("Content-Type", tipo)
	}
	h.Set("ETag", etag)
	http.ServeContent(w, r, nombre, info.ModTime(), cuerpo)
	return nil
}

// etag es la huella SHA-256 del contenido; el binario no cambia mientras corre, así que se calcula una vez.
func (e *estaticos) etag(nombre string) (string, error) {
	if v, ok := e.etags.Load(nombre); ok {
		return v.(string), nil
	}
	contenido, err := fs.ReadFile(e.archivos, nombre)
	if err != nil {
		return "", err
	}
	suma := sha256.Sum256(contenido)
	etag := `"` + base64.RawURLEncoding.EncodeToString(suma[:16]) + `"`
	e.etags.Store(nombre, etag)
	return etag, nil
}
