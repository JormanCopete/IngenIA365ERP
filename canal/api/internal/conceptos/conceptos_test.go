package conceptos

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
)

type erpFalso struct {
	conceptos erp.Conceptos
	sim       erp.Simulacion
	err       error
	pedidos   []erp.PedidoDeSimulacion
	llamadas  int
	coop, per uuid.UUID
}

func (f *erpFalso) Conceptos(_ context.Context, c, p uuid.UUID) (erp.Conceptos, error) {
	f.llamadas++
	f.coop, f.per = c, p
	return f.conceptos, f.err
}

func (f *erpFalso) Simular(_ context.Context, c, p uuid.UUID, pedido erp.PedidoDeSimulacion) (erp.Simulacion, error) {
	f.llamadas++
	f.coop, f.per = c, p
	f.pedidos = append(f.pedidos, pedido)
	return f.sim, f.err
}

func (f *erpFalso) TextosLegales(_ context.Context, c uuid.UUID) ([]erp.TextoLegal, error) {
	f.llamadas++
	f.coop = c
	return []erp.TextoLegal{
		{Tipo: "Terms", Version: 3, Contenido: "Términos"},
		{Tipo: "DataPolicy", Version: 1, Contenido: "Política"},
		{Tipo: "Faq", Version: 2, Contenido: "Preguntas de la cooperativa"},
		{Tipo: "GatewayFaq", Version: 1, Contenido: "Preguntas de la pasarela"},
	}, f.err
}

type mux struct{ *http.ServeMux }

func (m mux) Manejar(patron string, h http.Handler) { m.Handle(patron, h) }

var (
	cooperativa = uuid.New()
	persona     = uuid.New()
)

// acotadaFalsa hace lo que Acotada deja en el contexto: la cooperativa y la persona.
func acotadaFalsa(h http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ctx := contexto.ConCooperativa(r.Context(), contexto.Cooperativa{ID: uuid.New(), PublicID: cooperativa})
		ctx = contexto.ConPersona(ctx, persona)
		h.ServeHTTP(w, r.WithContext(ctx))
	})
}

func montar(f *erpFalso) http.Handler {
	m := mux{http.NewServeMux()}
	Nuevo(f, slog.New(slog.NewTextHandler(io.Discard, nil))).Registrar(m, acotadaFalsa)
	return m
}

func pedir(t *testing.T, h http.Handler, metodo, ruta, cuerpo string) (*httptest.ResponseRecorder, map[string]any) {
	t.Helper()
	var lector io.Reader
	if cuerpo != "" {
		lector = strings.NewReader(cuerpo)
	}
	w := httptest.NewRecorder()
	h.ServeHTTP(w, httptest.NewRequest(metodo, ruta, lector))
	var r map[string]any
	if err := json.Unmarshal(w.Body.Bytes(), &r); err != nil {
		t.Fatalf("respuesta no es JSON: %q", w.Body.String())
	}
	return w, r
}

func TestLosConceptosSePidenAlERPConLaCooperativaYLaPersonaDeLaSesion(t *testing.T) {
	proxima := "2026-12-15"
	f := &erpFalso{conceptos: erp.Conceptos{Fecha: "2026-12-05", Items: []erp.Concepto{{
		Clave: "LOAN:" + uuid.NewString(), Tipo: "Loan", ExigibleHoy: json.Number("332500.00"), ProximaFecha: &proxima, Pagable: true,
	}}}}
	w, r := pedir(t, montar(f), http.MethodGet, "/api/v1/payables", "")

	if w.Code != http.StatusOK {
		t.Fatalf("estado %d: %s", w.Code, w.Body.String())
	}
	if f.coop != cooperativa || f.per != persona {
		t.Fatalf("pidió con %s/%s", f.coop, f.per)
	}
	if w.Header().Get("Cache-Control") != "no-store" {
		t.Fatalf("la respuesta no debe guardarse en caché (FR-030): %q", w.Header().Get("Cache-Control"))
	}
	items := r["items"].([]any)
	primero := items[0].(map[string]any)
	if primero["dueToday"] != 332500.0 || primero["nextDueDate"] != "2026-12-15" {
		t.Fatalf("valores alterados: %v", primero)
	}
	if avisos, ok := r["notices"].([]any); !ok || len(avisos) != 0 {
		t.Fatalf("los avisos deben ser una lista vacía, no nulos: %v", r["notices"])
	}
}

func TestConElERPCaidoNoMuestraNadaViejo(t *testing.T) {
	for nombre, err := range map[string]error{
		"red":           fmt.Errorf("%w: conexión rehusada", erp.ErrERPNoDisponible),
		"canal apagado": &erp.ErrorDelERP{Estado: http.StatusConflict, Codigo: "Channel.Disabled", Mensaje: "apagado"},
		"otro error":    errors.New("cualquier cosa"),
	} {
		t.Run(nombre, func(t *testing.T) {
			w, r := pedir(t, montar(&erpFalso{err: err}), http.MethodGet, "/api/v1/payables", "")
			if w.Code != http.StatusServiceUnavailable || r["code"] != "Canal.Entidad.NoDisponible" {
				t.Fatalf("estado %d, código %v", w.Code, r["code"])
			}
		})
	}
}

func TestPersonaInhabilitadaEnElERPDejaDeVerLaCooperativa(t *testing.T) {
	f := &erpFalso{err: &erp.ErrorDelERP{Estado: http.StatusNotFound, Codigo: "Channel.Person.NotEnabled", Mensaje: "no"}}
	w, r := pedir(t, montar(f), http.MethodGet, "/api/v1/payables", "")
	if w.Code != http.StatusForbidden || r["code"] != "Canal.Cooperativa.NoDisponible" {
		t.Fatalf("estado %d, código %v", w.Code, r["code"])
	}
}

