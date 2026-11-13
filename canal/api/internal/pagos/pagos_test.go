package pagos

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/riverqueue/river"
	"github.com/riverqueue/river/rivertype"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/cifrado"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela/simulada"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T094: intentos de pago contra PostgreSQL real y la pasarela simulada. Un contenedor para todas las subpruebas; cada
// una usa su cooperativa y su cuenta.
func TestPagos(t *testing.T) {
	pool := dbtest.Migrada(t)
	t.Run("doble clic crea un solo intento", func(t *testing.T) { dobleClic(t, pool) })
	t.Run("un pendiente bloquea el concepto", func(t *testing.T) { pendienteBloquea(t, pool) })
	t.Run("validaciones contra los valores del ERP", func(t *testing.T) { validaciones(t, pool) })
	t.Run("textos por aceptar y código de pago", func(t *testing.T) { textosYCodigo(t, pool) })
	t.Run("retorno, notificación y sonda convergen sin duplicar", func(t *testing.T) { convergencia(t, pool) })
	t.Run("vencimiento y aprobación después", func(t *testing.T) { vencimiento(t, pool) })
	t.Run("firma inválida no cambia nada", func(t *testing.T) { firmaInvalida(t, pool) })
	t.Run("pasarela caída al crear", func(t *testing.T) { pasarelaCaida(t, pool) })
	t.Run("sonda: frecuencia y alerta a los 7 días", func(t *testing.T) { sonda(t, pool) })
	t.Run("aviso al ERP y comprobante", func(t *testing.T) { avisoAlERP(t, pool) })
}

// ----------------------------------------------------------------------------------------------------- banco --

type reloj struct {
	mu sync.Mutex
	t  time.Time
}

func (r *reloj) ahora() time.Time { r.mu.Lock(); defer r.mu.Unlock(); return r.t }
func (r *reloj) avanzar(d time.Duration) {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.t = r.t.Add(d)
}

// avisadorFalso es el ERP que recibe los avisos y entrega los comprobantes.
type avisadorFalso struct {
	mu        sync.Mutex
	avisos    []erp.AvisoDePago
	resultado erp.ResultadoDelAviso
	err       error
}

func (a *avisadorFalso) AvisarPago(_ context.Context, _, _ uuid.UUID, aviso erp.AvisoDePago) (erp.ResultadoDelAviso, error) {
	a.mu.Lock()
	defer a.mu.Unlock()
	a.avisos = append(a.avisos, aviso)
	return a.resultado, a.err
}

func (a *avisadorFalso) Comprobante(context.Context, uuid.UUID, uuid.UUID, uuid.UUID) ([]byte, string, error) {
	return []byte("%PDF-1.7 comprobante"), "application/pdf", nil
}

type erpFalso struct {
	mu        sync.Mutex
	conceptos erp.Conceptos
	textos    []erp.TextoLegal
	caido     bool
}

func (e *erpFalso) Conceptos(context.Context, uuid.UUID, uuid.UUID) (erp.Conceptos, error) {
	e.mu.Lock()
	defer e.mu.Unlock()
	if e.caido {
		return erp.Conceptos{}, fmt.Errorf("ERP caído")
	}
	return e.conceptos, nil
}

func (e *erpFalso) TextosLegales(context.Context, uuid.UUID) ([]erp.TextoLegal, error) {
	e.mu.Lock()
	defer e.mu.Unlock()
	return e.textos, nil
}

type encoladorFalso struct {
	mu   sync.Mutex
	jobs []river.JobArgs
}

func (e *encoladorFalso) InsertTx(_ context.Context, _ pgx.Tx, a river.JobArgs, _ *river.InsertOpts) (*rivertype.JobInsertResult, error) {
	return e.Insert(context.Background(), a, nil)
}

func (e *encoladorFalso) Insert(_ context.Context, a river.JobArgs, _ *river.InsertOpts) (*rivertype.JobInsertResult, error) {
	e.mu.Lock()
	defer e.mu.Unlock()
	e.jobs = append(e.jobs, a)
	return &rivertype.JobInsertResult{}, nil
}

func (e *encoladorFalso) tomar() []river.JobArgs {
	e.mu.Lock()
	defer e.mu.Unlock()
	j := e.jobs
	e.jobs = nil
	return j
}

type codigosFalsos struct{ validos map[uuid.UUID]bool }

