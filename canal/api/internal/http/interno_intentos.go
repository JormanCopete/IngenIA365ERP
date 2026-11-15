package httpapi

import (
	"context"
	"errors"
	"net/http"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pagos"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// BuscadorDeIntentos lo cumple *pagos.Servicio.
type BuscadorDeIntentos interface {
	BuscarIntentos(ctx context.Context, cooperativa uuid.UUID, tipoDocumento, documento, referencia string) ([]pagos.IntentoInterno, error)
	Reversar(ctx context.Context, cooperativa uuid.UUID, referencia string, p pagos.PedidoDeReverso) (pagos.ResultadoDelReverso, error)
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

	// T116 (FR-052): el reverso de un pago aprobado, por la pasarela antes del corte o marcado como devuelto por Tesorería.
	s.ManejarInterno("POST /internal/v1/tenants/{tenant}/attempts/{reference}/reverse", rc.envolver(func(w http.ResponseWriter, r *http.Request, _ uuid.UUID) {
		ctx := r.Context()
		coop, ok := contexto.CooperativaDe(ctx)
		if !ok || coop.ID == uuid.Nil {
			sobre.Escribir(w, pagos.ErrNoExiste)
			return
		}
		var pedido pagos.PedidoDeReverso
		if e := leerJSON(w, r, &pedido); e != nil {
			sobre.Escribir(w, e)
			return
		}
		resultado, err := b.Reversar(ctx, coop.ID, r.PathValue("reference"), pedido)
		var e *sobre.Error
		switch {
		case errors.As(err, &e):
			sobre.Escribir(w, e)
		case err != nil:
			rc.Logger.ErrorContext(ctx, "no se pudo reversar el intento", "error", err)
			sobre.Escribir(w, sobre.ErrInesperado)
		default:
			sobre.JSON(w, http.StatusOK, resultado)
		}
	}))
}
