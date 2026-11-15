package placetopay

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
)

// T087: vectores calculados fuera de Go (Python hashlib) y respuestas grabadas con la forma del ambiente de pruebas,
// sin credenciales ni datos reales.

const secretoDePrueba = "SecretoDePrueba123"

var relojFijo = time.Date(2026, 11, 9, 15, 4, 5, 0, time.UTC)

func nueva(h *http.Client) *Pasarela {
	p := Nueva(h)
	p.ahora = func() time.Time { return relojFijo }
	p.nonce = func() ([]byte, error) {
		b := make([]byte, 16)
		for i := range b {
			b[i] = byte(i)
		}
		return b, nil
	}
	return p
}

func sitio(base string) pasarela.Sitio {
	return pasarela.Sitio{ID: uuid.New(), URLBase: base, Login: "loginDePrueba", Secreto: pasarela.Secreto(secretoDePrueba),
		MediosDePago: []string{"pse", "visa"}}
}

func TestTranKeyCoincideConElVector(t *testing.T) {
	a, err := nueva(nil).autenticacion(sitio("https://x.test"))
	if err != nil {
		t.Fatal(err)
	}
	if a.Seed != "2026-11-09T15:04:05Z" || a.Nonce != "AAECAwQFBgcICQoLDA0ODw==" {
		t.Fatalf("semilla o nonce: %+v", a)
	}
	if a.TranKey != "QVYVkYNQE0rkKwY9vjVjRb14q3sPdAraTT+jSck/J5I=" {
		t.Fatalf("tranKey = %s", a.TranKey)
	}
}

func TestFirmaDeLaNotificacion(t *testing.T) {
	const fecha = "2026-11-09T10:20:30-05:00"
	if f := Firma(123456, "APPROVED", fecha, secretoDePrueba, true); f != "sha256:2268b99718158d8b5e83b3a314236b6dbf665a59f65a90b1484403264c9e67b6" {
		t.Fatalf("sha256 = %s", f)
	}
	if f := Firma(123456, "APPROVED", fecha, secretoDePrueba, false); f != "3c5b1a8f3788388c1bc2aae0a9be8d7c34f88867" {
		t.Fatalf("sha1 = %s", f)
	}

	p, s := nueva(nil), sitio("https://x.test")
	cuerpo := func(firma string) []byte {
		b, _ := json.Marshal(map[string]any{
			"status":    map[string]any{"status": "APPROVED", "reason": "00", "message": "Aprobada", "date": fecha},
			"requestId": 123456, "reference": "ABC2611090000000A1", "signature": firma,
		})
		return b
	}
	for _, f := range []string{Firma(123456, "APPROVED", fecha, secretoDePrueba, true), Firma(123456, "APPROVED", fecha, secretoDePrueba, false)} {
		n, err := p.VerificarNotificacion(s, cuerpo(f))
		if err != nil || n.RequestID != 123456 || n.Estado != "APPROVED" || n.Referencia != "ABC2611090000000A1" {
			t.Fatalf("firma válida rechazada (%s): %v %+v", f, err, n)
		}
	}
	for _, f := range []string{"", "sha256:00", Firma(123456, "APPROVED", fecha, "otro", true), Firma(123456, "REJECTED", fecha, secretoDePrueba, true)} {
		if _, err := p.VerificarNotificacion(s, cuerpo(f)); !errors.Is(err, pasarela.ErrFirmaInvalida) {
			t.Fatalf("firma inválida aceptada: %q", f)
		}
	}
	if _, err := p.VerificarNotificacion(s, []byte("no es json")); !errors.Is(err, pasarela.ErrFirmaInvalida) {
		t.Fatal("cuerpo ilegible aceptado")
	}
}

func consulta(t *testing.T, crudo string) respuestaDeConsulta {
	t.Helper()
	var r respuestaDeConsulta
	if err := json.Unmarshal([]byte(crudo), &r); err != nil {
		t.Fatal(err)
	}
	return r
}

