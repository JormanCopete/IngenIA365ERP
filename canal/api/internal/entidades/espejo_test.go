package entidades

import (
	"cmp"
	"context"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"encoding/json"
	"errors"
	"net/http"
	"net/http/httptest"
	"net/url"
	"slices"
	"strconv"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/trabajos"
)

// base es un ChangeSequence realista: ticks UTC de noviembre de 2026.
const base int64 = 639_000_000_000_000_000

// erpDeHabilitaciones imita GET …/enablements: una fila vigente por persona, listada por secuencia creciente.
type erpDeHabilitaciones struct {
	mu           sync.Mutex
	filas        map[uuid.UUID]map[uuid.UUID]erp.CambioDeHabilitacion // cooperativa → persona → fila
	fallar       map[uuid.UUID]error
	conciliar    error
	llamadas     []string
	limite       int // si no es 0, reemplaza el limit pedido (para forzar varias páginas)
	desdesVistos []int64
}

func nuevoERP() *erpDeHabilitaciones {
	return &erpDeHabilitaciones{filas: map[uuid.UUID]map[uuid.UUID]erp.CambioDeHabilitacion{}, fallar: map[uuid.UUID]error{}}
}

func (f *erpDeHabilitaciones) poner(coop uuid.UUID, c erp.CambioDeHabilitacion) {
	f.mu.Lock()
	defer f.mu.Unlock()
	if f.filas[coop] == nil {
		f.filas[coop] = map[uuid.UUID]erp.CambioDeHabilitacion{}
	}
	f.filas[coop][c.PersonPublicID] = c
}

func (f *erpDeHabilitaciones) quitar(coop, persona uuid.UUID) {
	f.mu.Lock()
	defer f.mu.Unlock()
	delete(f.filas[coop], persona)
}

func (f *erpDeHabilitaciones) ConciliarHabilitaciones(_ context.Context, coop uuid.UUID) (erp.ConciliacionDeHabilitaciones, error) {
	f.mu.Lock()
	defer f.mu.Unlock()
	f.llamadas = append(f.llamadas, "reconcile:"+coop.String())
	return erp.ConciliacionDeHabilitaciones{}, f.conciliar
}

func (f *erpDeHabilitaciones) ListarCambiosDeHabilitacion(_ context.Context, coop uuid.UUID, desde int64, limite int) (erp.PaginaDeHabilitaciones, error) {
	f.mu.Lock()
	defer f.mu.Unlock()
	f.llamadas = append(f.llamadas, "list:"+coop.String())
	f.desdesVistos = append(f.desdesVistos, desde)
	if err := f.fallar[coop]; err != nil {
		return erp.PaginaDeHabilitaciones{}, err
	}
	if f.limite > 0 {
		limite = f.limite
	}
	var posteriores []erp.CambioDeHabilitacion
	for _, c := range f.filas[coop] {
		if c.Sequence > desde {
			posteriores = append(posteriores, c)
		}
	}
	slices.SortFunc(posteriores, func(a, b erp.CambioDeHabilitacion) int { return cmp.Compare(a.Sequence, b.Sequence) })
	pagina := erp.PaginaDeHabilitaciones{Items: posteriores, Next: desde}
	if len(posteriores) > limite {
		pagina.Items, pagina.HasMore = posteriores[:limite], true
	}
	if n := len(pagina.Items); n > 0 {
		pagina.Next = pagina.Items[n-1].Sequence
	}
	return pagina, nil
}

var siguienteCodigo atomic.Int32

// cooperativa crea una cooperativa en el directorio con un código único.
func cooperativa(t *testing.T, q *gen.Queries) gen.Tenant {
	t.Helper()
	n := siguienteCodigo.Add(1)
	codigo := string([]byte{'Q', byte('A' + n/26%26), byte('A' + n%26)})
	c, err := q.UpsertTenant(context.Background(), gen.UpsertTenantParams{
		ID: uuid.Must(uuid.NewV7()), TenantPublicID: uuid.New(), ChannelCode: codigo, DisplayName: "Cooperativa " + codigo,
		Brand: json.RawMessage(`{}`), Support: json.RawMessage(`{}`), InactivityMinutes: 5, LastSyncAt: time.Now(),
	})
	if err != nil {
		t.Fatalf("crear la cooperativa: %v", err)
	}
	return c
}

func habilitada(persona uuid.UUID, numero string, seq int64) erp.CambioDeHabilitacion {
	return erp.CambioDeHabilitacion{
		PersonPublicID: persona, DocumentType: "CC", DocumentNumber: numero, Status: "Enabled",
		EnabledAt: time.Date(2026, 11, 1, 13, 0, 0, 0, time.UTC), Sequence: seq,
	}
}

