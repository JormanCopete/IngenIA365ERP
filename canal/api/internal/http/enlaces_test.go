package httpapi

import (
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

// T138: Android e iOS verifican la app con estos archivos; sin configurar, no existen.
func TestLosEnlacesDeLaAppSalenDeLaConfiguracion(t *testing.T) {
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	sinAuth := func(h http.Handler) http.Handler { return h }
	pedir := func(s *Servidor, ruta string) (*httptest.ResponseRecorder, any) {
		w := httptest.NewRecorder()
		s.Handler().ServeHTTP(w, httptest.NewRequest(http.MethodGet, ruta, nil))
		var cuerpo any
		_ = json.Unmarshal(w.Body.Bytes(), &cuerpo)
		return w, cuerpo
	}

	vacio := Nuevo(Opciones{Logger: logger, Interno: sinAuth, LimitePorMinuto: 1000, LimiteRafaga: 1000})
	vacio.RegistrarEnlacesDeLaApp(EnlacesDeLaApp{PaqueteAndroid: "com.ingenia365.canal"})
	if w, _ := pedir(vacio, "/.well-known/assetlinks.json"); w.Code != http.StatusNotFound {
		t.Fatalf("sin huellas: %d", w.Code)
	}
	if w, _ := pedir(vacio, "/.well-known/apple-app-site-association"); w.Code != http.StatusNotFound {
		t.Fatalf("sin App ID: %d", w.Code)
	}

	huella := strings.TrimSuffix(strings.Repeat("AB:", 32), ":")
	s := Nuevo(Opciones{Logger: logger, Interno: sinAuth, LimitePorMinuto: 1000, LimiteRafaga: 1000})
	s.RegistrarEnlacesDeLaApp(EnlacesDeLaApp{PaqueteAndroid: "com.ingenia365.canal", HuellasAndroid: []string{huella}, AppIDiOS: "EQUIPO1234.com.ingenia365.canal"})
	w, android := pedir(s, "/.well-known/assetlinks.json")
	if w.Code != http.StatusOK || !strings.HasPrefix(w.Header().Get("Content-Type"), "application/json") {
		t.Fatalf("assetlinks: %d %s", w.Code, w.Header())
	}
	destino := android.([]any)[0].(map[string]any)["target"].(map[string]any)
	if destino["package_name"] != "com.ingenia365.canal" || destino["sha256_cert_fingerprints"].([]any)[0] != huella {
		t.Fatalf("assetlinks: %v", android)
	}
	w, ios := pedir(s, "/.well-known/apple-app-site-association")
	detalle := ios.(map[string]any)["applinks"].(map[string]any)["details"].([]any)[0].(map[string]any)
	if w.Code != http.StatusOK || detalle["appIDs"].([]any)[0] != "EQUIPO1234.com.ingenia365.canal" || len(detalle["components"].([]any)) != len(RutasDeLaApp) {
		t.Fatalf("apple-app-site-association: %d %v", w.Code, ios)
	}
}