// Respuesta grabada (forma del ambiente de pruebas, datos inventados) de una sesión aprobada por PSE.
const aprobadaPSE = `{
  "requestId": 98765,
  "status": {"status": "APPROVED", "reason": "00", "message": "La petición ha sido aprobada exitosamente", "date": "2026-11-09T10:21:00-05:00"},
  "request": {"locale": "es_CO", "buyer": {"document": "1000000001", "documentType": "CC", "name": "Ana", "email": "ana@ejemplo.test"},
    "payment": {"reference": "ABC2611090000000A1", "amount": {"currency": "COP", "total": 332500}}, "expiration": "2026-11-09T10:35:00-05:00"},
  "payment": [
    {"status": {"status": "REJECTED", "reason": "05", "message": "Rechazada", "date": "2026-11-09T10:19:00-05:00"},
     "internalReference": 1, "paymentMethod": "visa", "paymentMethodName": "Visa", "authorization": "000000", "receipt": "1",
     "processorFields": [{"keyword": "lastDigits", "value": "1111"}, {"keyword": "bin", "value": "411111"}]},
    {"status": {"status": "APPROVED", "reason": "00", "message": "Aprobada", "date": "2026-11-09T10:20:30-05:00"},
     "internalReference": 1597317, "paymentMethod": "pse", "paymentMethodName": "PSE", "issuerName": "BANCO DE PRUEBAS",
     "authorization": "3324451", "reference": "ABC2611090000000A1", "receipt": "1597317690", "franchise": "_PSE_", "refunded": false,
     "amount": {"from": {"currency": "COP", "total": 332500}},
     "processorFields": [{"keyword": "trazabilityCode", "value": "1597317"}]}
  ]
}`

func TestNormalizarAprobada(t *testing.T) {
	e := Normalizar(consulta(t, aprobadaPSE), relojFijo)
	if e.Estado != pasarela.EstadoAprobado || e.Aprobacion == nil {
		t.Fatalf("estado %+v", e)
	}
	a := e.Aprobacion
	if a.ReferenciaInterna != 1597317 || a.Autorizacion != "3324451" || a.Recibo != "1597317690" || a.Franquicia != "_PSE_" ||
		a.Medio != "PSE" || a.Emisor != "BANCO DE PRUEBAS" || a.Fecha.IsZero() {
		t.Fatalf("aprobación %+v", a)
	}
	// Lo que se guarda no lleva comprador ni datos de tarjeta (FR-042).
	for _, prohibido := range []string{"processorFields", "lastDigits", "411111", "1111", "buyer", "ana@ejemplo.test", "1000000001"} {
		if strings.Contains(string(e.Respuesta), prohibido) {
			t.Fatalf("la respuesta guardada lleva %q: %s", prohibido, e.Respuesta)
		}
	}
}

func TestNormalizarEstados(t *testing.T) {
	casos := []struct {
		nombre, crudo string
		ahora         time.Time
		esperado      pasarela.EstadoDePago
		diferencia    bool
	}{
		{"pendiente", `{"status":{"status":"PENDING","reason":"PT","date":"2026-11-09T10:00:00-05:00"},"payment":[]}`, relojFijo, pasarela.EstadoPendiente, false},
		{"rechazada con intento", `{"status":{"status":"REJECTED","reason":"05"},"request":{"expiration":"2026-11-09T10:35:00-05:00"},
			"payment":[{"status":{"status":"REJECTED"}}]}`, relojFijo, pasarela.EstadoRechazado, false},
		{"vencida por motivo", `{"status":{"status":"REJECTED","reason":"EX"},"payment":[]}`, relojFijo, pasarela.EstadoVencido, false},
		{"vencida sin intentos", `{"status":{"status":"REJECTED","reason":"?C"},"request":{"expiration":"2026-11-09T09:00:00-05:00"},"payment":null}`,
			relojFijo, pasarela.EstadoVencido, false},
		{"rechazada antes de vencer", `{"status":{"status":"REJECTED","reason":"?C"},"request":{"expiration":"2026-11-09T23:00:00-05:00"}}`,
			relojFijo, pasarela.EstadoRechazado, false},
		{"motivo numérico", `{"status":{"status":"FAILED","reason":401,"message":"Autenticación fallida"}}`, relojFijo, pasarela.EstadoFallido, false},
		{"parcial", `{"status":{"status":"APPROVED_PARTIAL"}}`, relojFijo, pasarela.EstadoPendiente, true},
	}
	for _, c := range casos {
		e := Normalizar(consulta(t, c.crudo), c.ahora)
		if e.Estado != c.esperado || e.Diferencia != c.diferencia {
			t.Errorf("%s: %s (diferencia %v)", c.nombre, e.Estado, e.Diferencia)
		}
	}
}

