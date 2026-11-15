package httpapi

import (
	"errors"
	"net/http"
	"time"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/atencion"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T120 (US12; FR-058 a FR-060): la atención y los indicadores que pide el ERP. Toda respuesta va acotada a la
// cooperativa del token: una cuenta de una persona que no está habilitada allí no existe para ella (FR-059).

var horaDeColombia = time.FixedZone("COT", -5*60*60)

// RegistrarAtencionInterna registra GET …/accounts, POST …/accounts/{id}/unlock, POST …/accounts/{id}/sessions/revoke
// y GET …/metrics.
func (s *Servidor) RegistrarAtencionInterna(rc RutasInternasDeCooperativas, a *atencion.Servicio) {
	if rc.Logger == nil {
		rc.Logger = s.opciones.Logger
	}
	escribirError := func(w http.ResponseWriter, r *http.Request, err error) {
		var e *sobre.Error
		if errors.As(err, &e) {
			sobre.Escribir(w, e)
			return
		}
		rc.Logger.ErrorContext(r.Context(), "la atención del canal falló", "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
	}
	cooperativa := func(r *http.Request) (uuid.UUID, bool) {
		c, ok := contexto.CooperativaDe(r.Context())
		return c.ID, ok && c.ID != uuid.Nil
	}
	cuenta := func(w http.ResponseWriter, r *http.Request) (uuid.UUID, bool) {
		id, err := uuid.Parse(r.PathValue("id"))
		if err != nil {
			sobre.Escribir(w, ErrIdentificadorInvalido)
			return uuid.Nil, false
		}
		return id, true
	}

	s.ManejarInterno("GET /internal/v1/tenants/{tenant}/accounts", rc.envolver(func(w http.ResponseWriter, r *http.Request, _ uuid.UUID) {
		coop, ok := cooperativa(r)
		if !ok {
			sobre.JSON(w, http.StatusOK, nil)
			return
		}
		q := r.URL.Query()
		c, err := a.BuscarCuenta(r.Context(), coop, q.Get("documentType"), q.Get("document"))
		if err != nil {
			escribirError(w, r, err)
			return
		}
		sobre.JSON(w, http.StatusOK, c)
	}))
	s.ManejarInterno("POST /internal/v1/tenants/{tenant}/accounts/{id}/unlock", rc.envolver(func(w http.ResponseWriter, r *http.Request, _ uuid.UUID) {
		coop, ok := cooperativa(r)
		id, okID := cuenta(w, r)
		if !okID {
			return
		}
		if !ok {
			sobre.Escribir(w, atencion.ErrCuentaNoExiste)
			return
		}
		if err := a.Desbloquear(r.Context(), coop, id); err != nil {
			escribirError(w, r, err)
			return
		}
		sobre.JSON(w, http.StatusOK, map[string]any{"unlocked": true})
	}))
	s.ManejarInterno("POST /internal/v1/tenants/{tenant}/accounts/{id}/sessions/revoke", rc.envolver(func(w http.ResponseWriter, r *http.Request, _ uuid.UUID) {
		coop, ok := cooperativa(r)
		id, okID := cuenta(w, r)
		if !okID {
			return
		}
		if !ok {
			sobre.Escribir(w, atencion.ErrCuentaNoExiste)
			return
		}
		n, err := a.CerrarSesiones(r.Context(), coop, id)
		if err != nil {
			escribirError(w, r, err)
			return
		}
		sobre.JSON(w, http.StatusOK, map[string]any{"closed": n})
	}))
	s.ManejarInterno("GET /internal/v1/tenants/{tenant}/metrics", rc.envolver(func(w http.ResponseWriter, r *http.Request, _ uuid.UUID) {
		q := r.URL.Query()
		desde, err1 := time.ParseInLocation(time.DateOnly, q.Get("from"), horaDeColombia)
		hasta, err2 := time.ParseInLocation(time.DateOnly, q.Get("to"), horaDeColombia)
		if err1 != nil || err2 != nil {
			sobre.Escribir(w, atencion.ErrRangoInvalido)
			return
		}
		coop, ok := cooperativa(r)
		if !ok {
			sobre.JSON(w, http.StatusOK, atencion.Metricas{RechazosPorMotivo: map[string]int64{}})
			return
		}
		m, err := a.Indicadores(r.Context(), coop, desde, hasta.AddDate(0, 0, 1))
		if err != nil {
			escribirError(w, r, err)
			return
		}
		sobre.JSON(w, http.StatusOK, m)
	}))
}