func espejoDe(q *gen.Queries, c gen.Tenant) map[uuid.UUID]gen.Enablement {
	filas, err := q.ListEnablementsByTenant(context.Background(), c.ID)
	if err != nil {
		panic(err)
	}
	m := map[uuid.UUID]gen.Enablement{}
	for _, f := range filas {
		m[f.PersonPublicID] = f
	}
	return m
}

// releer trae la cooperativa con su cursor actual (como lo haría el trabajo periódico).
func releer(t *testing.T, q *gen.Queries, c gen.Tenant) gen.Tenant {
	t.Helper()
	fila, err := q.GetTenantByPublicID(context.Background(), c.TenantPublicID)
	if err != nil {
		t.Fatal(err)
	}
	return fila
}

func TestEspejo(t *testing.T) {
	pool := dbtest.Migrada(t)
	t.Run("cursor, traslape e idempotencia", func(t *testing.T) { cursorTraslapeEIdempotencia(t, pool) })
	t.Run("commit fuera de orden y resincronización", func(t *testing.T) { commitFueraDeOrden(t, pool) })
	t.Run("inhabilitación reflejada y cambio viejo ignorado", func(t *testing.T) { inhabilitacionReflejada(t, pool) })
	t.Run("filas ilegibles", func(t *testing.T) { filasIlegibles(t, pool) })
	t.Run("una cooperativa que falla no detiene a las demás", func(t *testing.T) { unaQueFalla(t, pool) })
	t.Run("conciliación antes del arrastre y con límite", func(t *testing.T) { conciliacionAntesYConLimite(t, pool) })
	t.Run("los trabajos se registran y encolan en River", func(t *testing.T) { trabajosEnRiver(t, pool) })
	t.Run("arrastre a pedido", func(t *testing.T) { arrastreAPedido(t, pool) })
}

func cursorTraslapeEIdempotencia(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	q := gen.New(pool)
	c := cooperativa(t, q)
	fuente := nuevoERP()
	fuente.limite = 2 // tres filas: dos páginas
	p1, p2, p3 := uuid.New(), uuid.New(), uuid.New()
	fuente.poner(c.TenantPublicID, habilitada(p1, "1010", base+1))
	fuente.poner(c.TenantPublicID, habilitada(p2, "2020", base+2))
	fuente.poner(c.TenantPublicID, habilitada(p3, "3030", base+3))
	espejo := NuevoEspejo(pool, fuente, time.Hour, dbtest.Silencioso())

	res, err := espejo.ArrastrarCooperativa(ctx, c)
	if err != nil {
		t.Fatalf("primer arrastre: %v", err)
	}
	if res.Paginas != 2 || res.Aplicados != 3 || res.Cursor != base+3 {
		t.Fatalf("resultado inesperado: %+v", res)
	}
	if fuente.desdesVistos[0] != 0 || fuente.desdesVistos[1] != base+2 {
		t.Fatalf("since pedidos: %v", fuente.desdesVistos)
	}
	c = releer(t, q, c)
	if c.EnablementsCursor != base+3 {
		t.Fatalf("cursor guardado = %d", c.EnablementsCursor)
	}

	// Repetir pide desde el cursor menos el traslape, recibe lo mismo y no duplica ni cambia nada.
	antes := espejoDe(q, c)
	fuente.desdesVistos = nil
	res, err = espejo.ArrastrarCooperativa(ctx, c)
	if err != nil {
		t.Fatalf("segundo arrastre: %v", err)
	}
	if fuente.desdesVistos[0] != base+3-TraslapeDelCursor {
		t.Fatalf("since con traslape = %d, se esperaba %d", fuente.desdesVistos[0], base+3-TraslapeDelCursor)
	}
	if res.Recibidos != 3 || res.Cursor != base+3 {
		t.Fatalf("resultado inesperado: %+v", res)
	}
	despues := espejoDe(q, c)
	if len(despues) != 3 {
		t.Fatalf("filas = %d, se esperaban 3", len(despues))
	}
	for p, a := range antes {
		d := despues[p]
		if d.ID != a.ID || d.Status != a.Status || d.DocumentNumber != a.DocumentNumber || d.ChangeSequence != a.ChangeSequence {
			t.Fatalf("la fila de %s cambió: %+v → %+v", p, a, d)
		}
	}
	if f := despues[p1]; f.Status != "enabled" || f.DocumentType != "CC" || f.DocumentNumber != "1010" ||
		!f.EnabledAt.Equal(time.Date(2026, 11, 1, 13, 0, 0, 0, time.UTC)) {
		t.Fatalf("fila mal reflejada: %+v", f)
	}
}