func comprador() pasarela.Comprador {
	return pasarela.Comprador{TipoDeDocumento: "CC", NumeroDeDocumento: "1000000001", Nombres: "Ana María", Apellidos: "Núñez Peña",
		Correo: "ana@ejemplo.test", Celular: "3001234567"}
}

func TestCrearSesionArmaElPedido(t *testing.T) {
	var recibido map[string]any
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/api/session" || r.Method != http.MethodPost {
			t.Errorf("ruta %s %s", r.Method, r.URL.Path)
		}
		b, _ := io.ReadAll(r.Body)
		_ = json.Unmarshal(b, &recibido)
		_, _ = w.Write([]byte(`{"status":{"status":"OK","reason":"PC","message":"La petición se ha procesado correctamente","date":"2026-11-09T10:00:00-05:00"},
			"requestId":98765,"processUrl":"https://checkout.ejemplo.test/spa/session/98765/abc"}`))
	}))
	defer srv.Close()

	s, err := nueva(srv.Client()).CrearSesion(context.Background(), sitio(srv.URL), pasarela.SolicitudDeSesion{
		Referencia: "ABC2611090000000A1", Descripcion: "Pago en línea", Total: 332500, Comprador: comprador(),
		IP: "190.0.0.1", Agente: "Mozilla/5.0", Vence: relojFijo.Add(15 * time.Minute),
		URLDeRetorno: "https://asociados.ejemplo.test/pagos/x/retorno", Medios: []string{"pse", "amex"},
	})
	if err != nil || s.RequestID != 98765 || !strings.HasPrefix(s.ProcessURL, "https://checkout.ejemplo.test/") {
		t.Fatalf("%v %+v", err, s)
	}
	auth := recibido["auth"].(map[string]any)
	if auth["login"] != "loginDePrueba" || auth["tranKey"] != "QVYVkYNQE0rkKwY9vjVjRb14q3sPdAraTT+jSck/J5I=" {
		t.Fatalf("auth %+v", auth)
	}
	if strings.Contains(mustJSON(recibido), secretoDePrueba) {
		t.Fatal("el secreto viajó en el cuerpo")
	}
	pago := recibido["payment"].(map[string]any)
	monto := pago["amount"].(map[string]any)
	if pago["reference"] != "ABC2611090000000A1" || pago["allowPartial"] != false || monto["currency"] != "COP" || monto["total"] != float64(332500) {
		t.Fatalf("pago %+v", pago)
	}
	if _, hay := monto["taxes"]; hay {
		t.Fatal("sin impuestos no va el arreglo")
	}
	comprador := recibido["buyer"].(map[string]any)
	if comprador["documentType"] != "CC" || comprador["name"] != "Ana María" || comprador["surname"] != "Núñez Peña" {
		t.Fatalf("comprador %+v", comprador)
	}
	if recibido["ipAddress"] != "190.0.0.1" || recibido["locale"] != "es_CO" || recibido["expiration"] != "2026-11-09T15:19:05Z" ||
		recibido["paymentMethod"] != "pse" {
		t.Fatalf("pedido %+v", recibido)
	}
}

func mustJSON(v any) string { b, _ := json.Marshal(v); return string(b) }

