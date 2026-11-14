package httpapi

import (
	"net/http"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T138 (US11; research R14): los archivos con que Android e iOS verifican que la app de tiendas puede abrir los enlaces del canal
// (App Links y Universal Links): /cooperativa, /perfil y el retorno de la pasarela /pagos/{id}/retorno. Salen de la configuración
// del ambiente (el paquete y las huellas del certificado de firma de Android; el App ID de iOS); sin configurar, 404.

// EnlacesDeLaApp es la identidad de la app firmada en este ambiente.
type EnlacesDeLaApp struct {
	PaqueteAndroid string
	HuellasAndroid []string // SHA-256 del certificado de firma, «AA:BB:…»
	AppIDiOS       string   // TEAMID.bundle
}

// RutasDeLaApp son las rutas que abre la app.
var RutasDeLaApp = []string{"/cooperativa*", "/perfil*", "/pagos/*"}

// RegistrarEnlacesDeLaApp sirve /.well-known/assetlinks.json y /.well-known/apple-app-site-association.
func (s *Servidor) RegistrarEnlacesDeLaApp(e EnlacesDeLaApp) {
	s.Manejar("GET /.well-known/assetlinks.json", http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		if e.PaqueteAndroid == "" || len(e.HuellasAndroid) == 0 {
			sobre.Escribir(w, sobre.Nuevo(http.StatusNotFound, "Canal.Ruta.NoExiste", "Recurso no encontrado."))
			return
		}
		w.Header().Set("Cache-Control", "public, max-age=3600")
		sobre.JSON(w, http.StatusOK, []map[string]any{{
			"relation": []string{"delegate_permission/common.handle_all_urls"},
			"target":   map[string]any{"namespace": "android_app", "package_name": e.PaqueteAndroid, "sha256_cert_fingerprints": e.HuellasAndroid},
		}})
	}))
	s.Manejar("GET /.well-known/apple-app-site-association", http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		if e.AppIDiOS == "" {
			sobre.Escribir(w, sobre.Nuevo(http.StatusNotFound, "Canal.Ruta.NoExiste", "Recurso no encontrado."))
			return
		}
		w.Header().Set("Cache-Control", "public, max-age=3600")
		sobre.JSON(w, http.StatusOK, map[string]any{
			"applinks":       map[string]any{"details": []map[string]any{{"appIDs": []string{e.AppIDiOS}, "components": componentes()}}},
			"webcredentials": map[string]any{"apps": []string{e.AppIDiOS}},
		})
	}))
}

func componentes() []map[string]string {
	salida := make([]map[string]string, 0, len(RutasDeLaApp))
	for _, r := range RutasDeLaApp {
		salida = append(salida, map[string]string{"/": r})
	}
	return salida
}
