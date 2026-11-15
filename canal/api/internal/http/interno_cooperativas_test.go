package httpapi

import (
	"bytes"
	"context"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"encoding/json"
	"errors"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"slices"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/riverqueue/river"
	"github.com/riverqueue/river/rivertype"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/cifrado"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/config"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/entidades"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
)

const secretoDePrueba = "SecretoDePrueba-3b2a1c0d"

// encoladorFalso registra lo que se encola en vez de insertarlo en River.
type encoladorFalso struct {
	mu   sync.Mutex
	args []river.JobArgs
}

func (e *encoladorFalso) Insert(_ context.Context, args river.JobArgs, _ *river.InsertOpts) (*rivertype.JobInsertResult, error) {
	e.mu.Lock()
	defer e.mu.Unlock()
	e.args = append(e.args, args)
	return &rivertype.JobInsertResult{Job: &rivertype.JobRow{ID: int64(len(e.args))}}, nil
}

// bufer es un bytes.Buffer seguro para el logger.
type bufer struct {
	mu sync.Mutex
	b  bytes.Buffer
}

func (b *bufer) Write(p []byte) (int, error) {
	b.mu.Lock()
	defer b.mu.Unlock()
	return b.b.Write(p)
}

func (b *bufer) String() string {
	b.mu.Lock()
	defer b.mu.Unlock()
	return b.b.String()
}

type entorno struct {
	pool      *pgxpool.Pool
	q         *gen.Queries
	llave     *ecdsa.PrivateKey
	llavero   *cifrado.Llavero
	encolador *encoladorFalso
	logs      *bufer
	simulada  http.Handler // CANAL_PASARELA=simulada
	real      http.Handler // CANAL_PASARELA=placetopay
}

func nuevoEntorno(t *testing.T) *entorno {
	t.Helper()
	pool := dbtest.Migrada(t)
	llave, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		t.Fatal(err)
	}
	llavero, err := cifrado.NuevoLlavero(map[int][]byte{3: bytes.Repeat([]byte{7}, 32)}, 3)
	if err != nil {
		t.Fatal(err)
	}
	e := &entorno{pool: pool, q: gen.New(pool), llave: llave, llavero: llavero, encolador: &encoladorFalso{}, logs: &bufer{}}
	logger := auditoria.NuevoLogger(e.logs, slog.LevelDebug)
	registro := auditoria.Nuevo(pool)
	armar := func(nombre string) http.Handler {
		verificador := erp.NuevoVerificador(&llave.PublicKey, erp.NuevoAlmacenDeJtiEnBase(e.q), logger)
		s := Nuevo(Opciones{Logger: logger, Interno: verificador.Middleware, LimitePorMinuto: 10000, LimiteRafaga: 10000})
		s.RegistrarInternasDeCooperativas(RutasInternasDeCooperativas{
			Encolador:    e.encolador,
			Codigos:      entidades.NuevosCodigos(pool, logger),
			Sitios:       pasarela.NuevosSitios(pool, llavero, pasarela.VerificadorPara(nombre), registro, nombre == config.PasarelaSimulada, logger),
			Cooperativas: e.q,
			Auditoria:    registro,
			Logger:       logger,
		})
		return s.Handler()
	}
	e.simulada, e.real = armar(config.PasarelaSimulada), armar(config.PasarelaPlacetopay)
	return e
}

// pedir firma un token del ERP para sujeto (como ClienteDelCanal del ERP) y llama la ruta.
func (e *entorno) pedir(t *testing.T, h http.Handler, metodo, ruta string, sujeto uuid.UUID, cuerpo string) *httptest.ResponseRecorder {
	t.Helper()
	ahora := time.Now()
	token, err := jwt.NewWithClaims(jwt.SigningMethodES256, jwt.RegisteredClaims{
		Issuer: erp.EmisorDelERP, Audience: jwt.ClaimStrings{erp.AudienciaDelCanal}, Subject: sujeto.String(),
		IssuedAt: jwt.NewNumericDate(ahora), NotBefore: jwt.NewNumericDate(ahora),
		ExpiresAt: jwt.NewNumericDate(ahora.Add(60 * time.Second)), ID: uuid.NewString(),
	}).SignedString(e.llave)
	if err != nil {
		t.Fatal(err)
	}
	r := httptest.NewRequest(metodo, ruta, strings.NewReader(cuerpo))
	r.Header.Set("Authorization", "Bearer "+token)
	r.Header.Set("Content-Type", "application/json; charset=utf-8")
	w := httptest.NewRecorder()
	h.ServeHTTP(w, r)
	return w
}