func TestLaSimulacionValidaElPedidoAntesDeLlamarAlERP(t *testing.T) {
	casos := map[string]struct {
		cuerpo string
		estado int
		codigo string
	}{
		"sin clave":           {`{"modality":"Due","amount":1000}`, http.StatusBadRequest, "Canal.Pedido.Invalido"},
		"modalidad rara":      {`{"conceptKey":"LOAN:x","modality":"Lunar","amount":1000}`, http.StatusBadRequest, "Canal.Pedido.Invalido"},
		"campo desconocido":   {`{"conceptKey":"LOAN:x","modality":"Due","amount":1000,"cuenta":"1"}`, http.StatusBadRequest, "Canal.Pedido.Invalido"},
		"valor con decimales": {`{"conceptKey":"LOAN:x","modality":"Due","amount":1000.5}`, http.StatusBadRequest, "Canal.Pedido.Invalido"},
		"valor cero":          {`{"conceptKey":"LOAN:x","modality":"Due","amount":0}`, http.StatusUnprocessableEntity, "Canal.Concepto.ValorInvalido"},
	}
	for nombre, c := range casos {
		t.Run(nombre, func(t *testing.T) {
			f := &erpFalso{}
			w, r := pedir(t, montar(f), http.MethodPost, "/api/v1/payables/simulate", c.cuerpo)
			if w.Code != c.estado || r["code"] != c.codigo {
				t.Fatalf("estado %d, código %v", w.Code, r["code"])
			}
			if f.llamadas != 0 {
				t.Fatal("no debía llamar al ERP")
			}
		})
	}
}

func TestLaSimulacionPasaAlERPYDevuelveSuRespuesta(t *testing.T) {
	plazo := "ReduceTerm"
	f := &erpFalso{sim: erp.Simulacion{Clave: "LOAN:1", Modalidad: "Prepayment", Valor: json.Number("1000000"), Admitida: true,
		Alternativas: []erp.AlternativaSimulada{{Eleccion: "ReduceTerm"}, {Eleccion: "ReduceInstallment"}}}}
	w, r := pedir(t, montar(f), http.MethodPost, "/api/v1/payables/simulate",
		`{"conceptKey":"LOAN:1","modality":"Prepayment","amount":1000000,"prepaymentChoice":"ReduceTerm"}`)
	if w.Code != http.StatusOK {
		t.Fatalf("estado %d: %s", w.Code, w.Body.String())
	}
	if len(f.pedidos) != 1 || f.pedidos[0].Valor != 1_000_000 || *f.pedidos[0].Eleccion != plazo {
		t.Fatalf("pedido al ERP: %+v", f.pedidos)
	}
	if len(r["alternatives"].([]any)) != 2 {
		t.Fatalf("faltan las alternativas: %v", r)
	}
}

func TestLosRechazosDelConceptoPasanConSuMensajeYSusDatos(t *testing.T) {
	f := &erpFalso{err: &erp.ErrorDelERP{Estado: http.StatusUnprocessableEntity, Codigo: "Channel.Payable.AmountInvalid",
		Mensaje: "El valor mínimo de un pago parcial es 20.000.", Datos: json.RawMessage(`{"minimum":20000}`)}}
	w, r := pedir(t, montar(f), http.MethodPost, "/api/v1/payables/simulate", `{"conceptKey":"LOAN:1","modality":"Partial","amount":1000}`)
	if w.Code != http.StatusUnprocessableEntity || r["code"] != "Canal.Concepto.AmountInvalid" {
		t.Fatalf("estado %d, código %v", w.Code, r["code"])
	}
	if r["message"] != "El valor mínimo de un pago parcial es 20.000." || r["data"].(map[string]any)["minimum"] != 20000.0 {
		t.Fatalf("respuesta: %v", r)
	}

	f.err = &erp.ErrorDelERP{Estado: http.StatusNotFound, Codigo: "Channel.Payable.NotFound", Mensaje: "Ese concepto no está disponible en el canal."}
	w, r = pedir(t, montar(f), http.MethodPost, "/api/v1/payables/simulate", `{"conceptKey":"LOAN:1","modality":"Due","amount":1000}`)
	if w.Code != http.StatusNotFound || r["code"] != "Canal.Concepto.NotFound" {
		t.Fatalf("estado %d, código %v", w.Code, r["code"])
	}
}

func TestTextosLegalesDeLaCooperativa(t *testing.T) {
	f := &erpFalso{}
	w, r := pedir(t, montar(f), http.MethodGet, "/api/v1/legal/faq", "")
	textos, _ := r["texts"].([]any)
	if w.Code != http.StatusOK || len(textos) != 2 || f.coop != cooperativa {
		t.Fatalf("preguntas: %d %v", w.Code, r)
	}
	_, r = pedir(t, montar(f), http.MethodGet, "/api/v1/legal/terms", "")
	if textos, _ = r["texts"].([]any); len(textos) != 1 || textos[0].(map[string]any)["version"] != 3.0 {
		t.Fatalf("términos: %v", r)
	}
	if w, _ = pedir(t, montar(f), http.MethodGet, "/api/v1/legal/otra", ""); w.Code != http.StatusBadRequest {
		t.Fatalf("tipo desconocido: %d", w.Code)
	}
}
