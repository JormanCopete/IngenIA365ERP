package httpapi

import (
	"context"
	"net/http"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pagos"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// BuscadorDeIntentos lo cumple *pagos.Servicio.
type BuscadorDeIntentos interface {
	BuscarIntentos(ctx context.Context, cooperativa uuid.UUID, tipoDocumento, documento, referencia string) ([]pagos.IntentoInterno, error)
}

// AccionIntentosLeidos audita la lectura de intentos desde el ERP.
const AccionIntentosLeidos = "Canal.Atencion.IntentosLeidos"

// RegistrarIntentosInternos registra GET /internal/v1/tenants/{tenant}/attempts?documentType=&document=&reference= (atención FR-058 y conciliación
// del archivo de la pasarela, T112). Va aparte porque el servicio de pagos se arma después de las demás rutas internas.
func (s *Servidor) RegistrarIntentosInternos(rc RutasInternasDeCooperativas, b BuscadorDeIntentos) {
	if rc.Logger == nil {
		rc.Logger = s.opciones.Logger
	}
	s.ManejarInterno("GET /internal/v1/tenants/{tenant}/attempts", rc.envolver(func(w http.ResponseWriter, r *http.Request, _ uuid.UUID) {
		ctx := r.Context()
		coop, ok := contexto.CooperativaDe(ctx)
		if !ok || coop.ID == uuid.Nil {
			// La cooperativa no está en el directorio del canal: no tiene intentos.
			sobre.JSON(w, http.StatusOK, []pagos.IntentoInterno{})
			return
		}
		q := r.URL.Query()
		intentos, err := b.BuscarIntentos(ctx, coop.ID, q.Get("documentType"), q.Get("document"), q.Get("reference"))
		if err != nil {
			rc.Logger.ErrorContext(ctx, "no se pudieron buscar los intentos", "error", err)
			sobre.Escribir(w, sobre.ErrInesperado)
			return
		}
		if rc.Auditoria != nil {
			if err := rc.Auditoria.Registrar(ctx, AccionIntentosLeidos, coop.PublicID.String(), map[string]any{
				"reference": q.Get("reference"), "byDocument": q.Get("document") != "", "found": len(intentos),
			}); err != nil {
				rc.Logger.ErrorContext(ctx, "no se pudo auditar la lectura de intentos", "error", err)
			}
		}
		sobre.JSON(w, http.StatusOK, intentos)
	}))
}