func (e *entorno) auditoria(t *testing.T, accion string) []gen.AuditLog {
	t.Helper()
	filas, err := e.q.ListAuditEntriesByAction(context.Background(), gen.ListAuditEntriesByActionParams{Action: accion, MaxRows: 100})
	if err != nil {
		t.Fatal(err)
	}
	return filas
}

func codigoDe(t *testing.T, w *httptest.ResponseRecorder) string {
	t.Helper()
	var s struct {
		Code string `json:"code"`
	}
	if err := json.Unmarshal(w.Body.Bytes(), &s); err != nil {
		t.Fatalf("respuesta sin sobre: %s", w.Body.String())
	}
	return s.Code
}

func TestRutasInternasDeCooperativas(t *testing.T) {
	e := nuevoEntorno(t)
	t.Run("sync encola el arrastre", func(t *testing.T) { syncEncola(t, e) })
	t.Run("código de canal", func(t *testing.T) { codigoDeCanal(t, e) })
	t.Run("credenciales con la pasarela simulada", func(t *testing.T) { credencialesSimulada(t, e) })
	t.Run("credenciales de Placetopay quedan por verificar", func(t *testing.T) { credencialesPorVerificar(t, e) })
	t.Run("https obligatorio", func(t *testing.T) { httpsObligatorio(t, e) })
	t.Run("validación con el sobre", func(t *testing.T) { validacionConSobre(t, e) })
	t.Run("sitio de otra cooperativa", func(t *testing.T) { sitioDeOtraCooperativa(t, e) })
	// Al final: revisa que ningún registro de las pruebas anteriores tenga el secreto.
	t.Run("el secreto no está en la auditoría ni en el log", func(t *testing.T) { secretoAusente(t, e) })
}

func syncEncola(t *testing.T, e *entorno) {
	coop := uuid.New()
	w := e.pedir(t, e.simulada, http.MethodPost, "/internal/v1/tenants/"+coop.String()+"/sync", coop, "{}")
	if w.Code != http.StatusAccepted || strings.TrimSpace(w.Body.String()) != `{"enqueued":true}` {
		t.Fatalf("respuesta: %d %s", w.Code, w.Body.String())
	}
	e.encolador.mu.Lock()
	ultimo := e.encolador.args[len(e.encolador.args)-1]
	e.encolador.mu.Unlock()
	if a, ok := ultimo.(entidades.ArgsArrastrarCooperativa); !ok || a.TenantPublicID != coop {
		t.Fatalf("encolado inesperado: %#v", ultimo)
	}
	filas := e.auditoria(t, AccionArrastreSolicitado)
	if len(filas) != 1 || filas[0].Actor != "erp_staff" || filas[0].Subject != coop.String() {
		t.Fatalf("auditoría inesperada: %+v", filas)
	}

	// Un token de otra cooperativa no pasa (lo frena el verificador antes de llegar aquí).
	if w := e.pedir(t, e.simulada, http.MethodPost, "/internal/v1/tenants/"+coop.String()+"/sync", uuid.New(), "{}"); w.Code != http.StatusForbidden {
		t.Fatalf("otra cooperativa: %d %s", w.Code, w.Body.String())
	}
}