func commitFueraDeOrden(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	q := gen.New(pool)
	c := cooperativa(t, q)
	fuente := nuevoERP()
	a, b := uuid.New(), uuid.New()
	fuente.poner(c.TenantPublicID, habilitada(a, "1111", base+100))
	fuente.poner(c.TenantPublicID, habilitada(b, "2222", base+200))
	espejo := NuevoEspejo(pool, fuente, time.Hour, dbtest.Silencioso())
	if _, err := espejo.ArrastrarCooperativa(ctx, c); err != nil {
		t.Fatal(err)
	}

	// Una transacción del ERP tomó su secuencia antes que b pero confirmó después: el traslape la alcanza.
	tarde := uuid.New()
	fuente.poner(c.TenantPublicID, habilitada(tarde, "3333", base+150))
	// Otra confirmó más de 5 minutos tarde: queda fuera del traslape; la cubre la resincronización.
	muyTarde := uuid.New()
	fuente.poner(c.TenantPublicID, habilitada(muyTarde, "4444", base+200-TraslapeDelCursor-1))
	// Y b desaparece del ERP (p. ej. su fila se dio de baja): sólo la resincronización lo nota.
	fuente.quitar(c.TenantPublicID, b)

	c = releer(t, q, c)
	if _, err := espejo.ArrastrarCooperativa(ctx, c); err != nil {
		t.Fatal(err)
	}
	espejo1 := espejoDe(q, c)
	if _, ok := espejo1[tarde]; !ok {
		t.Fatalf("el commit fuera de orden dentro del traslape no se reflejó")
	}
	if _, ok := espejo1[muyTarde]; ok {
		t.Fatalf("un commit fuera del traslape no debía verse en el arrastre incremental")
	}

	c = releer(t, q, c)
	res, err := espejo.Resincronizar(ctx, c)
	if err != nil {
		t.Fatalf("resincronizar: %v", err)
	}
	if res.Inhabilitados != 1 {
		t.Fatalf("inhabilitados = %d, se esperaba 1: %+v", res.Inhabilitados, res)
	}
	espejo2 := espejoDe(q, c)
	if len(espejo2) != 4 {
		t.Fatalf("filas = %d, se esperaban 4 (ninguna borrada)", len(espejo2))
	}
	if espejo2[b].Status != "disabled" {
		t.Fatalf("la persona que el ERP ya no lista debía quedar inhabilitada: %+v", espejo2[b])
	}
	if espejo2[muyTarde].Status != "enabled" || espejo2[a].Status != "enabled" || espejo2[tarde].Status != "enabled" {
		t.Fatalf("las listadas debían seguir habilitadas: %+v", espejo2)
	}
	if releer(t, q, c).EnablementsCursor != base+200 {
		t.Fatalf("la resincronización no debía mover el cursor hacia atrás")
	}

	// Una resincronización con el ERP caído no inhabilita nada.
	fuente.fallar[c.TenantPublicID] = errors.New("ERP caído")
	fuente.quitar(c.TenantPublicID, a)
	if _, err := espejo.Resincronizar(ctx, releer(t, q, c)); err == nil {
		t.Fatalf("se esperaba el error del ERP")
	}
	if espejoDe(q, c)[a].Status != "enabled" {
		t.Fatalf("con el listado incompleto no se inhabilita nada")
	}
}

