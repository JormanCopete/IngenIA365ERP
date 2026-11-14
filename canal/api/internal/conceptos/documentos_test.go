package conceptos

import (
	"context"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
)

type documentosFalsos struct {
	coop, per uuid.UUID
	pedido    erp.PedidoDeDocumento
	err       error
}

func (f *documentosFalsos) Documentos(_ context.Context, c, p uuid.UUID) ([]erp.Documento, error) {
	f.coop, f.per = c, p
	return []erp.Documento{{Tipo: "ContributionCertificate", Nombre: "Certificado de aportes", Parametro: "none"}}, f.err
}

func (f *documentosFalsos) EmitirDocumento(_ context.Context, c, p uuid.UUID, pedido erp.PedidoDeDocumento) ([]byte, string, error) {
	f.coop, f.per, f.pedido = c, p, pedido
	if f.err != nil {
		return nil, "", f.err
	}
	return []byte("%PDF-1.7 prueba"), "application/pdf", nil
}

func montarDocumentos(f *documentosFalsos) http.Handler {
	m := mux{http.NewServeMux()}
	NuevosDocumentos(f, slog.New(slog.NewTextHandler(io.Discard, nil))).Registrar(m, acotadaFalsa)
	return m
}

// T125 (FR-055): el canal pide al ERP los documentos y el PDF con la cooperativa y la persona de la sesión, y lo entrega tal cual.
func TestLosDocumentosSePidenAlERPConLaPersonaDeLaSesion(t *testing.T) {
	f := &documentosFalsos{}
	h := montarDocumentos(f)
	w, r := pedir(t, h, http.MethodGet, "/api/v1/documents", "")
	if w.Code != http.StatusOK || f.coop != cooperativa || f.per != persona || len(r["documents"].([]any)) != 1 {
		t.Fatalf("lista: %d %v", w.Code, r)
	}

	rec := httptest.NewRecorder()
	h.ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/api/v1/documents",
		strings.NewReader(`{"kind":"Statement","productPublicId":"`+uuid.NewString()+`","from":"2026-09-01","to":"2026-11-30"}`)))
	if rec.Code != http.StatusOK || rec.Header().Get("Content-Type") != "application/pdf" || !strings.HasPrefix(rec.Body.String(), "%PDF") ||
		f.pedido.Tipo != "Statement" || *f.pedido.Desde != "2026-09-01" || f.per != persona {
		t.Fatalf("PDF: %d %s %+v", rec.Code, rec.Header(), f.pedido)
	}

	if w, r := pedir(t, h, http.MethodPost, "/api/v1/documents", `{"kind":"Statement","from":"01/09/2026"}`); w.Code != http.StatusBadRequest ||
		r["code"] != "Canal.Documento.Invalido" {
		t.Fatalf("fecha mal escrita: %d %v", w.Code, r)
	}
	f.err = &erp.ErrorDelERP{Estado: http.StatusNotFound, Codigo: "Channel.Document.NotAvailable", Mensaje: "no"}
	if w, r := pedir(t, h, http.MethodPost, "/api/v1/documents", `{"kind":"AccountStatement"}`); w.Code != http.StatusNotFound ||
		r["code"] != "Canal.Documento.NoDisponible" {
		t.Fatalf("no publicado: %d %v", w.Code, r)
	}
	f.err = &erp.ErrorDelERP{Estado: http.StatusUnprocessableEntity, Codigo: "Lending.Document.TaxYearNotClosed", Mensaje: "El año 2026 no ha terminado."}
	if w, r := pedir(t, h, http.MethodPost, "/api/v1/documents", `{"kind":"TaxCertificate","year":2026}`); w.Code != http.StatusUnprocessableEntity ||
		r["code"] != "Canal.Documento.TaxYearNotClosed" || r["message"] != "El año 2026 no ha terminado." {
		t.Fatalf("año sin cerrar: %d %v", w.Code, r)
	}
}