func codigoDeCanal(t *testing.T, e *entorno) {
	a, b := uuid.New(), uuid.New()
	pedir := func(coop uuid.UUID, cuerpo string) *httptest.ResponseRecorder {
		return e.pedir(t, e.simulada, http.MethodPost, "/internal/v1/tenants/"+coop.String()+"/channel-code", coop, cuerpo)
	}
	for _, caso := range []struct {
		coop     uuid.UUID
		cuerpo   string
		esperado string
	}{
		{a, `{"suggested":"Coop. Fondo Palmira"}`, `{"code":"COO"}`},
		{a, `{"suggested":"XYZ"}`, `{"code":"COO"}`}, // ya tenía
		{b, `{"suggested":"coo"}`, `{"code":"COA"}`}, // colisión: variante
	} {
		w := pedir(caso.coop, caso.cuerpo)
		if w.Code != http.StatusOK || strings.TrimSpace(w.Body.String()) != caso.esperado {
			t.Fatalf("%s: %d %s, se esperaba %s", caso.cuerpo, w.Code, w.Body.String(), caso.esperado)
		}
	}
	if filas := e.auditoria(t, AccionCodigoReservado); len(filas) != 3 {
		t.Fatalf("se esperaban 3 entradas de auditoría, hay %d", len(filas))
	}
	if w := pedir(a, `{"suggested":`); w.Code != http.StatusBadRequest || codigoDe(t, w) != "Canal.Solicitud.CuerpoInvalido" {
		t.Fatalf("JSON roto: %d %s", w.Code, w.Body.String())
	}
}

const cuerpoValido = `{"gateway":"Placetopay","baseUrl":"https://checkout.pasarela.test","login":"login-de-prueba-6789",` +
	`"secretKey":"` + secretoDePrueba + `","paymentMethods":["pse","visa","master"]}`

func rutaDeCredenciales(coop, sitio uuid.UUID) string {
	return "/internal/v1/tenants/" + coop.String() + "/sites/" + sitio.String() + "/credentials"
}

func credencialesSimulada(t *testing.T, e *entorno) {
	ctx := context.Background()
	coop, sitio := uuid.New(), uuid.New()
	w := e.pedir(t, e.simulada, http.MethodPut, rutaDeCredenciales(coop, sitio), coop, cuerpoValido)
	if w.Code != http.StatusOK {
		t.Fatalf("respuesta: %d %s", w.Code, w.Body.String())
	}
	if strings.Contains(w.Body.String(), secretoDePrueba) {
		t.Fatalf("la respuesta trae el secreto: %s", w.Body.String())
	}
	var r map[string]any
	if err := json.Unmarshal(w.Body.Bytes(), &r); err != nil {
		t.Fatal(err)
	}
	claves := make([]string, 0, len(r))
	for k := range r {
		claves = append(claves, k)
	}
	slices.Sort(claves)
	if !slices.Equal(claves, []string{"activa", "loginLast4", "motivo", "verificadaEn"}) {
		t.Fatalf("forma de la respuesta: %s", w.Body.String())
	}
	if r["activa"] != true || r["loginLast4"] != "6789" || r["motivo"] != nil {
		t.Fatalf("respuesta inesperada: %s", w.Body.String())
	}
	if v, ok := r["verificadaEn"].(string); !ok {
		t.Fatalf("verificadaEn debía venir: %s", w.Body.String())
	} else if _, err := time.Parse(time.RFC3339, v); err != nil {
		t.Fatalf("verificadaEn no es RFC 3339: %v", err)
	}

	// En la base: cifrado, que no contiene el secreto y que vuelve con la KEK y el id del sitio.
	fila, err := e.q.GetGatewaySite(ctx, sitio)
	if err != nil {
		t.Fatal(err)
	}
	if fila.TenantPublicID != coop || fila.Status != "active" || fila.CheckedAt == nil || fila.KekVersion != 3 ||
		!slices.Equal(fila.PaymentMethods, []string{"pse", "visa", "master"}) || fila.Gateway != "placetopay" {
		t.Fatalf("fila inesperada: %+v", fila)
	}
	if bytes.Contains(fila.SecretCiphertext, []byte(secretoDePrueba)) {
		t.Fatalf("el secreto quedó en claro en la base")
	}
	plano, err := e.llavero.Descifrar(fila.SecretCiphertext, int(fila.KekVersion), sitio[:])
	if err != nil || string(plano) != secretoDePrueba {
		t.Fatalf("el secreto no vuelve con la KEK y el sitio: %v", err)
	}
	otro := uuid.New()
	if _, err := e.llavero.Descifrar(fila.SecretCiphertext, int(fila.KekVersion), otro[:]); err == nil {
		t.Fatalf("el secreto no debía descifrarse con el id de otro sitio")
	}
	var crudo string
	if err := e.pool.QueryRow(ctx, "SELECT row_to_json(g)::text FROM gateway_sites g WHERE id = $1", sitio).Scan(&crudo); err != nil {
		t.Fatal(err)
	}
	if strings.Contains(crudo, secretoDePrueba) {
		t.Fatalf("la fila tiene el secreto en claro")
	}

	// Actualizar el mismo sitio: otro login y otro cifrado.
	cambiado := strings.Replace(cuerpoValido, "login-de-prueba-6789", "otro-login-4321", 1)
	if w := e.pedir(t, e.simulada, http.MethodPut, rutaDeCredenciales(coop, sitio), coop, cambiado); w.Code != http.StatusOK ||
		!strings.Contains(w.Body.String(), `"loginLast4":"4321"`) {
		t.Fatalf("actualizar: %d %s", w.Code, w.Body.String())
	}
	actualizada, err := e.q.GetGatewaySite(ctx, sitio)
	if err != nil {
		t.Fatal(err)
	}
	if actualizada.Login != "otro-login-4321" || bytes.Equal(actualizada.SecretCiphertext, fila.SecretCiphertext) {
		t.Fatalf("la actualización no se guardó: %+v", actualizada)
	}

	filas := e.auditoria(t, pasarela.AccionCredencialesGuardadas)
	if len(filas) < 2 || filas[0].Actor != "erp_staff" || filas[0].Subject != sitio.String() ||
		!strings.Contains(string(filas[0].Data), `"loginLast4": "6789"`) {
		t.Fatalf("auditoría inesperada: %+v", filas)
	}
}

