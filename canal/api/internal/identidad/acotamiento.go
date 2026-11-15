package identidad

import (
	"errors"
	"net/http"

	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// ErrSinCooperativa: la ruta es de una cooperativa y la sesión todavía no eligió ninguna.
var ErrSinCooperativa = sobre.Nuevo(http.StatusConflict, "Canal.Cooperativa.SinElegir", "Elige primero con qué cooperativa quieres continuar.")

// Acotada (T071, FR-026, FR-028) envuelve las rutas de una cooperativa: exige sesión con cooperativa elegida y vuelve a
// comprobar en el espejo, en cada petición, que la persona siga habilitada y el canal encendido. Si la cooperativa la
// inhabilitó, la sesión pierde la cooperativa (no se cierra: las demás siguen disponibles) y responde 403. Pone en el
// contexto la cooperativa y el PublicId de la persona en ella.
func (s *Servicio) Acotada(siguiente http.Handler) http.Handler {
	return s.ConSesion(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ctx := r.Context()
		activa, _ := SesionDe(ctx)
		if activa.CooperativaID == nil {
			sobre.Escribir(w, ErrSinCooperativa)
			return
		}
		h, err := s.q.EnablementForSession(ctx, gen.EnablementForSessionParams{TenantID: *activa.CooperativaID,
			DocumentType: activa.Documento.Tipo, DocumentNumber: activa.Documento.Numero})
		if errors.Is(err, pgx.ErrNoRows) {
			if err := s.q.ClearSessionTenant(ctx, activa.ID); err != nil {
				s.o.Logger.WarnContext(ctx, "no se pudo soltar la cooperativa de la sesión", "error", err)
			}
			s.auditar(contexto.ConCooperativa(ctx, contexto.Cooperativa{ID: *activa.CooperativaID}), "Canal.Sesion.CooperativaRetirada",
				activa.ID.String(), nil)
			sobre.Escribir(w, ErrCooperativaNoDisponible)
			return
		}
		if err != nil {
			s.fallo(w, r, "no se pudo comprobar la habilitación", err)
			return
		}
		ctx = contexto.ConCooperativa(ctx, contexto.Cooperativa{ID: *activa.CooperativaID, PublicID: h.TenantPublicID})
		ctx = contexto.ConPersona(ctx, h.PersonPublicID)
		siguiente.ServeHTTP(w, r.WithContext(ctx))
	}))
}
