package conceptos

import (
	"context"
	"encoding/json"
	"errors"
	"log/slog"
	"net/http"
	"strings"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T144 (US13, C6): el simulador y la solicitud de crédito en línea. El canal valida la forma y pasa al ERP con la cooperativa y la
// persona de la sesión; la simulación y las reglas de la línea son las del ERP. La solicitud lleva la llave de idempotencia del
// asociado: repetirla no crea otra.

// CreditoDelERP lo cumple *erp.Cliente.
type CreditoDelERP interface {
	LineasDeCredito(ctx context.Context, cooperativa uuid.UUID) (json.RawMessage, error)
	SimularCredito(ctx context.Context, cooperativa, persona uuid.UUID, p erp.PedidoDeSimulacionDeCredito) (json.RawMessage, error)
	SolicitarCredito(ctx context.Context, cooperativa, persona uuid.UUID, p erp.PedidoDeSolicitudDeCredito) (json.RawMessage, error)
	SolicitudesDeCredito(ctx context.Context, cooperativa, persona uuid.UUID) (json.RawMessage, error)
}

// Errores del crédito en línea.
var (
	ErrCreditoInvalido = sobre.Nuevo(http.StatusBadRequest, "Canal.Credito.Invalido",
		"Elige la línea y escribe el monto en pesos enteros y el número de cuotas.")
	ErrCreditoSinLlave = sobre.Nuevo(http.StatusBadRequest, "Canal.Credito.SinLlave", "Falta la cabecera Idempotency-Key.")
)

// Credito atiende /api/v1/credit/*.
type Credito struct {
	erp    CreditoDelERP
	logger *slog.Logger
}

// NuevoCredito crea el servicio.
func NuevoCredito(e CreditoDelERP, logger *slog.Logger) *Credito {
	if logger == nil {
		logger = slog.Default()
	}
	return &Credito{erp: e, logger: logger}
}

// Registrar pone las rutas, acotadas a la cooperativa elegida.
func (c *Credito) Registrar(m Registrador, acotada func(http.Handler) http.Handler) {
	m.Manejar("GET /api/v1/credit/lines", acotada(http.HandlerFunc(c.lineas)))
	m.Manejar("POST /api/v1/credit/simulate", acotada(http.HandlerFunc(c.simular)))
	m.Manejar("POST /api/v1/credit/applications", acotada(http.HandlerFunc(c.solicitar)))
	m.Manejar("GET /api/v1/credit/applications", acotada(http.HandlerFunc(c.solicitudes)))
}

func (c *Credito) alcance(w http.ResponseWriter, r *http.Request) (uuid.UUID, uuid.UUID, bool) {
	coop, okC := contexto.CooperativaDe(r.Context())
	p, okP := contexto.Persona(r.Context())
	if !okC || !okP || coop.PublicID == uuid.Nil {
		sobre.Escribir(w, sobre.ErrInesperado)
		return uuid.Nil, uuid.Nil, false
	}
	return coop.PublicID, p, true
}

func (c *Credito) lineas(w http.ResponseWriter, r *http.Request) {
	coop, _, ok := c.alcance(w, r)
	if !ok {
		return
	}
	res, err := c.erp.LineasDeCredito(r.Context(), coop)
	c.responder(w, r, map[string]json.RawMessage{"lines": res}, err)
}

func (c *Credito) simular(w http.ResponseWriter, r *http.Request) {
	coop, persona, ok := c.alcance(w, r)
	if !ok {
		return
	}
	var p erp.PedidoDeSimulacionDeCredito
	if !leerCredito(w, r, &p) || p.Linea == uuid.Nil || p.Monto <= 0 || p.Cuotas <= 0 || p.Cuotas > 600 {
		sobre.Escribir(w, ErrCreditoInvalido)
		return
	}
	res, err := c.erp.SimularCredito(r.Context(), coop, persona, p)
	c.responder(w, r, res, err)
}

func (c *Credito) solicitar(w http.ResponseWriter, r *http.Request) {
	coop, persona, ok := c.alcance(w, r)
	if !ok {
		return
	}
	llave := strings.TrimSpace(r.Header.Get("Idempotency-Key"))
	if len(llave) < 8 || len(llave) > 100 {
		sobre.Escribir(w, ErrCreditoSinLlave)
		return
	}
	var p erp.PedidoDeSolicitudDeCredito
	if !leerCredito(w, r, &p) || p.Linea == uuid.Nil || p.Monto <= 0 || p.Cuotas <= 0 || p.Cuotas > 600 || (p.Destino != nil && len(*p.Destino) > 300) {
		sobre.Escribir(w, ErrCreditoInvalido)
		return
	}
	// La llave del ERP: la del asociado atada a la persona y a la cooperativa (otra persona con la misma llave no choca).
	p.Clave = uuid.NewSHA1(uuid.NameSpaceOID, []byte("canal.credito|"+coop.String()+"|"+persona.String()+"|"+llave))
	res, err := c.erp.SolicitarCredito(r.Context(), coop, persona, p)
	c.responder(w, r, res, err)
}

func (c *Credito) solicitudes(w http.ResponseWriter, r *http.Request) {
	coop, persona, ok := c.alcance(w, r)
	if !ok {
		return
	}
	res, err := c.erp.SolicitudesDeCredito(r.Context(), coop, persona)
	c.responder(w, r, map[string]json.RawMessage{"applications": res}, err)
}

// responder: un rechazo de negocio del ERP (línea, usura, montos) pasa con su mensaje; lo demás es «no disponible».
func (c *Credito) responder(w http.ResponseWriter, r *http.Request, valor any, err error) {
	var e *erp.ErrorDelERP
	switch {
	case err == nil:
		w.Header().Set("Cache-Control", "no-store")
		sobre.JSON(w, http.StatusOK, valor)
	case errors.As(err, &e) && e.Codigo == "Channel.Person.NotEnabled":
		sobre.Escribir(w, ErrCooperativaNoDisponible)
	case errors.As(err, &e) && e.Estado >= 400 && e.Estado < 500:
		sobre.Escribir(w, sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Credito."+ultimo(e.Codigo), e.Mensaje))
	default:
		c.logger.ErrorContext(r.Context(), "el ERP no atendió el crédito en línea", "error", err)
		sobre.Escribir(w, ErrEntidadNoDisponible)
	}
}

func ultimo(codigo string) string {
	if i := strings.LastIndex(codigo, "."); i >= 0 {
		return codigo[i+1:]
	}
	return codigo
}

func leerCredito(w http.ResponseWriter, r *http.Request, destino any) bool {
	d := json.NewDecoder(http.MaxBytesReader(w, r.Body, 4<<10))
	d.DisallowUnknownFields()
	return d.Decode(destino) == nil
}