func credencialesPorVerificar(t *testing.T, e *entorno) {
	coop, sitio := uuid.New(), uuid.New()
	w := e.pedir(t, e.real, http.MethodPut, rutaDeCredenciales(coop, sitio), coop, cuerpoValido)
	if w.Code != http.StatusOK {
		t.Fatalf("respuesta: %d %s", w.Code, w.Body.String())
	}
	var r struct {
		Activa       bool    `json:"activa"`
		VerificadaEn *string `json:"verificadaEn"`
		LoginLast4   string  `json:"loginLast4"`
		Motivo       *string `json:"motivo"`
	}
	if err := json.Unmarshal(w.Body.Bytes(), &r); err != nil {
		t.Fatal(err)
	}
	if r.Activa || r.VerificadaEn != nil || r.Motivo == nil || *r.Motivo != pasarela.MotivoSinAdaptador || r.LoginLast4 != "6789" {
		t.Fatalf("respuesta inesperada: %s", w.Body.String())
	}
	fila, err := e.q.GetGatewaySite(context.Background(), sitio)
	if err != nil {
		t.Fatal(err)
	}
	if fila.Status != "pending_verification" || fila.CheckedAt != nil {
		t.Fatalf("fila inesperada: %+v", fila)
	}
}

func httpsObligatorio(t *testing.T, e *entorno) {
	coop := uuid.New()
	conURL := func(u string) string {
		return strings.Replace(cuerpoValido, "https://checkout.pasarela.test", u, 1)
	}
	casos := []struct {
		h      http.Handler
		url    string
		estado int
	}{
		{e.simulada, "http://checkout.pasarela.test", http.StatusBadRequest},
		{e.real, "http://checkout.pasarela.test", http.StatusBadRequest},
		{e.simulada, "http://localhost:9090", http.StatusOK},
		{e.simulada, "http://127.0.0.1:9090", http.StatusOK},
		{e.real, "http://localhost:9090", http.StatusBadRequest},
		{e.real, "https://checkout.pasarela.test/ruta", http.StatusOK},
	}
	for _, c := range casos {
		w := e.pedir(t, c.h, http.MethodPut, rutaDeCredenciales(coop, uuid.New()), coop, conURL(c.url))
		if w.Code != c.estado {
			t.Errorf("%s: %d %s, se esperaba %d", c.url, w.Code, w.Body.String(), c.estado)
		}
		if c.estado == http.StatusBadRequest && codigoDe(t, w) != "Canal.Sitio.UrlInvalida" {
			t.Errorf("%s: código %s", c.url, codigoDe(t, w))
		}
	}
}