func (c *codigosFalsos) TomarCodigoDePago(_ context.Context, _ *gen.Queries, _, desafio uuid.UUID) error {
	if !c.validos[desafio] {
		return sobre.Nuevo(http.StatusForbidden, "Canal.Pago.CodigoRequerido", "código")
	}
	delete(c.validos, desafio)
	return nil
}

type mux struct{ *http.ServeMux }

func (m mux) Manejar(patron string, h http.Handler) { m.Handle(patron, h) }

type banco struct {
	t       *testing.T
	pool    *pgxpool.Pool
	q       *gen.Queries
	s       *Servicio
	h       http.Handler
	reloj   *reloj
	erp     *erpFalso
	sim     *simulada.Pasarela
	cola    *encoladorFalso
	aviso   *avisadorFalso
	codigos *codigosFalsos
	tenant  gen.Tenant
	cuenta  uuid.UUID
	persona uuid.UUID
	sitio   pasarela.Sitio
	destino uuid.UUID
}

var numero atomic.Int64

const (
	claveCredito = "LOAN:11111111-1111-1111-1111-111111111111"
	claveAporte  = "ACCOUNT:22222222-2222-2222-2222-222222222222"
)

func nuevoBanco(t *testing.T, pool *pgxpool.Pool) *banco {
	t.Helper()
	ctx := context.Background()
	r := &reloj{t: time.Now().UTC().Truncate(time.Second)}
	b := &banco{t: t, pool: pool, q: gen.New(pool), reloj: r, cola: &encoladorFalso{}, aviso: &avisadorFalso{}, codigos: &codigosFalsos{validos: map[uuid.UUID]bool{}},
		persona: uuid.New(), destino: uuid.New()}
	b.sim = simulada.Nueva("asociados.ejemplo.test").ConReloj(r.ahora)

	n := numero.Add(1)
	tenant, err := b.q.UpsertTenant(ctx, gen.UpsertTenantParams{
		ID: uuid.Must(uuid.NewV7()), TenantPublicID: uuid.New(), ChannelCode: fmt.Sprintf("P%c%c", 'A'+n/26%26, 'A'+n%26),
		DisplayName: "Cooperativa de prueba", Brand: json.RawMessage(`{}`), Support: json.RawMessage(`{"phone":"6025550000"}`),
		InactivityMinutes: 5, PaymentSessionMinutes: 15, LastSyncAt: r.ahora(),
	})
	if err != nil {
		t.Fatal(err)
	}
	b.tenant = tenant
	b.cuenta = uuid.Must(uuid.NewV7())
	correo := fmt.Sprintf("ana%d@ejemplo.test", n)
	celular := "3001234567"
	if err := b.q.InsertAccount(ctx, gen.InsertAccountParams{ID: b.cuenta, DocumentType: "CC", DocumentNumber: fmt.Sprintf("%d", 1000000000+n),
		FirstNames: "Ana María", LastNames: "Núñez", Email: &correo, Mobile: &celular, PasswordHash: strings.Repeat("x", 60),
		ActivatedAt: r.ahora(), ValidatingTenantID: tenant.ID}); err != nil {
		t.Fatal(err)
	}
	for _, k := range []string{"Terms", "DataPolicy"} {
		if err := b.q.InsertLegalAcceptance(ctx, gen.InsertLegalAcceptanceParams{ID: uuid.New(), AccountID: b.cuenta, TenantID: tenant.ID,
			Kind: k, Version: 1, AcceptedAt: r.ahora()}); err != nil {
			t.Fatal(err)
		}
	}

	llavero, err := cifrado.NuevoLlavero(map[int][]byte{1: bytes.Repeat([]byte{3}, 32)}, 1)
	if err != nil {
		t.Fatal(err)
	}
	registro := auditoria.Nuevo(pool)
	sitios := pasarela.NuevosSitios(pool, llavero, pasarela.VerificadorSimulado{}, registro, true, dbtest.Silencioso())
	sitioID := uuid.New()
	if _, err := sitios.Guardar(ctx, tenant.TenantPublicID, sitioID, pasarela.Credenciales{Pasarela: "placetopay",
		URLBase: "https://pruebas.ejemplo.test", Login: "login", Secreto: "secreto-de-prueba", MediosDePago: []string{"pse"}}); err != nil {
		t.Fatal(err)
	}
	if b.sitio, _, err = sitios.Cargar(ctx, sitioID); err != nil {
		t.Fatal(err)
	}

	b.erp = &erpFalso{
		textos: []erp.TextoLegal{{Tipo: "Terms", Version: 1}, {Tipo: "DataPolicy", Version: 1}, {Tipo: "Faq", Version: 3}},
		conceptos: erp.Conceptos{
			Fecha: "2026-12-05",
			Items: []erp.Concepto{
				{Clave: claveCredito, Tipo: "Loan", Descripcion: "Crédito CR-1", Producto: uuid.New(), ExigibleHoy: "332500", Pagable: true,
					Destino: &b.destino, ParaCancelar: ptr(json.Number("5450000")),
					Modos: erp.ModosDePago{Exigible: true, Parcial: &erp.PagoParcial{Minimo: "20000"},
						AbonoCapital: &erp.AbonoACapital{Preseleccion: "ReduceTerm"}, Cancelacion: true}},
				{Clave: claveAporte, Tipo: "Contribution", Descripcion: "Aportes", Producto: uuid.New(), ExigibleHoy: "50000", Pagable: true,
					Destino: &b.destino, Modos: erp.ModosDePago{Exigible: true}},
			},
			Destinos: []erp.Destino{{ID: b.destino, Sitio: sitioID, Nombre: "Avalpay", OfrecePSE: true}},
		},
	}

	b.s, err = Nuevo(Opciones{Pool: pool, ERP: b.erp, Sitios: sitios, Pasarela: b.sim, Codigos: b.codigos, Auditoria: registro,
		Encolador: b.cola, Avisador: b.aviso, Logger: dbtest.Silencioso(), HostPublico: "asociados.ejemplo.test", Ahora: r.ahora})
	if err != nil {
		t.Fatal(err)
	}
	m := mux{http.NewServeMux()}
	acotada := func(h http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			ctx := contexto.ConCooperativa(r.Context(), contexto.Cooperativa{ID: b.tenant.ID, PublicID: b.tenant.TenantPublicID})
			ctx = contexto.ConPersona(contexto.ConCuenta(ctx, b.cuenta), b.persona)
			ctx = contexto.ConIP(contexto.ConAgente(ctx, "Navegador de prueba"), "190.0.0.1")
			h.ServeHTTP(w, r.WithContext(ctx))
		})
	}
	b.s.Registrar(m, acotada)
	b.h = m
	return b
}