func inhabilitacionReflejada(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	q := gen.New(pool)
	c := cooperativa(t, q)
	fuente := nuevoERP()
	p := uuid.New()
	fuente.poner(c.TenantPublicID, habilitada(p, "5555", base+10))
	espejo := NuevoEspejo(pool, fuente, time.Hour, dbtest.Silencioso())
	if _, err := espejo.ArrastrarCooperativa(ctx, c); err != nil {
		t.Fatal(err)
	}

	inhabilitada := habilitada(p, "5555", base+20)
	inhabilitada.Status = "Disabled"
	fuente.poner(c.TenantPublicID, inhabilitada)
	if _, err := espejo.ArrastrarCooperativa(ctx, releer(t, q, c)); err != nil {
		t.Fatal(err)
	}
	if f := espejoDe(q, c)[p]; f.Status != "disabled" || f.ChangeSequence != base+20 {
		t.Fatalf("la inhabilitación no se reflejó: %+v", f)
	}

	// Un cambio más viejo (otra réplica, traslape) que llega después no pisa al nuevo.
	fuente.poner(c.TenantPublicID, habilitada(p, "5555", base+15))
	res, err := espejo.ArrastrarCooperativa(ctx, releer(t, q, c))
	if err != nil {
		t.Fatal(err)
	}
	if res.Viejos != 1 || res.Aplicados != 0 {
		t.Fatalf("el cambio viejo debía ignorarse: %+v", res)
	}
	if f := espejoDe(q, c)[p]; f.Status != "disabled" {
		t.Fatalf("un cambio viejo pisó la inhabilitación: %+v", f)
	}

	// Rehabilitar con una secuencia nueva sí se refleja.
	fuente.poner(c.TenantPublicID, habilitada(p, "5555", base+30))
	if _, err := espejo.ArrastrarCooperativa(ctx, releer(t, q, c)); err != nil {
		t.Fatal(err)
	}
	if f := espejoDe(q, c)[p]; f.Status != "enabled" {
		t.Fatalf("la rehabilitación no se reflejó: %+v", f)
	}
}

func filasIlegibles(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	q := gen.New(pool)
	c := cooperativa(t, q)
	fuente := nuevoERP()
	buena, rara, conDocumentoRoto := uuid.New(), uuid.New(), uuid.New()
	fuente.poner(c.TenantPublicID, habilitada(conDocumentoRoto, "7777", base+1))
	espejo := NuevoEspejo(pool, fuente, time.Hour, dbtest.Silencioso())
	if _, err := espejo.ArrastrarCooperativa(ctx, c); err != nil {
		t.Fatal(err)
	}

	fuente.poner(c.TenantPublicID, habilitada(buena, " ab12 ", base+2)) // se normaliza a AB12
	tipoRaro := habilitada(rara, "8888", base+3)
	tipoRaro.DocumentType = "XX"
	fuente.poner(c.TenantPublicID, tipoRaro)
	roto := habilitada(conDocumentoRoto, "", base+4)
	roto.Status = "Disabled"
	fuente.poner(c.TenantPublicID, roto)

	res, err := espejo.ArrastrarCooperativa(ctx, releer(t, q, c))
	if err != nil {
		t.Fatal(err)
	}
	if res.Rechazados != 1 || res.Cursor != base+4 {
		t.Fatalf("resultado inesperado: %+v", res)
	}
	filas := espejoDe(q, c)
	if _, ok := filas[rara]; ok {
		t.Fatalf("un tipo fuera del catálogo no debía reflejarse")
	}
	if filas[buena].DocumentNumber != "AB12" {
		t.Fatalf("número sin normalizar: %+v", filas[buena])
	}
	if f := filas[conDocumentoRoto]; f.Status != "disabled" || f.DocumentNumber != "7777" {
		t.Fatalf("la inhabilitación con documento ilegible debía reflejar sólo el estado: %+v", f)
	}
}

func unaQueFalla(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	q := gen.New(pool)
	mala, buena := cooperativa(t, q), cooperativa(t, q)
	fuente := nuevoERP()
	fuente.fallar[mala.TenantPublicID] = errors.New("ERP de la cooperativa caído")
	p := uuid.New()
	fuente.poner(buena.TenantPublicID, habilitada(p, "9999", base+1))
	espejo := NuevoEspejo(pool, fuente, time.Hour, dbtest.Silencioso())

	err := espejo.ArrastrarTodas(ctx)
	if err == nil || !strings.Contains(err.Error(), mala.TenantPublicID.String()) {
		t.Fatalf("se esperaba el error de la cooperativa caída, llegó %v", err)
	}
	if strings.Contains(err.Error(), buena.TenantPublicID.String()) {
		t.Fatalf("la cooperativa sana no debía figurar en el error: %v", err)
	}
	if _, ok := espejoDe(q, buena)[p]; !ok {
		t.Fatalf("la cooperativa sana debía arrastrarse aunque otra falle")
	}

	// El trabajo de River devuelve el error para reintentar.
	trabajador := &TrabajadorDeHabilitaciones{Espejo: espejo}
	if err := trabajador.Work(ctx, &river.Job[ArgsArrastrarHabilitaciones]{}); err == nil {
		t.Fatalf("el trabajo debía fallar para que River reintente")
	}
}

