package conceptos

import (
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
)

type creditoFalso struct {
	coop, per uuid.UUID
	solicitud erp.PedidoDeSolicitudDeCredito
	err       error
}

func (f *creditoFalso) LineasDeCredito(_ context.Context, c uuid.UUID) (json.RawMessage, error) {
	f.coop = c
	return json.RawMessage(`[{"publicId":"x","code":"CON","name":"Consumo"}]`), nil
}

func (f *creditoFalso) SimularCredito(_ context.Context, c, p uuid.UUID, _ erp.PedidoDeSimulacionDeCredito) (json.RawMessage, error) {
	f.coop, f.per = c, p
	return json.RawMessage(`{"installmentCount":24}`), f.err
}

func (f *creditoFalso) SolicitarCredito(_ context.Context, c, p uuid.UUID, s erp.PedidoDeSolicitudDeCredito) (json.RawMessage, error) {
	f.coop, f.per, f.solicitud = c, p, s
	return json.RawMessage(`{"status":"Draft"}`), f.err
}

func (f *creditoFalso) SolicitudesDeCredito(context.Context, uuid.UUID, uuid.UUID) (json.RawMessage, error) {
	return json.RawMessage(`[]`), nil
}

func montarCredito(f *creditoFalso) http.Handler {
	m := mux{http.NewServeMux()}
	NuevoCredito(f, slog.New(slog.NewTextHandler(io.Discard, nil))).Registrar(m, acotadaFalsa)
	return m
}

// T144 (US13): la simulación y la solicitud van al ERP con la cooperativa y la persona de la sesión; la llave del ERP sale de la del asociado
// atada a la persona (la misma llave repetida da la misma).
func TestElCreditoEnLineaVaAlERPConLaPersonaDeLaSesion(t *testing.T) {
	f := &creditoFalso{}
	h := montarCredito(f)
	linea := uuid.NewString()
	if w, r := pedir(t, h, http.MethodGet, "/api/v1/credit/lines", ""); w.Code != http.StatusOK || len(r["lines"].([]any)) != 1 || f.coop != cooperativa {
		t.Fatalf("líneas: %d %v", w.Code, r)
	}
	if w, _ := pedir(t, h, http.MethodPost, "/api/v1/credit/simulate", `{"creditLinePublicId":"`+linea+`","amount":5000000,"installments":24}`); w.Code != http.StatusOK ||
		f.per != persona {
		t.Fatalf("simular: %d", w.Code)
	}
	if w, r := pedir(t, h, http.MethodPost, "/api/v1/credit/simulate", `{"creditLinePublicId":"`+linea+`","amount":0,"installments":24}`); w.Code != http.StatusBadRequest ||
		r["code"] != "Canal.Credito.Invalido" {
		t.Fatalf("monto cero: %d %v", w.Code, r)
	}

	solicitar := func(llave string) *httptest.ResponseRecorder {
		req := httptest.NewRequest(http.MethodPost, "/api/v1/credit/applications",
			strings.NewReader(`{"creditLinePublicId":"`+linea+`","amount":5000000,"installments":24,"purpose":"Estudio"}`))
		req.Header.Set("Content-Type", "application/json")
		if llave != "" {
			req.Header.Set("Idempotency-Key", llave)
		}
		w := httptest.NewRecorder()
		h.ServeHTTP(w, req)
		return w
	}
	if w := solicitar(""); w.Code != http.StatusBadRequest {
		t.Fatalf("sin llave: %d", w.Code)
	}
	if w := solicitar("llave-1234"); w.Code != http.StatusOK {
		t.Fatalf("solicitud: %d %s", w.Code, w.Body.String())
	}
	primera := f.solicitud.Clave
	_ = solicitar("llave-1234")
	if f.solicitud.Clave != primera || primera == uuid.Nil || *f.solicitud.Destino != "Estudio" {
		t.Fatalf("la misma llave debe dar la misma clave: %v %v", primera, f.solicitud.Clave)
	}

	f.err = &erp.ErrorDelERP{Estado: http.StatusUnprocessableEntity, Codigo: "Lending.Usury.Exceeded", Mensaje: "La tasa supera la usura."}
	if w, r := pedir(t, h, http.MethodPost, "/api/v1/credit/simulate", `{"creditLinePublicId":"`+linea+`","amount":5000000,"installments":24}`); w.Code != 422 ||
		r["code"] != "Canal.Credito.Exceeded" || r["message"] != "La tasa supera la usura." {
		t.Fatalf("rechazo del ERP: %d %v", w.Code, r)
	}
}