type respuesta struct {
	estado int
	cuerpo map[string]any
}

func (r respuesta) codigo() string        { c, _ := r.cuerpo["code"].(string); return c }
func (r respuesta) texto(k string) string { v, _ := r.cuerpo[k].(string); return v }
func (r respuesta) datos() map[string]any { d, _ := r.cuerpo["data"].(map[string]any); return d }

func (b *banco) pedir(metodo, ruta, llave string, cuerpo any) respuesta {
	b.t.Helper()
	var lector *bytes.Reader
	if cuerpo != nil {
		j, _ := json.Marshal(cuerpo)
		lector = bytes.NewReader(j)
	} else {
		lector = bytes.NewReader(nil)
	}
	req := httptest.NewRequest(metodo, ruta, lector)
	req.Header.Set("Content-Type", "application/json")
	if llave != "" {
		req.Header.Set("Idempotency-Key", llave)
	}
	rec := httptest.NewRecorder()
	b.h.ServeHTTP(rec, req)
	r := respuesta{estado: rec.Code, cuerpo: map[string]any{}}
	_ = json.Unmarshal(rec.Body.Bytes(), &r.cuerpo)
	return r
}

func item(clave, modalidad string, valor int64) map[string]any {
	return map[string]any{"conceptKey": clave, "modality": modalidad, "amount": valor}
}

func (b *banco) pagar(llave string, items ...map[string]any) respuesta {
	b.t.Helper()
	return b.pedir(http.MethodPost, "/api/v1/payments", llave, map[string]any{"items": items})
}

func (b *banco) requestID(intento string) int64 {
	b.t.Helper()
	a, err := b.q.GetPaymentAttempt(context.Background(), uuid.MustParse(intento))
	if err != nil || a.GatewayRequestID == nil {
		b.t.Fatalf("intento sin sesión: %v", err)
	}
	return *a.GatewayRequestID
}

func (b *banco) eventos(intento string) []gen.AttemptEvent {
	b.t.Helper()
	e, err := b.q.ListAttemptEvents(context.Background(), uuid.MustParse(intento))
	if err != nil {
		b.t.Fatal(err)
	}
	return e
}

