package conceptos

import (
	"context"
	"encoding/json"
	"errors"
	"log/slog"
	"net/http"
	"strings"
	"time"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T125 (US9; FR-055): los extractos y certificados que la cooperativa publica. El canal no los guarda ni los arma: los
// pide al ERP, que los genera en el momento con el mismo motor de la oficina, y los entrega tal cual.

// DocumentosDelERP lo cumple *erp.Cliente.
type DocumentosDelERP interface {
	Documentos(ctx context.Context, cooperativa, persona uuid.UUID) ([]erp.Documento, error)
	EmitirDocumento(ctx context.Context, cooperativa, persona uuid.UUID, p erp.PedidoDeDocumento) ([]byte, string, error)
}

// Errores de los documentos.
var (
	ErrDocumentoNoDisponible = sobre.Nuevo(http.StatusNotFound, "Canal.Documento.NoDisponible",
		"Ese documento no está disponible para ti en el canal.")
	ErrDocumentoInvalido = sobre.Nuevo(http.StatusBadRequest, "Canal.Documento.Invalido",
		"Elige el documento y, si lo pide, el período (desde y hasta) o el año.")
)

// Documentos atiende GET y POST /api/v1/documents.
type Documentos struct {
	erp    DocumentosDelERP
	logger *slog.Logger
}

// NuevosDocumentos crea el servicio.
func NuevosDocumentos(e DocumentosDelERP, logger *slog.Logger) *Documentos {
	if logger == nil {
		logger = slog.Default()
	}
	return &Documentos{erp: e, logger: logger}
}

// Registrar pone las rutas, acotadas a la cooperativa elegida.
func (d *Documentos) Registrar(m Registrador, acotada func(http.Handler) http.Handler) {
	m.Manejar("GET /api/v1/documents", acotada(http.HandlerFunc(d.listar)))
	m.Manejar("POST /api/v1/documents", acotada(http.HandlerFunc(d.emitir)))
}

func (d *Documentos) alcance(w http.ResponseWriter, r *http.Request) (uuid.UUID, uuid.UUID, bool) {
	c, okC := contexto.CooperativaDe(r.Context())
	p, okP := contexto.Persona(r.Context())
	if !okC || !okP || c.PublicID == uuid.Nil {
		d.logger.ErrorContext(r.Context(), "ruta de documentos sin cooperativa ni persona en el contexto")
		sobre.Escribir(w, sobre.ErrInesperado)
		return uuid.Nil, uuid.Nil, false
	}
	return c.PublicID, p, true
}

func (d *Documentos) listar(w http.ResponseWriter, r *http.Request) {
	coop, persona, ok := d.alcance(w, r)
	if !ok {
		return
	}
	docs, err := d.erp.Documentos(r.Context(), coop, persona)
	if err != nil {
		d.falla(w, r, err)
		return
	}
	if docs == nil {
		docs = []erp.Documento{}
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"documents": docs})
}

func (d *Documentos) emitir(w http.ResponseWriter, r *http.Request) {
	coop, persona, ok := d.alcance(w, r)
	if !ok {
		return
	}
	var p erp.PedidoDeDocumento
	if !strings.HasPrefix(strings.ToLower(r.Header.Get("Content-Type")), "application/json") {
		sobre.Escribir(w, ErrDocumentoInvalido)
		return
	}
	lector := json.NewDecoder(http.MaxBytesReader(w, r.Body, 2<<10))
	lector.DisallowUnknownFields()
	if err := lector.Decode(&p); err != nil || strings.TrimSpace(p.Tipo) == "" || !fechaValida(p.Desde) || !fechaValida(p.Hasta) {
		sobre.Escribir(w, ErrDocumentoInvalido)
		return
	}
	pdf, tipo, err := d.erp.EmitirDocumento(r.Context(), coop, persona, p)
	if err != nil {
		d.falla(w, r, err)
		return
	}
	if tipo == "" {
		tipo = "application/pdf"
	}
	w.Header().Set("Content-Type", tipo)
	w.Header().Set("Content-Disposition", `attachment; filename="documento.pdf"`)
	w.Header().Set("Cache-Control", "no-store")
	w.WriteHeader(http.StatusOK)
	_, _ = w.Write(pdf)
}

func fechaValida(f *string) bool {
	if f == nil {
		return true
	}
	_, err := time.Parse(time.DateOnly, *f)
	return err == nil
}

// falla: no disponible → 404; un rechazo de negocio de Cartera (período, año, firmante) pasa con su mensaje; lo demás es
// «la cooperativa no está disponible».
func (d *Documentos) falla(w http.ResponseWriter, r *http.Request, err error) {
	var e *erp.ErrorDelERP
	switch {
	case errors.As(err, &e) && e.Codigo == "Channel.Person.NotEnabled":
		sobre.Escribir(w, ErrCooperativaNoDisponible)
	case errors.As(err, &e) && e.Codigo == "Channel.Document.NotAvailable":
		sobre.Escribir(w, ErrDocumentoNoDisponible)
	case errors.As(err, &e) && strings.HasPrefix(e.Codigo, "Lending.Document.") && e.Estado < 500:
		sobre.Escribir(w, sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Documento."+strings.TrimPrefix(e.Codigo, "Lending.Document."), e.Mensaje))
	default:
		d.logger.ErrorContext(r.Context(), "el ERP no entregó el documento", "error", err)
		sobre.Escribir(w, ErrEntidadNoDisponible)
	}
}
