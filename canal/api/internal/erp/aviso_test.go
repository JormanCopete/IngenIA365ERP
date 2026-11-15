package erp

import (
	"context"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"net/url"
	"sort"
	"strings"
	"testing"
	"time"

	"github.com/google/uuid"
)

// T108: el cuerpo del aviso coincide con contracts/erp-api-de-servicio.md («payment-notices»). El ejemplo del contrato está aquí
// copiado: si alguno de los dos cambia un campo, esta prueba lo dice.
const avisoDelContrato = `{
  "reference": "CFP261205A1B2C3D47",
  "destinationPublicId": "11111111-1111-1111-1111-111111111111",
  "total": 332500, "currency": "COP", "approvedAt": "2026-12-05T10:41:22-05:00",
  "gateway": { "requestId": 733112, "internalReference": 1598634446, "authorization": "123",
               "receipt": "1597317690", "franchise": "PS_PSE", "paymentMethodName": "PSE", "issuerName": "Banco" },
  "items": [{ "conceptKey": "LOAN:22222222-2222-2222-2222-222222222222", "modality": "Due", "amount": 332500, "prepaymentChoice": null }],
  "payer": { "ip": "186.80.28.28", "userAgent": "Navegador", "accountId": "33333333-3333-3333-3333-333333333333" }
}`

// claves devuelve las rutas de todas las claves de un JSON («gateway.requestId», «items[].amount»…).
func claves(t *testing.T, crudo []byte) []string {
	t.Helper()
	var v any
	if err := json.Unmarshal(crudo, &v); err != nil {
		t.Fatal(err)
	}
	var out []string
	var recorrer func(prefijo string, v any)
	recorrer = func(prefijo string, v any) {
		switch x := v.(type) {
		case map[string]any:
			for k, hijo := range x {
				ruta := k
				if prefijo != "" {
					ruta = prefijo + "." + k
				}
				out = append(out, ruta)
				recorrer(ruta, hijo)
			}
		case []any:
			for _, hijo := range x {
				recorrer(prefijo+"[]", hijo)
			}
		}
	}
	recorrer("", v)
	sort.Strings(out)
	return compactar(out)
}

func compactar(s []string) []string {
	var r []string
	for i, v := range s {
		if i == 0 || v != s[i-1] {
			r = append(r, v)
		}
	}
	return r
}

func TestElCuerpoDelAvisoCoincideConElContrato(t *testing.T) {
	var aviso AvisoDePago
	if err := json.Unmarshal([]byte(avisoDelContrato), &aviso); err != nil {
		t.Fatal(err)
	}
	if aviso.Total != 332500 || aviso.Pasarela.RequestID != 733112 || aviso.Items[0].Modalidad != "Due" || aviso.Pagador.IP == nil {
		t.Fatalf("lectura del ejemplo: %+v", aviso)
	}
	serializado, err := json.Marshal(aviso)
	if err != nil {
		t.Fatal(err)
	}
	esperadas, obtenidas := claves(t, []byte(avisoDelContrato)), claves(t, serializado)
	if strings.Join(esperadas, ",") != strings.Join(obtenidas, ",") {
		t.Fatalf("el aviso no coincide con el contrato:\n  contrato: %v\n  canal:    %v", esperadas, obtenidas)
	}
}

func TestAvisarPagoYDescargarElComprobante(t *testing.T) {
	cooperativa, persona, recibo := uuid.New(), uuid.New(), uuid.New()
	var ruta, metodo string
	var cuerpo map[string]any
	erpDePrueba := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ruta, metodo = r.URL.Path, r.Method
		if strings.HasSuffix(r.URL.Path, "/document") {
			w.Header().Set("Content-Type", "application/pdf")
			_, _ = w.Write([]byte("%PDF-1.7"))
			return
		}
		b, _ := io.ReadAll(r.Body)
		_ = json.Unmarshal(b, &cuerpo)
		_, _ = w.Write([]byte(`{"outcome":"InReview","receiptPublicId":"` + recibo.String() + `","receiptNumber":"RL-1","reason":"Sobró"}`))
	}))
	defer erpDePrueba.Close()
	base, _ := url.Parse(erpDePrueba.URL)
	c := NuevoCliente(base, llaveDePrueba(t), "k", erpDePrueba.Client())

	r, err := c.AvisarPago(context.Background(), cooperativa, persona, AvisoDePago{Referencia: "ABC1", Total: 1000, Moneda: "COP",
		Aprobado: time.Date(2026, 12, 5, 15, 0, 0, 0, time.UTC), Items: []ItemDelAviso{{Clave: "ACCOUNT:x", Modalidad: "Due", Valor: 1000}}})
	if err != nil || r.Resultado != "InReview" || r.Recibo == nil || *r.Recibo != recibo {
		t.Fatalf("aviso: %v %+v", err, r)
	}
	if metodo != http.MethodPost || ruta != "/api/canal/v1/tenants/"+cooperativa.String()+"/people/"+persona.String()+"/payment-notices" ||
		cuerpo["reference"] != "ABC1" {
		t.Fatalf("petición: %s %s %v", metodo, ruta, cuerpo)
	}

	pdf, tipo, err := c.Comprobante(context.Background(), cooperativa, persona, recibo)
	if err != nil || tipo != "application/pdf" || string(pdf) != "%PDF-1.7" || !strings.HasSuffix(ruta, "/receipts/"+recibo.String()+"/document") {
		t.Fatalf("comprobante: %v %s %q %s", err, tipo, pdf, ruta)
	}
}