func esperarEstado(t *testing.T, r respuesta, estado int, codigo string) {
	t.Helper()
	if r.estado != estado || (codigo != "" && r.codigo() != codigo) {
		t.Fatalf("esperaba %d %s, llegó %d %v", estado, codigo, r.estado, r.cuerpo)
	}
}

// --------------------------------------------------------------------------------------------------- pruebas --

func dobleClic(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	primero := b.pagar("llave-0001", item(claveCredito, "Due", 332500), item(claveAporte, "Due", 50000))
	esperarEstado(t, primero, http.StatusCreated, "")
	if primero.texto("status") != "pending" || primero.texto("processUrl") == "" || primero.cuerpo["total"] != float64(382500) {
		t.Fatalf("intento %v", primero.cuerpo)
	}
	ref := primero.texto("reference")
	if !ReferenciaValida(ref) || !strings.HasPrefix(ref, b.tenant.ChannelCode) {
		t.Fatalf("referencia %s", ref)
	}
	segundo := b.pagar("llave-0001", item(claveCredito, "Due", 332500))
	esperarEstado(t, segundo, http.StatusOK, "")
	if segundo.texto("attemptId") != primero.texto("attemptId") || b.sim.Sesiones() != 1 {
		t.Fatalf("el doble clic creó otro intento: %v (sesiones %d)", segundo.cuerpo, b.sim.Sesiones())
	}
	// Lo que se pidió a la pasarela: total, comprador, IP, agente, retorno y vencimiento (FR-037).
	s, _ := b.sim.Solicitud(b.requestID(primero.texto("attemptId")))
	if s.Total != 382500 || s.Comprador.Nombres != "Ana María" || s.IP != "190.0.0.1" || s.Agente != "Navegador de prueba" ||
		!strings.HasSuffix(s.URLDeRetorno, "/pagos/"+primero.texto("attemptId")+"/retorno") || !s.Vence.Equal(b.reloj.ahora().Add(15*time.Minute)) {
		t.Fatalf("solicitud %+v", s)
	}
}

func pendienteBloquea(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	primero := b.pagar("llave-0001", item(claveCredito, "Due", 332500))
	esperarEstado(t, primero, http.StatusCreated, "")
	otro := b.pagar("llave-0002", item(claveCredito, "Partial", 40000))
	esperarEstado(t, otro, http.StatusConflict, "Canal.Pago.Pendiente")
	if otro.datos()["reference"] != primero.texto("reference") || otro.datos()["support"] == nil {
		t.Fatalf("el aviso no trae la referencia ni el contacto: %v", otro.cuerpo)
	}
	// Otro concepto sí se puede pagar.
	esperarEstado(t, b.pagar("llave-0003", item(claveAporte, "Due", 50000)), http.StatusCreated, "")
	pend := b.pedir(http.MethodGet, "/api/v1/payments/pending", "", nil)
	if items, _ := pend.cuerpo["items"].([]any); len(items) != 2 {
		t.Fatalf("pendientes %v", pend.cuerpo)
	}
	// Rechazado en la pasarela, el concepto se libera.
	if err := b.sim.Fijar(b.requestID(primero.texto("attemptId")), pasarela.EstadoRechazado); err != nil {
		t.Fatal(err)
	}
	esperarEstado(t, b.pagar("llave-0004", item(claveCredito, "Partial", 40000)), http.StatusCreated, "")
}