// erpHTTP es un ERP de prueba por HTTP: registra el orden de las llamadas y responde como /api/canal/v1.
type erpHTTP struct {
	mu                sync.Mutex
	llamadas          []string
	conciliacionFalla bool
	items             []map[string]any
}

func (e *erpHTTP) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	reclamos := &jwt.RegisteredClaims{}
	crudo := strings.TrimPrefix(r.Header.Get("Authorization"), "Bearer ")
	if _, _, err := jwt.NewParser().ParseUnverified(crudo, reclamos); err != nil || !strings.Contains(r.URL.Path, "/tenants/"+reclamos.Subject+"/") {
		w.WriteHeader(http.StatusForbidden)
		return
	}
	e.mu.Lock()
	defer e.mu.Unlock()
	w.Header().Set("Content-Type", "application/json")
	switch {
	case r.Method == http.MethodPost && strings.HasSuffix(r.URL.Path, "/enablements/reconcile"):
		e.llamadas = append(e.llamadas, "reconcile")
		if e.conciliacionFalla {
			w.WriteHeader(http.StatusConflict)
			_, _ = w.Write([]byte(`{"code":"Channel.Disabled","message":"La cooperativa no tiene encendido el canal digital."}`))
			return
		}
		_, _ = w.Write([]byte(`{"enabled":2,"disabled":1}`))
	case r.Method == http.MethodGet && strings.HasSuffix(r.URL.Path, "/enablements"):
		e.llamadas = append(e.llamadas, "list?since="+r.URL.Query().Get("since")+"&limit="+r.URL.Query().Get("limit"))
		_ = json.NewEncoder(w).Encode(map[string]any{"items": e.items, "next": 0, "hasMore": false})
	default:
		w.WriteHeader(http.StatusNotFound)
	}
}

func (e *erpHTTP) tomarLlamadas() []string {
	e.mu.Lock()
	defer e.mu.Unlock()
	l := e.llamadas
	e.llamadas = nil
	return l
}

func conciliacionAntesYConLimite(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	q := gen.New(pool)
	c := cooperativa(t, q)
	persona := uuid.New()
	falso := &erpHTTP{items: []map[string]any{{
		"personPublicId": persona, "documentType": "CE", "documentNumber": "987654", "status": "Enabled",
		"enabledAt": "2026-11-02T15:04:05Z", "sequence": base + 7,
	}}}
	servidor := httptest.NewServer(falso)
	defer servidor.Close()
	baseURL, _ := url.Parse(servidor.URL)
	cliente := erp.NuevoCliente(baseURL, llaveDePrueba(t), "canal1", servidor.Client())

	ahora := time.Date(2026, 11, 8, 12, 0, 0, 0, time.UTC)
	espejo := NuevoEspejo(pool, cliente, 5*time.Minute, dbtest.Silencioso())
	espejo.ahora = func() time.Time { return ahora }

	res, err := espejo.ArrastrarCooperativa(ctx, c)
	if err != nil {
		t.Fatalf("arrastre: %v", err)
	}
	if got := falso.tomarLlamadas(); !slices.Equal(got, []string{"reconcile", "list?since=0&limit=500"}) || !res.Conciliada {
		t.Fatalf("la conciliación debía ir antes del arrastre: %v (%+v)", got, res)
	}
	if f := espejoDe(q, c)[persona]; f.Status != "enabled" || f.DocumentType != "CE" || f.ChangeSequence != base+7 ||
		!f.EnabledAt.Equal(time.Date(2026, 11, 2, 15, 4, 5, 0, time.UTC)) {
		t.Fatalf("la fila del ERP no se reflejó: %+v", f)
	}

	// Dentro del intervalo no se vuelve a conciliar, aunque sea otra réplica (el turno vive en la base).
	ahora = ahora.Add(4 * time.Minute)
	otraReplica := NuevoEspejo(pool, cliente, 5*time.Minute, dbtest.Silencioso())
	otraReplica.ahora = func() time.Time { return ahora }
	if _, err := otraReplica.ArrastrarCooperativa(ctx, releer(t, q, c)); err != nil {
		t.Fatal(err)
	}
	desde := strconv.FormatInt(base+7-TraslapeDelCursor, 10)
	if got := falso.tomarLlamadas(); !slices.Equal(got, []string{"list?since=" + desde + "&limit=500"}) {
		t.Fatalf("dentro del intervalo sólo debía arrastrar: %v", got)
	}

	// Cumplido el intervalo, concilia otra vez; si la conciliación falla, se arrastra igual.
	ahora = ahora.Add(time.Minute)
	falso.mu.Lock()
	falso.conciliacionFalla = true // bajo el candado: lo lee la goroutina del servidor de prueba
	falso.mu.Unlock()
	res, err = espejo.ArrastrarCooperativa(ctx, releer(t, q, c))
	if err != nil {
		t.Fatalf("una conciliación fallida no debía impedir el arrastre: %v", err)
	}
	if got := falso.tomarLlamadas(); !slices.Equal(got, []string{"reconcile", "list?since=" + desde + "&limit=500"}) || res.Conciliada {
		t.Fatalf("debía intentar conciliar y arrastrar: %v (%+v)", got, res)
	}
}