func TestErroresDeLaPasarela(t *testing.T) {
	codigo := http.StatusUnauthorized
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		w.WriteHeader(codigo)
		_, _ = w.Write([]byte(`{"status":{"status":"FAILED","reason":401,"message":"Autenticación fallida 101","date":"2026-11-09T10:00:00-05:00"}}`))
	}))
	defer srv.Close()
	p := nueva(srv.Client())

	_, err := p.ConsultarSesion(context.Background(), sitio(srv.URL), 1)
	var rechazo *pasarela.ErrRechazo
	if !errors.As(err, &rechazo) || rechazo.Codigo != "401" {
		t.Fatalf("401 → %v", err)
	}
	codigo = http.StatusBadGateway
	if _, err := p.ConsultarSesion(context.Background(), sitio(srv.URL), 1); !errors.Is(err, pasarela.ErrNoDisponible) {
		t.Fatalf("502 → %v", err)
	}
	if _, err := p.ConsultarSesion(context.Background(), sitio("http://127.0.0.1:1"), 1); !errors.Is(err, pasarela.ErrNoDisponible) {
		t.Fatalf("sin red → %v", err)
	}
}

func TestVerificadorDeCredenciales(t *testing.T) {
	rechazar := false
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if rechazar {
			w.WriteHeader(http.StatusUnauthorized)
			_, _ = w.Write([]byte(`{"status":{"status":"FAILED","reason":401,"message":"Autenticación fallida 101"}}`))
			return
		}
		if r.URL.Path == "/api/session" {
			_, _ = w.Write([]byte(`{"status":{"status":"OK"},"requestId":5,"processUrl":"https://x.test/5"}`))
			return
		}
		_, _ = w.Write([]byte(`{"requestId":5,"status":{"status":"PENDING"}}`))
	}))
	defer srv.Close()
	v := Verificador{Pasarela: nueva(srv.Client()), HostPublico: "asociados.ejemplo.test"}
	c := pasarela.Credenciales{URLBase: srv.URL, Login: "l", Secreto: secretoDePrueba}

	r, err := v.Verificar(context.Background(), uuid.New(), c)
	if err != nil || r.Estado != pasarela.EstadoActiva || r.VerificadaEn == nil {
		t.Fatalf("válidas: %v %+v", err, r)
	}
	rechazar = true
	r, err = v.Verificar(context.Background(), uuid.New(), c)
	if err != nil || r.Estado != pasarela.EstadoFallida || !strings.Contains(r.Motivo, "Autenticación fallida 101") {
		t.Fatalf("rechazadas: %v %+v", err, r)
	}
}

func TestValidarComprador(t *testing.T) {
	bien := comprador()
	if tipo, err := pasarela.ValidarComprador(bien); err != nil || tipo != "CC" {
		t.Fatalf("%v %s", err, tipo)
	}
	pa := bien
	pa.TipoDeDocumento, pa.NumeroDeDocumento = "PA", "AB123456"
	if tipo, err := pasarela.ValidarComprador(pa); err != nil || tipo != "PPN" {
		t.Fatalf("pasaporte: %v %s", err, tipo)
	}
	malos := map[string]func(*pasarela.Comprador){
		"documentType":   func(c *pasarela.Comprador) { c.TipoDeDocumento = "RC" },
		"documentNumber": func(c *pasarela.Comprador) { c.NumeroDeDocumento = "0123" },
		"names":          func(c *pasarela.Comprador) { c.Nombres = "Ana 2" },
		"surnames":       func(c *pasarela.Comprador) { c.Apellidos = "Pérez-Gómez" },
		"email":          func(c *pasarela.Comprador) { c.Correo = "" },
		"mobile":         func(c *pasarela.Comprador) { c.Celular = "+57 300" },
	}
	for campo, dañar := range malos {
		c := bien
		dañar(&c)
		var e *pasarela.ErrCompradorInvalido
		if _, err := pasarela.ValidarComprador(c); !errors.As(err, &e) || e.Campo != campo {
			t.Errorf("%s: %v", campo, err)
		}
	}
}