func validaciones(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	esperarEstado(t, b.pagar("", item(claveCredito, "Due", 332500)), http.StatusBadRequest, "Canal.Pago.SinLlave")
	esperarEstado(t, b.pagar("llave-0001", item(claveCredito, "Due", 1)), http.StatusUnprocessableEntity, "Canal.Pago.ValorInvalido")
	esperarEstado(t, b.pagar("llave-0002", item(claveCredito, "Partial", 100)), http.StatusUnprocessableEntity, "Canal.Pago.ValorInvalido")
	esperarEstado(t, b.pagar("llave-0003", item(claveAporte, "Payoff", 50000)), http.StatusUnprocessableEntity, "Canal.Pago.ModalidadNoAdmitida")
	esperarEstado(t, b.pagar("llave-0004", item("LOAN:otro", "Due", 1000)), http.StatusUnprocessableEntity, "Canal.Pago.ConceptoNoDisponible")
	esperarEstado(t, b.pagar("llave-0005", item(claveCredito, "Lunar", 1000)), http.StatusBadRequest, "Canal.Pedido.Invalido")
	esperarEstado(t, b.pagar("llave-0006", item(claveCredito, "Due", 332500), item(claveCredito, "Due", 332500)),
		http.StatusBadRequest, "Canal.Pedido.Invalido")

	// Abono a capital: sin elección toma la preselección de la cooperativa.
	r := b.pagar("llave-0007", item(claveCredito, "Prepayment", 100000))
	esperarEstado(t, r, http.StatusCreated, "")
	if it := r.cuerpo["items"].([]any)[0].(map[string]any); it["prepaymentChoice"] != "ReduceTerm" {
		t.Fatalf("elección %v", it)
	}

	// Dos destinos no se pagan juntos (FR-032).
	otroDestino := uuid.New()
	b.erp.mu.Lock()
	b.erp.conceptos.Items[1].Destino = &otroDestino
	b.erp.mu.Unlock()
	b2 := b.pagar("llave-0008", item(claveAporte, "Due", 50000), item(claveCredito, "Partial", 30000))
	if b2.codigo() != "Canal.Pago.DestinosDistintos" && b2.codigo() != "Canal.Pago.Pendiente" {
		t.Fatalf("destinos distintos: %v", b2.cuerpo)
	}

	b.erp.mu.Lock()
	b.erp.caido = true
	b.erp.mu.Unlock()
	esperarEstado(t, b.pagar("llave-0009", item(claveAporte, "Due", 50000)), http.StatusServiceUnavailable, "Canal.Entidad.NoDisponible")
}

func textosYCodigo(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	b.erp.mu.Lock()
	b.erp.textos[0].Version = 2
	b.erp.mu.Unlock()
	r := b.pagar("llave-0001", item(claveAporte, "Due", 50000))
	esperarEstado(t, r, http.StatusConflict, "Canal.Pago.TextosPorAceptar")
	cuerpo := map[string]any{"items": []any{item(claveAporte, "Due", 50000)}, "acceptances": []any{map[string]any{"kind": "Terms", "version": 2}}}
	esperarEstado(t, b.pedir(http.MethodPost, "/api/v1/payments", "llave-0002", cuerpo), http.StatusCreated, "")
	ultimas, _ := b.q.LatestLegalAcceptances(context.Background(), gen.LatestLegalAcceptancesParams{AccountID: b.cuenta, TenantID: b.tenant.ID})
	for _, u := range ultimas {
		if u.Kind == "Terms" && u.Version != 2 {
			t.Fatalf("aceptación %v", ultimas)
		}
	}

	// La cooperativa exige código (FR-009).
	if _, err := b.pool.Exec(context.Background(), "UPDATE tenants SET require_otp_to_pay = true WHERE id = $1", b.tenant.ID); err != nil {
		t.Fatal(err)
	}
	esperarEstado(t, b.pagar("llave-0003", item(claveCredito, "Due", 332500)), http.StatusForbidden, "Canal.Pago.CodigoRequerido")
	desafio := uuid.New()
	b.codigos.validos[desafio] = true
	conCodigo := map[string]any{"items": []any{item(claveCredito, "Due", 332500)}, "otpChallengeId": desafio}
	esperarEstado(t, b.pedir(http.MethodPost, "/api/v1/payments", "llave-0004", conCodigo), http.StatusCreated, "")
}