func trabajosEnRiver(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	if err := trabajos.MigrarRiver(ctx, pool); err != nil {
		t.Fatal(err)
	}
	espejo := NuevoEspejo(pool, nuevoERP(), time.Hour, dbtest.Silencioso())
	workers := river.NewWorkers()
	river.AddWorker(workers, &TrabajadorDeHabilitaciones{Espejo: espejo})
	river.AddWorker(workers, &TrabajadorDeResincronizacion{Espejo: espejo})
	river.AddWorker(workers, &TrabajadorDeCooperativa{Espejo: espejo})
	cliente, err := trabajos.NuevoCliente(pool, trabajos.Opciones{
		Logger: dbtest.Silencioso(), Workers: workers,
		Periodicos: []trabajos.Periodico{
			{ID: "pull_enablements", Intervalo: time.Minute, Args: func() river.JobArgs { return ArgsArrastrarHabilitaciones{} }},
			{ID: "full_resync", Horario: trabajos.Diario(3, 0, trabajos.HoraDeColombia), Args: func() river.JobArgs { return ArgsResincronizarHabilitaciones{} }},
		},
	})
	if err != nil {
		t.Fatalf("crear el cliente de River con los trabajos del espejo: %v", err)
	}
	coop := uuid.New()
	res, err := cliente.Insert(ctx, ArgsArrastrarCooperativa{TenantPublicID: coop}, nil)
	if err != nil {
		t.Fatalf("encolar el arrastre a pedido: %v", err)
	}
	if res.Job.Kind != "pull_tenant_enablements" || res.Job.MaxAttempts != 5 || !strings.Contains(string(res.Job.EncodedArgs), coop.String()) {
		t.Fatalf("trabajo inesperado: %s %d %s", res.Job.Kind, res.Job.MaxAttempts, res.Job.EncodedArgs)
	}
}

// Va al final de TestEspejo: el directorio de prueba sólo lista una cooperativa e inhabilita las de las demás pruebas.
func arrastreAPedido(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	q := gen.New(pool)
	fuente := nuevoERP()
	espejo := NuevoEspejo(pool, fuente, time.Hour, dbtest.Silencioso())

	// La cooperativa todavía no está en el directorio: el trabajo lo sincroniza primero.
	nueva := uuid.New()
	p := uuid.New()
	fuente.poner(nueva, habilitada(p, "1212", base+1))
	directorio := NuevoDirectorio(q, &fuenteFija{cooperativas: []erp.Cooperativa{
		{TenantPublicID: nueva, ChannelCode: cadena("ZQZ"), DisplayName: "Recién encendida", InactivityMinutes: 5},
	}}, dbtest.Silencioso())
	trabajador := &TrabajadorDeCooperativa{Espejo: espejo, Directorio: directorio}
	if err := trabajador.Work(ctx, &river.Job[ArgsArrastrarCooperativa]{Args: ArgsArrastrarCooperativa{TenantPublicID: nueva}}); err != nil {
		t.Fatalf("arrastre a pedido: %v", err)
	}
	fila, err := q.GetTenantByPublicID(ctx, nueva)
	if err != nil {
		t.Fatal(err)
	}
	if _, ok := espejoDe(q, fila)[p]; !ok {
		t.Fatalf("el arrastre a pedido no reflejó la habilitación")
	}

	// Una cooperativa que el ERP no lista: se cancela (reintentar no lo arregla).
	err = trabajador.Work(ctx, &river.Job[ArgsArrastrarCooperativa]{Args: ArgsArrastrarCooperativa{TenantPublicID: uuid.New()}})
	var cancelado *river.JobCancelError
	if !errors.As(err, &cancelado) {
		t.Fatalf("se esperaba la cancelación del trabajo, llegó %v", err)
	}
}

func llaveDePrueba(t *testing.T) *ecdsa.PrivateKey {
	t.Helper()
	llave, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		t.Fatalf("generar llave: %v", err)
	}
	return llave
}
