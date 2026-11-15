package httpapi

import (
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

// T150: una escritura con la cookie de sesión desde otro sitio se rechaza; desde el canal, sin cookie (la app) o en lectura, pasa.
func TestLasEscriturasConCookieDeOtroSitioSeRechazan(t *testing.T) {
	s := Nuevo(Opciones{Logger: slog.New(slog.NewTextHandler(io.Discard, nil)), Interno: func(h http.Handler) http.Handler { return h },
		LimitePorMinuto: 1000, LimiteRafaga: 1000, OrigenesPermitidos: []string{"https://asociados.ejemplo.test"}, CookieDeSesion: "canal_sesion"})
	s.Manejar("POST /api/v1/x", http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) { w.WriteHeader(http.StatusNoContent) }))
	s.Manejar("GET /api/v1/x", http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) { w.WriteHeader(http.StatusNoContent) }))
	pedir := func(metodo, origen string, conCookie bool) int {
		r := httptest.NewRequest(metodo, "/api/v1/x", strings.NewReader("{}"))
		if origen != "" {
			r.Header.Set("Origin", origen)
		}
		if conCookie {
			r.AddCookie(&http.Cookie{Name: "canal_sesion", Value: "x"})
		}
		w := httptest.NewRecorder()
		s.Handler().ServeHTTP(w, r)
		return w.Code
	}
	casos := []struct {
		metodo, origen string
		cookie         bool
		estado         int
	}{
		{http.MethodPost, "https://malicioso.test", true, http.StatusForbidden},
		{http.MethodPost, "https://asociados.ejemplo.test", true, http.StatusNoContent},
		{http.MethodPost, "", true, http.StatusNoContent},
		{http.MethodPost, "https://0.0.0.1", false, http.StatusNoContent},
		{http.MethodGet, "https://malicioso.test", true, http.StatusNoContent},
	}
	for _, c := range casos {
		if got := pedir(c.metodo, c.origen, c.cookie); got != c.estado {
			t.Errorf("%s desde %q con cookie %v: %d, se esperaba %d", c.metodo, c.origen, c.cookie, got, c.estado)
		}
	}
}