func convergencia(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	r := b.pagar("llave-0001", item(claveCredito, "Due", 332500))
	id := r.texto("attemptId")
	rid := b.requestID(id)
	if err := b.sim.Fijar(rid, pasarela.EstadoAprobado); err != nil {
		t.Fatal(err)
	}

	// El retorno consulta la pasarela y no confía en la URL.
	ret := b.pedir(http.MethodGet, "/api/v1/payments/"+id+"?return=1", "", nil)
	esperarEstado(t, ret, http.StatusOK, "")
	ap, _ := ret.cuerpo["approval"].(map[string]any)
	if ret.texto("status") != "approved" || ap["authorization"] == "" || ret.cuerpo["processUrl"] != nil {
		t.Fatalf("retorno %v", ret.cuerpo)
	}

	// Después llega la notificación (firmada) y corre la sonda: nada se duplica.
	cuerpo, _ := b.sim.Notificacion(b.sitio, rid)
	req := httptest.NewRequest(http.MethodPost, "/webhooks/placetopay/"+b.sitio.ID.String(), bytes.NewReader(cuerpo))
	rec := httptest.NewRecorder()
	b.h.ServeHTTP(rec, req)
	if rec.Code != http.StatusOK {
		t.Fatalf("webhook %d", rec.Code)
	}
	// La aprobación del retorno encoló el aviso al ERP; la notificación, una consulta.
	var consultas []ArgsConsultarIntento
	for _, j := range b.cola.tomar() {
		if c, ok := j.(ArgsConsultarIntento); ok {
			consultas = append(consultas, c)
		}
	}
	if len(consultas) != 1 {
		t.Fatalf("la notificación debía encolar una consulta: %v", consultas)
	}
	w := &TrabajadorDeConsulta{Pagos: b.s}
	if err := w.Work(context.Background(), &river.Job[ArgsConsultarIntento]{Args: consultas[0]}); err != nil {
		t.Fatal(err)
	}
	b.reloj.avanzar(20 * time.Minute)
	if _, _, err := b.s.Sondear(context.Background()); err != nil {
		t.Fatal(err)
	}
	aprobados := 0
	for _, e := range b.eventos(id) {
		if e.Status == "approved" {
			aprobados++
			if e.Source != FuenteRetorno {
				t.Fatalf("la aprobación debía quedar con fuente retorno: %s", e.Source)
			}
		}
	}
	if aprobados != 1 {
		t.Fatalf("eventos de aprobación: %d", aprobados)
	}
	// Ya no está pendiente: el concepto se puede volver a pagar.
	esperarEstado(t, b.pagar("llave-0002", item(claveCredito, "Partial", 30000)), http.StatusCreated, "")
}

func vencimiento(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	r := b.pagar("llave-0001", item(claveAporte, "Due", 50000))
	id := r.texto("attemptId")
	b.reloj.avanzar(16 * time.Minute)
	g := b.pedir(http.MethodGet, "/api/v1/payments/"+id, "", nil)
	if g.texto("status") != "expired" {
		t.Fatalf("vencido %v", g.cuerpo)
	}
	// La pasarela aprueba después (caso borde): el retorno lo recoge.
	if err := b.sim.Fijar(b.requestID(id), pasarela.EstadoAprobado); err != nil {
		t.Fatal(err)
	}
	// La sonda no mira vencidos.
	if _, _, err := b.s.Sondear(context.Background()); err != nil {
		t.Fatal(err)
	}
	if a, _ := b.q.GetPaymentAttempt(context.Background(), uuid.MustParse(id)); a.Status != "expired" {
		t.Fatalf("la sonda tocó un vencido: %s", a.Status)
	}
	g = b.pedir(http.MethodGet, "/api/v1/payments/"+id+"?return=1", "", nil)
	if g.texto("status") != "approved" {
		t.Fatalf("vencido → aprobado: %v", g.cuerpo)
	}
	// Un estado final no vuelve atrás.
	_ = b.sim.Fijar(b.requestID(id), pasarela.EstadoRechazado)
	if g = b.pedir(http.MethodGet, "/api/v1/payments/"+id+"?return=1", "", nil); g.texto("status") != "approved" {
		t.Fatalf("un aprobado cambió: %v", g.cuerpo)
	}
	if !TransicionValida("expired", "approved") || TransicionValida("approved", "rejected") || TransicionValida("rejected", "approved") {
		t.Fatal("tabla de transiciones")
	}
}

func firmaInvalida(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	r := b.pagar("llave-0001", item(claveAporte, "Due", 50000))
	rid := b.requestID(r.texto("attemptId"))
	_ = b.sim.Fijar(rid, pasarela.EstadoAprobado)
	otro := b.sitio
	otro.Secreto = "otro-secreto"
	cuerpo, _ := b.sim.Notificacion(otro, rid)
	for i := 0; i < 2; i++ {
		rec := httptest.NewRecorder()
		b.h.ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/webhooks/placetopay/"+b.sitio.ID.String(), bytes.NewReader(cuerpo)))
		if rec.Code != http.StatusOK {
			t.Fatalf("firma inválida debe responder 200: %d", rec.Code)
		}
	}
	if len(b.cola.tomar()) != 0 {
		t.Fatal("una firma inválida encoló trabajo")
	}
	n, _ := b.q.CountRecentAuditForSubject(context.Background(), gen.CountRecentAuditForSubjectParams{Action: AccionFirmaInvalida,
		Subject: b.sitio.ID.String(), Since: time.Now().Add(-time.Hour)})
	if n != 2 {
		t.Fatalf("auditoría de firmas inválidas: %d", n)
	}
	if a, _ := b.q.GetPaymentAttempt(context.Background(), uuid.MustParse(r.texto("attemptId"))); a.Status != "pending" {
		t.Fatalf("el intento cambió: %s", a.Status)
	}
}

