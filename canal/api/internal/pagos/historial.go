package pagos

import (
	"encoding/json"
	"net/http"
	"strconv"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T109 (US8, FR-054): el historial de intentos del asociado en la cooperativa elegida, del más reciente al más antiguo, con
// fecha y hora, referencia, autorización o CUS, estado y valor con su moneda. La certificación pide al menos los últimos cinco.

const (
	// HistorialPorDefecto es cuántos intentos se muestran sin ?limit.
	HistorialPorDefecto = 20
	// HistorialMaximo es el tope de ?limit.
	HistorialMaximo = 50
)

// historial atiende GET /api/v1/payments?limit=.
func (s *Servicio) historial(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	coop, _, cuenta, ok := s.alcance(w, r)
	if !ok {
		return
	}
	limite := HistorialPorDefecto
	if v := r.URL.Query().Get("limit"); v != "" {
		n, err := strconv.Atoi(v)
		if err != nil || n < 1 {
			sobre.Escribir(w, ErrPedidoInvalido)
			return
		}
		limite = min(n, HistorialMaximo)
	}
	filas, err := s.q.ListAccountAttempts(ctx, gen.ListAccountAttemptsParams{AccountID: cuenta, TenantID: coop.ID, MaxRows: int32(limite)})
	if err != nil {
		s.fallo(w, r, "no se pudo leer el historial", err)
		return
	}
	soporte := json.RawMessage("{}")
	if t, err := s.q.GetTenantByID(ctx, coop.ID); err == nil && len(t.Support) > 0 {
		soporte = t.Support
	}
	items := make([]Respuesta, 0, len(filas))
	for _, a := range filas {
		items = append(items, s.respuestaSinSoporte(a))
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"items": items, "support": soporte})
}