func validacionConSobre(t *testing.T, e *entorno) {
	coop := uuid.New()
	w := e.pedir(t, e.simulada, http.MethodPut, rutaDeCredenciales(coop, uuid.New()), coop,
		`{"gateway":"placetopay","baseUrl":"https://x.test","login":"","secretKey":"","paymentMethods":["pse","bitcoin"]}`)
	if w.Code != http.StatusBadRequest {
		t.Fatalf("respuesta: %d %s", w.Code, w.Body.String())
	}
	var s struct {
		Code    string `json:"code"`
		Message string `json:"message"`
		Data    struct {
			Errores []pasarela.Problema `json:"errores"`
		} `json:"data"`
	}
	if err := json.Unmarshal(w.Body.Bytes(), &s); err != nil {
		t.Fatal(err)
	}
	if s.Code != "Canal.Sitio.LoginInvalido" || s.Message == "" || len(s.Data.Errores) != 3 {
		t.Fatalf("sobre inesperado: %s", w.Body.String())
	}
	if w := e.pedir(t, e.simulada, http.MethodPut, "/internal/v1/tenants/"+coop.String()+"/sites/no-es-uuid/credentials", coop, cuerpoValido); w.Code != http.StatusBadRequest || codigoDe(t, w) != "Canal.Ruta.IdentificadorInvalido" {
		t.Fatalf("sitio inválido: %d %s", w.Code, w.Body.String())
	}
	if filas := e.auditoria(t, pasarela.AccionCredencialesRechazadas); len(filas) == 0 {
		t.Fatalf("los rechazos también se auditan")
	}
}

func sitioDeOtraCooperativa(t *testing.T, e *entorno) {
	a, b, sitio := uuid.New(), uuid.New(), uuid.New()
	if w := e.pedir(t, e.simulada, http.MethodPut, rutaDeCredenciales(a, sitio), a, cuerpoValido); w.Code != http.StatusOK {
		t.Fatalf("crear: %d %s", w.Code, w.Body.String())
	}
	cambiado := strings.Replace(cuerpoValido, "login-de-prueba-6789", "intruso-0000", 1)
	w := e.pedir(t, e.simulada, http.MethodPut, rutaDeCredenciales(b, sitio), b, cambiado)
	if w.Code != http.StatusConflict || codigoDe(t, w) != "Canal.Sitio.DeOtraCooperativa" {
		t.Fatalf("se esperaba 409, llegó %d %s", w.Code, w.Body.String())
	}
	fila, err := e.q.GetGatewaySite(context.Background(), sitio)
	if err != nil {
		t.Fatal(err)
	}
	if fila.TenantPublicID != a || fila.Login != "login-de-prueba-6789" {
		t.Fatalf("el sitio de otra cooperativa cambió: %+v", fila)
	}

	// La guarda de la escritura (por si otra cooperativa gana la carrera entre la lectura y el upsert).
	_, err = e.q.UpsertGatewaySite(context.Background(), gen.UpsertGatewaySiteParams{
		ID: sitio, TenantPublicID: b, Gateway: "placetopay", BaseUrl: "https://x.test", Login: "intruso",
		SecretCiphertext: fila.SecretCiphertext, KekVersion: fila.KekVersion, PaymentMethods: []string{}, Status: "active",
	})
	if !errors.Is(err, pgx.ErrNoRows) {
		t.Fatalf("el upsert de otra cooperativa debía no devolver fila, llegó %v", err)
	}
}

func secretoAusente(t *testing.T, e *entorno) {
	filas, err := e.pool.Query(context.Background(), "SELECT data::text FROM audit_log")
	if err != nil {
		t.Fatal(err)
	}
	defer filas.Close()
	n := 0
	for filas.Next() {
		var datos string
		if err := filas.Scan(&datos); err != nil {
			t.Fatal(err)
		}
		if strings.Contains(datos, secretoDePrueba) {
			t.Fatalf("la auditoría tiene el secreto: %s", datos)
		}
		n++
	}
	if err := filas.Err(); err != nil {
		t.Fatal(err)
	}
	if n == 0 {
		t.Fatalf("no hubo auditoría que revisar")
	}
	if logs := e.logs.String(); logs == "" || strings.Contains(logs, secretoDePrueba) {
		t.Fatalf("el log está vacío o tiene el secreto")
	}
}