func pasarelaCaida(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	b.sim.Fallar = true
	esperarEstado(t, b.pagar("llave-0001", item(claveAporte, "Due", 50000)), http.StatusServiceUnavailable, "Canal.Pago.PasarelaNoDisponible")
	b.sim.Fallar = false
	// El intento fallido liberó el concepto.
	esperarEstado(t, b.pagar("llave-0002", item(claveAporte, "Due", 50000)), http.StatusCreated, "")
}

func sonda(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	// Sesión de 30 minutos: a los 16 sigue pendiente en la pasarela.
	if _, err := b.pool.Exec(context.Background(), "UPDATE tenants SET payment_session_minutes = 30 WHERE id = $1", b.tenant.ID); err != nil {
		t.Fatal(err)
	}
	r := b.pagar("llave-0001", item(claveCredito, "Due", 332500))
	id := uuid.MustParse(r.texto("attemptId"))
	// La sonda recorre todos los pendientes de la base (también los de otras subpruebas): se mira sólo este intento.
	consultado := func() *time.Time {
		a, err := b.q.GetPaymentAttempt(context.Background(), id)
		if err != nil {
			t.Fatal(err)
		}
		return a.LastCheckedAt
	}
	pasada := func() int {
		_, alertados, err := b.s.Sondear(context.Background())
		if err != nil {
			t.Fatal(err)
		}
		return alertados
	}
	// Nunca consultado: toca.
	pasada()
	primera := consultado()
	if primera == nil || !primera.Equal(b.reloj.ahora()) {
		t.Fatalf("primera pasada: %v", primera)
	}
	b.reloj.avanzar(5 * time.Minute)
	pasada()
	if c := consultado(); !c.Equal(*primera) {
		t.Fatalf("antes de 15 min no toca: %v", c)
	}
	b.reloj.avanzar(11 * time.Minute)
	pasada()
	if c := consultado(); !c.Equal(b.reloj.ahora()) {
		t.Fatalf("a los 16 min toca: %v", c)
	}

	// Un pendiente de más de 7 días se alerta una sola vez.
	if _, err := b.pool.Exec(context.Background(),
		"UPDATE payment_attempts SET created_at = $2, expires_at = $3 WHERE id = $1", id, b.reloj.ahora().Add(-8*24*time.Hour),
		b.reloj.ahora().Add(time.Hour)); err != nil {
		t.Fatal(err)
	}
	alertas := func() int64 {
		n, err := b.q.CountRecentAuditForSubject(context.Background(), gen.CountRecentAuditForSubjectParams{
			Action: "Canal.Pago.PendienteSinResolver", Subject: id.String(), Since: time.Now().Add(-time.Hour)})
		if err != nil {
			t.Fatal(err)
		}
		return n
	}
	pasada()
	pasada()
	if n := alertas(); n != 1 {
		t.Fatalf("alertas del intento: %d", n)
	}
}

