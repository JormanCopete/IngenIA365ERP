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

// T082 (US3; FR-029 a FR-031; contracts/canal-api.md §A, «Dentro de la cooperativa elegida»): los conceptos y la
// simulación se piden al ERP en cada petición y no se guardan (FR-030). Si el ERP de la cooperativa no responde, el
// asociado ve «información no disponible» (FR-031) y nunca un valor viejo.

// Errores que ve el asociado.
var (
	// ErrEntidadNoDisponible: el ERP de la cooperativa no respondió o apagó el canal (FR-031).
	ErrEntidadNoDisponible = sobre.Nuevo(http.StatusServiceUnavailable, "Canal.Entidad.NoDisponible",
		"La información de tu cooperativa no está disponible en este momento. Intenta de nuevo en unos minutos.")
	// ErrCooperativaNoDisponible: la cooperativa inhabilitó a la persona y el espejo todavía no lo sabía (FR-026).
	ErrCooperativaNoDisponible = sobre.Nuevo(http.StatusForbidden, "Canal.Cooperativa.NoDisponible",
		"Esta cooperativa ya no está disponible para ti en el canal. Comunícate con ella si crees que es un error.")
	ErrPedidoInvalido = sobre.Nuevo(http.StatusBadRequest, "Canal.Pedido.Invalido", "La solicitud no tiene la forma esperada.")
	ErrValorInvalido  = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Concepto.ValorInvalido",
		"El valor debe ser un número entero de pesos mayor que cero.")
)

// Modalidades que el canal conoce (las traduce el ERP a las de sus contratos).
var modalidades = map[string]bool{"Due": true, "Partial": true, "Prepayment": true, "Payoff": true}

// LectorDelERP es lo que el servicio necesita del cliente del ERP.
type LectorDelERP interface {
	Conceptos(ctx context.Context, cooperativa, persona uuid.UUID) (erp.Conceptos, error)
	Simular(ctx context.Context, cooperativa, persona uuid.UUID, p erp.PedidoDeSimulacion) (erp.Simulacion, error)
}

// Registrador es el servidor donde se ponen las rutas.
type Registrador interface {
	Manejar(patron string, h http.Handler)
}

// Servicio atiende las rutas de conceptos.
type Servicio struct {
	erp    LectorDelERP
	logger *slog.Logger
}

// Nuevo crea el servicio.
func Nuevo(lector LectorDelERP, logger *slog.Logger) *Servicio {
	if logger == nil {
		logger = slog.Default()
	}
	return &Servicio{erp: lector, logger: logger}
}

// Registrar pone las rutas. acotada es el envoltorio que exige sesión con cooperativa elegida y deja en el contexto la
// cooperativa y la persona (identidad.Servicio.Acotada).
func (s *Servicio) Registrar(m Registrador, acotada func(http.Handler) http.Handler) {
	m.Manejar("GET /api/v1/payables", acotada(http.HandlerFunc(s.listar)))
	m.Manejar("POST /api/v1/payables/simulate", acotada(http.HandlerFunc(s.simular)))
}

func (s *Servicio) listar(w http.ResponseWriter, r *http.Request) {
	cooperativa, persona, ok := s.alcance(w, r)
	if !ok {
		return
	}
	c, err := s.erp.Conceptos(r.Context(), cooperativa, persona)
	if err != nil {
		s.fallaDelERP(w, r, "el ERP no dio los conceptos", err)
		return
	}
	if c.Items == nil {
		c.Items = []erp.Concepto{}
	}
	if c.Avisos == nil {
		c.Avisos = []string{}
	}
	sobre.JSON(w, http.StatusOK, c)
}

func (s *Servicio) simular(w http.ResponseWriter, r *http.Request) {
	cooperativa, persona, ok := s.alcance(w, r)
	if !ok {
		return
	}
	var p erp.PedidoDeSimulacion
	lector := json.NewDecoder(http.MaxBytesReader(w, r.Body, 4<<10))
	lector.DisallowUnknownFields()
	if err := lector.Decode(&p); err != nil || strings.TrimSpace(p.Clave) == "" || !modalidades[p.Modalidad] {
		sobre.Escribir(w, ErrPedidoInvalido)
		return
	}
	if p.Valor <= 0 {
		sobre.Escribir(w, ErrValorInvalido)
		return
	}
	sim, err := s.erp.Simular(r.Context(), cooperativa, persona, p)
	if err != nil {
		s.fallaDelERP(w, r, "el ERP no simuló el pago", err)
		return
	}
	sobre.JSON(w, http.StatusOK, sim)
}

// alcance lee la cooperativa y la persona que dejó la sesión acotada.
func (s *Servicio) alcance(w http.ResponseWriter, r *http.Request) (uuid.UUID, uuid.UUID, bool) {
	c, okC := contexto.CooperativaDe(r.Context())
	p, okP := contexto.Persona(r.Context())
	if !okC || !okP || c.PublicID == uuid.Nil {
		s.logger.ErrorContext(r.Context(), "ruta de conceptos sin cooperativa ni persona en el contexto")
		sobre.Escribir(w, sobre.ErrInesperado)
		return uuid.Nil, uuid.Nil, false
	}
	return c.PublicID, p, true
}

// fallaDelERP traduce un error del ERP al sobre del canal. Los rechazos de negocio del concepto (no publicado, modalidad,
// valor, rechazo del módulo) pasan con su mensaje, que el ERP ya escribe para el asociado; lo demás es «no disponible».
func (s *Servicio) fallaDelERP(w http.ResponseWriter, r *http.Request, que string, err error) {
	var e *erp.ErrorDelERP
	switch {
	case errors.As(err, &e) && e.Codigo == "Channel.Person.NotEnabled":
		sobre.Escribir(w, ErrCooperativaNoDisponible)
	case errors.As(err, &e) && strings.HasPrefix(e.Codigo, "Channel.Payable."):
		codigo := "Canal.Concepto." + strings.TrimPrefix(e.Codigo, "Channel.Payable.")
		estado := e.Estado
		if estado < 400 || estado >= 500 {
			estado = http.StatusUnprocessableEntity
		}
		respuesta := sobre.Nuevo(estado, codigo, e.Mensaje)
		if len(e.Datos) > 0 && string(e.Datos) != "null" {
			respuesta = respuesta.ConDatos(e.Datos)
		}
		sobre.Escribir(w, respuesta)
	default:
		// Red caída, 5xx, canal apagado (409 Channel.Disabled) o cualquier otra cosa: no se muestra nada viejo (FR-031).
		s.logger.WarnContext(r.Context(), que, "error", err)
		sobre.Escribir(w, ErrEntidadNoDisponible)
	}
}