func avisoAlERP(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	r := b.pagar("llave-0001", map[string]any{"conceptKey": claveCredito, "modality": "Prepayment", "amount": 100000, "prepaymentChoice": "ReduceInstallment"})
	id := r.texto("attemptId")
	if err := b.sim.Fijar(b.requestID(id), pasarela.EstadoAprobado); err != nil {
		t.Fatal(err)
	}
	b.cola.tomar()
	_ = b.pedir(http.MethodGet, "/api/v1/payments/"+id+"?return=1", "", nil)

	// La aprobación encola el aviso en su misma transacción y deja el intento «enviando».
	var avisos []ArgsAvisarPago
	for _, j := range b.cola.tomar() {
		if a, ok := j.(ArgsAvisarPago); ok {
			avisos = append(avisos, a)
		}
	}
	if len(avisos) != 1 || avisos[0].Intento.String() != id {
		t.Fatalf("aviso encolado: %v", avisos)
	}
	if a, _ := b.q.GetPaymentAttempt(context.Background(), uuid.MustParse(id)); a.ErpOutcome != AvisoEnviando {
		t.Fatalf("resultado del ERP: %s", a.ErpOutcome)
	}
	// Sin comprobante todavía.
	if g := b.pedir(http.MethodGet, "/api/v1/payments/"+id+"/receipt", "", nil); g.codigo() != "Canal.Pago.SinComprobante" {
		t.Fatalf("comprobante antes del aviso: %v", g.cuerpo)
	}

	// ERP caído: queda reintentando y el trabajo devuelve error (River reintenta).
	b.aviso.err = erp.ErrERPNoDisponible
	w := &TrabajadorDeAviso{Pagos: b.s}
	if err := w.Work(context.Background(), &river.Job[ArgsAvisarPago]{Args: avisos[0], JobRow: &rivertype.JobRow{Attempt: 1}}); err == nil {
		t.Fatal("con el ERP caído el trabajo debe fallar para reintentar")
	}
	if a, _ := b.q.GetPaymentAttempt(context.Background(), uuid.MustParse(id)); a.ErpOutcome != AvisoReintentando {
		t.Fatalf("resultado tras la caída: %s", a.ErpOutcome)
	}

	// El ERP aplica: guarda el recibo; un aviso repetido ya no se envía.
	recibo := uuid.New()
	b.aviso.err = nil
	b.aviso.resultado = erp.ResultadoDelAviso{Resultado: "Applied", Recibo: &recibo}
	if err := w.Work(context.Background(), &river.Job[ArgsAvisarPago]{Args: avisos[0], JobRow: &rivertype.JobRow{Attempt: 2}}); err != nil {
		t.Fatal(err)
	}
	if err := b.s.Avisar(context.Background(), uuid.MustParse(id)); err != nil {
		t.Fatal(err)
	}
	if len(b.aviso.avisos) != 2 {
		t.Fatalf("avisos enviados: %d", len(b.aviso.avisos))
	}
	enviado := b.aviso.avisos[1]
	if enviado.Referencia != r.texto("reference") || enviado.Total != 100000 || enviado.Moneda != "COP" || enviado.Pasarela.Autorizacion == "" ||
		len(enviado.Items) != 1 || enviado.Items[0].Modalidad != "Prepayment" || enviado.Items[0].Eleccion == nil ||
		*enviado.Items[0].Eleccion != "ReduceInstallment" || enviado.Destino != b.destino || enviado.Pagador.IP == nil {
		t.Fatalf("aviso: %+v", enviado)
	}
	g := b.pedir(http.MethodGet, "/api/v1/payments/"+id, "", nil)
	if g.texto("erpOutcome") != AvisoAplicado || g.texto("erpReceiptPublicId") != recibo.String() {
		t.Fatalf("intento tras el aviso: %v", g.cuerpo)
	}
	req := httptest.NewRequest(http.MethodGet, "/api/v1/payments/"+id+"/receipt", nil)
	rec := httptest.NewRecorder()
	b.h.ServeHTTP(rec, req)
	if rec.Code != http.StatusOK || rec.Header().Get("Content-Type") != "application/pdf" || !strings.HasPrefix(rec.Body.String(), "%PDF") {
		t.Fatalf("comprobante: %d %s", rec.Code, rec.Header())
	}

	// Un 409 del ERP (total que no cuadra) no se reintenta: se cancela y se alerta.
	r2 := b.pagar("llave-0002", item(claveAporte, "Due", 50000))
	id2 := r2.texto("attemptId")
	_ = b.sim.Fijar(b.requestID(id2), pasarela.EstadoAprobado)
	_ = b.pedir(http.MethodGet, "/api/v1/payments/"+id2+"?return=1", "", nil)
	b.aviso.err = &erp.ErrorDelERP{Estado: http.StatusConflict, Codigo: "Channel.Notice.TotalMismatch", Mensaje: "no cuadra"}
	err := w.Work(context.Background(), &river.Job[ArgsAvisarPago]{Args: ArgsAvisarPago{Intento: uuid.MustParse(id2)}, JobRow: &rivertype.JobRow{Attempt: 1}})
	var cancelado *rivertype.JobCancelError
	if !errors.As(err, &cancelado) {
		t.Fatalf("un 409 debe cancelar el trabajo: %v", err)
	}
	n, _ := b.q.CountRecentAuditForSubject(context.Background(), gen.CountRecentAuditForSubjectParams{Action: AccionAvisoSinAplicar, Subject: id2,
		Since: time.Now().Add(-time.Hour)})
	if n != 1 {
		t.Fatalf("alerta del aviso: %d", n)
	}
}
