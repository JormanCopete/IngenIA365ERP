package identidad

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"regexp"
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
	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria"
)

// T072: identidad contra PostgreSQL real. Un solo contenedor para todas las subpruebas; cada una usa sus documentos.

func TestIdentidad(t *testing.T) {
	pool := dbtest.Migrada(t)
	t.Run("activación completa y sesión", func(t *testing.T) { activacionCompleta(t, pool) })
	t.Run("enumeración imposible", func(t *testing.T) { enumeracionImposible(t, pool) })
	t.Run("sin datos de contacto: acércate a tu entidad", func(t *testing.T) { sinDatosDeContacto(t, pool) })
	t.Run("bloqueo por fallos", func(t *testing.T) { bloqueoPorFallos(t, pool) })
	t.Run("vencimiento e intentos de los códigos", func(t *testing.T) { vencimientoDeCodigos(t, pool) })
	t.Run("inactividad y máximo de 8 horas", func(t *testing.T) { inactividadYMaximo(t, pool) })
	t.Run("selector con 0, 1 y 2 cooperativas y acotamiento", func(t *testing.T) { selectorYAcotamiento(t, pool) })
	t.Run("dispositivo nuevo y de confianza", func(t *testing.T) { dispositivoNuevo(t, pool) })
	t.Run("recuperación de clave", func(t *testing.T) { recuperacion(t, pool) })
	t.Run("cambio de contacto", func(t *testing.T) { cambioDeContacto(t, pool) })
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

type erpFalso struct {
	mu        sync.Mutex
	contactos map[uuid.UUID]erp.ContactoDeActivacion
	lecturas  atomic.Int32
}

func (e *erpFalso) ContactoDeActivacion(_ context.Context, _, persona uuid.UUID) (erp.ContactoDeActivacion, error) {
	e.lecturas.Add(1)
	e.mu.Lock()
	defer e.mu.Unlock()
	c, ok := e.contactos[persona]
	if !ok {
		return erp.ContactoDeActivacion{}, &erp.ErrorDelERP{Estado: 404, Codigo: "Channel.Person.NotEnabled"}
	}
	return c, nil
}

func (e *erpFalso) TextosLegales(context.Context, uuid.UUID) ([]erp.TextoLegal, error) {
	return []erp.TextoLegal{
		{Tipo: "Terms", Version: 2, Contenido: "Términos"},
		{Tipo: "DataPolicy", Version: 1, Contenido: "Política"},
		{Tipo: "Faq", Version: 1, Contenido: "Preguntas"},
	}, nil
}

func (e *erpFalso) poner(persona uuid.UUID, correo, celular string) {
	e.mu.Lock()
	defer e.mu.Unlock()
	c := erp.ContactoDeActivacion{Nombres: "Ana María", Apellidos: "Prueba"}
	if correo != "" {
		c.Entrega.Correo = &correo
		c.Correo.Disponible = true
	}
	if celular != "" {
		c.Entrega.Celular = &celular
		c.Celular.Disponible = true
	}
	e.contactos[persona] = c
}

type encoladorFalso struct {
	mu   sync.Mutex
	jobs []river.JobArgs
}

func (e *encoladorFalso) InsertTx(_ context.Context, _ pgx.Tx, args river.JobArgs, _ *river.InsertOpts) (*rivertype.JobInsertResult, error) {
	e.mu.Lock()
	defer e.mu.Unlock()
	e.jobs = append(e.jobs, args)
	return &rivertype.JobInsertResult{}, nil
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
	correo  *mensajeria.Memoria
	cola    *encoladorFalso
	cookies map[string]*http.Cookie
}

var numeroDePrueba atomic.Int64

func nuevoBanco(t *testing.T, pool *pgxpool.Pool, minimo time.Duration) *banco {
	t.Helper()
	b := &banco{t: t, pool: pool, q: gen.New(pool), reloj: &reloj{t: time.Now().UTC().Truncate(time.Second)},
		erp: &erpFalso{contactos: map[uuid.UUID]erp.ContactoDeActivacion{}}, correo: &mensajeria.Memoria{}, cola: &encoladorFalso{},
		cookies: map[string]*http.Cookie{}}
	s, err := Nuevo(Opciones{
		Pool: pool, ERP: b.erp, Mensajero: mensajeria.Compuesto{PorCorreo: b.correo}, Auditoria: auditoria.Nuevo(pool),
		Encolador: b.cola, Logger: dbtest.Silencioso(), ClaveDeSenuelos: bytes.Repeat([]byte{7}, 32), CookiesSeguras: true,
		TiempoMinimo: minimo, Ahora: b.reloj.ahora,
	})
	if err != nil {
		t.Fatal(err)
	}
	b.s = s
	m := mux{http.NewServeMux()}
	s.Registrar(m)
	m.Manejar("GET /api/v1/prueba/acotada", s.Acotada(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		coop, _ := contexto.CooperativaDe(r.Context())
		persona, _ := contexto.Persona(r.Context())
		_ = json.NewEncoder(w).Encode(map[string]any{"tenant": coop.PublicID, "person": persona})
	})))
	ip := fmt.Sprintf("203.0.113.%d", numeroDePrueba.Add(1)%250)
	b.h = http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ctx := contexto.ConIP(r.Context(), ip)
		ctx = contexto.ConAgente(ctx, "Mozilla/5.0 (Windows NT 10.0) Chrome/140.0")
		m.ServeHTTP(w, r.WithContext(ctx))
	})
	return b
}

// documento único por prueba (el freno y los límites van por documento).
func documentoNuevo() Documento {
	return Documento{Tipo: "CC", Numero: fmt.Sprintf("%d", 7000000000+numeroDePrueba.Add(1))}
}

func (b *banco) cooperativa(nombre string) gen.Tenant {
	b.t.Helper()
	n := numeroDePrueba.Add(1)
	c, err := b.q.UpsertTenant(context.Background(), gen.UpsertTenantParams{
		ID: uuid.Must(uuid.NewV7()), TenantPublicID: uuid.New(), ChannelCode: fmt.Sprintf("I%c%c", 'A'+n/26%26, 'A'+n%26),
		DisplayName: nombre, Brand: json.RawMessage(`{"primary":"#123456"}`), Support: json.RawMessage(`{"phone":"6025550000"}`),
		InactivityMinutes: 5, LastSyncAt: time.Now(),
	})
	if err != nil {
		b.t.Fatalf("cooperativa: %v", err)
	}
	return c
}

func (b *banco) habilitar(c gen.Tenant, doc Documento, persona uuid.UUID, desde time.Time, estado string) {
	b.t.Helper()
	if _, err := b.q.UpsertEnablement(context.Background(), gen.UpsertEnablementParams{
		ID: uuid.Must(uuid.NewV7()), TenantID: c.ID, PersonPublicID: persona, DocumentType: doc.Tipo, DocumentNumber: doc.Numero,
		Status: estado, EnabledAt: desde, ChangeSequence: time.Now().UnixNano(),
	}); err != nil {
		b.t.Fatalf("habilitar: %v", err)
	}
}

type respuesta struct {
	estado int
	cuerpo map[string]any
	crudo  []byte
}

func (r respuesta) codigo() string { c, _ := r.cuerpo["code"].(string); return c }
func (r respuesta) texto(k string) string {
	v, _ := r.cuerpo[k].(string)
	return v
}

func (b *banco) pedir(metodo, ruta string, cuerpo any) respuesta {
	b.t.Helper()
	var lector *bytes.Reader
	if cuerpo != nil {
		j, _ := json.Marshal(cuerpo)
		lector = bytes.NewReader(j)
	} else {
		lector = bytes.NewReader(nil)
	}
	req := httptest.NewRequest(metodo, ruta, lector)
	if cuerpo != nil {
		req.Header.Set("Content-Type", "application/json")
	}
	for _, c := range b.cookies {
		req.AddCookie(&http.Cookie{Name: c.Name, Value: c.Value})
	}
	rec := httptest.NewRecorder()
	b.h.ServeHTTP(rec, req)
	for _, c := range rec.Result().Cookies() {
		if c.MaxAge < 0 {
			delete(b.cookies, c.Name)
		} else {
			if !c.HttpOnly || !c.Secure || c.SameSite != http.SameSiteStrictMode {
				b.t.Errorf("la cookie %s debe ser HttpOnly, Secure y SameSite=Strict", c.Name)
			}
			b.cookies[c.Name] = c
		}
	}
	r := respuesta{estado: rec.Code, crudo: rec.Body.Bytes()}
	_ = json.Unmarshal(r.crudo, &r.cuerpo)
	return r
}

var patronCodigo = regexp.MustCompile(`\b(\d{6})\b`)

func (b *banco) codigoEnviadoA(destino string) string {
	b.t.Helper()
	m, ok := b.correo.Ultimo(destino)
	if !ok {
		b.t.Fatalf("no llegó nada a %s", destino)
	}
	c := patronCodigo.FindStringSubmatch(m.Texto)
	if c == nil {
		b.t.Fatalf("el mensaje no trae código: %q", m.Texto)
	}
	return c[1]
}

// activar recorre la activación completa y deja la sesión en las cookies.
func (b *banco) activar(doc Documento, correo, clave string) {
	b.t.Helper()
	r := b.pedir("POST", "/api/v1/activation/start", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero})
	if r.estado != 200 {
		b.t.Fatalf("start: %d %s", r.estado, r.crudo)
	}
	desafio := r.texto("challengeId")
	if r := b.pedir("POST", "/api/v1/activation/send", map[string]string{"challengeId": desafio, "channel": "email"}); r.estado != 202 {
		b.t.Fatalf("send: %d %s", r.estado, r.crudo)
	}
	r = b.pedir("POST", "/api/v1/activation/verify", map[string]string{"challengeId": desafio, "code": b.codigoEnviadoA(correo)})
	if r.estado != 200 || r.texto("activationToken") == "" {
		b.t.Fatalf("verify: %d %s", r.estado, r.crudo)
	}
	r = b.pedir("POST", "/api/v1/activation/complete", map[string]any{"activationToken": r.texto("activationToken"), "password": clave,
		"acceptances": []map[string]any{{"kind": "Terms", "version": 2}, {"kind": "DataPolicy", "version": 1}}})
	if r.estado != 201 {
		b.t.Fatalf("complete: %d %s", r.estado, r.crudo)
	}
}

// --------------------------------------------------------------------------------------------------- pruebas --

func activacionCompleta(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool, time.Millisecond)
	coop := b.cooperativa("Cooperativa Uno")
	doc, persona := documentoNuevo(), uuid.New()
	b.habilitar(coop, doc, persona, time.Now().Add(-time.Hour), "enabled")
	b.erp.poner(persona, "ana@correo.test", "")

	r := b.pedir("POST", "/api/v1/activation/start", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero})
	canales, _ := r.cuerpo["channels"].([]any)
	if r.estado != 200 || len(canales) != 1 || canales[0].(map[string]any)["masked"] != "a•••@c•••.test" {
		t.Fatalf("start: %d %s", r.estado, r.crudo)
	}
	desafio := r.texto("challengeId")
	if r := b.pedir("POST", "/api/v1/activation/send", map[string]string{"challengeId": desafio, "channel": "sms"}); r.codigo() != "Canal.Codigo.CanalNoDisponible" {
		t.Errorf("sin proveedor de SMS el canal no está disponible: %s", r.crudo)
	}
	b.pedir("POST", "/api/v1/activation/send", map[string]string{"challengeId": desafio, "channel": "email"})
	r = b.pedir("POST", "/api/v1/activation/verify", map[string]string{"challengeId": desafio, "code": b.codigoEnviadoA("ana@correo.test")})
	if r.estado != 200 || r.texto("entityName") != "Cooperativa Uno" || len(r.cuerpo["legalTexts"].([]any)) != 2 {
		t.Fatalf("verify: %d %s", r.estado, r.crudo)
	}
	permiso := r.texto("activationToken")

	debil := b.pedir("POST", "/api/v1/activation/complete", map[string]any{"activationToken": permiso, "password": "password1",
		"acceptances": []map[string]any{{"kind": "Terms", "version": 2}, {"kind": "DataPolicy", "version": 1}}})
	if debil.codigo() != "Canal.Clave.Debil" {
		t.Errorf("clave común: %s", debil.crudo)
	}
	vieja := b.pedir("POST", "/api/v1/activation/complete", map[string]any{"activationToken": permiso, "password": "Cafetal2026x",
		"acceptances": []map[string]any{{"kind": "Terms", "version": 1}, {"kind": "DataPolicy", "version": 1}}})
	if vieja.codigo() != "Canal.Activacion.TextosDesactualizados" {
		t.Errorf("textos de otra versión: %s", vieja.crudo)
	}
	r = b.pedir("POST", "/api/v1/activation/complete", map[string]any{"activationToken": permiso, "password": "Cafetal2026x",
		"acceptances": []map[string]any{{"kind": "Terms", "version": 2}, {"kind": "DataPolicy", "version": 1}}})
	if r.estado != 201 || b.cookies[CookieDeSesion] == nil || b.cookies[CookieDeDispositivo] == nil {
		t.Fatalf("complete: %d %s", r.estado, r.crudo)
	}
	if r.texto("token") != "" || r.texto("deviceCredential") != "" {
		t.Error("en la PWA el token y la credencial no van en el cuerpo")
	}
	if otra := b.pedir("POST", "/api/v1/activation/complete", map[string]any{"activationToken": permiso, "password": "Cafetal2026x"}); otra.codigo() != "Canal.Permiso.Vencido" {
		t.Errorf("el permiso es de un solo uso: %s", otra.crudo)
	}

	me := b.pedir("GET", "/api/v1/me", nil)
	cuenta, _ := me.cuerpo["account"].(map[string]any)
	if me.estado != 200 || cuenta["emailMasked"] != "a•••@c•••.test" || len(me.cuerpo["tenants"].([]any)) != 1 {
		t.Fatalf("me: %d %s", me.estado, me.crudo)
	}
	var aceptaciones int
	_ = pool.QueryRow(context.Background(), `SELECT count(*) FROM legal_acceptances la JOIN accounts a ON a.id = la.account_id
		WHERE a.document_number = $1 AND la.ip LIKE '203.0.113.%'`, doc.Numero).Scan(&aceptaciones)
	if aceptaciones != 2 {
		t.Errorf("aceptaciones con IP: %d", aceptaciones)
	}
	if _, err := pool.Exec(context.Background(), `UPDATE legal_acceptances SET version = 9`); err == nil {
		t.Error("las aceptaciones son sólo de anexar")
	}

	// Activar otra vez con la cuenta ya creada: sólo quien recibe el código se entera.
	b.cookies = map[string]*http.Cookie{}
	r = b.pedir("POST", "/api/v1/activation/start", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero})
	desafio = r.texto("challengeId")
	b.pedir("POST", "/api/v1/activation/send", map[string]string{"challengeId": desafio, "channel": "email"})
	r = b.pedir("POST", "/api/v1/activation/verify", map[string]string{"challengeId": desafio, "code": b.codigoEnviadoA("ana@correo.test")})
	if r.cuerpo["alreadyActive"] != true {
		t.Errorf("cuenta existente: %s", r.crudo)
	}
}

func enumeracionImposible(t *testing.T, pool *pgxpool.Pool) {
	const minimo = 300 * time.Millisecond
	b := nuevoBanco(t, pool, minimo)
	coop := b.cooperativa("Cooperativa Dos")
	conocido, persona := documentoNuevo(), uuid.New()
	b.habilitar(coop, conocido, persona, time.Now().Add(-time.Hour), "enabled")
	b.erp.poner(persona, "luis@correo.test", "")
	desconocido := documentoNuevo()

	medir := func(doc Documento) (respuesta, time.Duration) {
		inicio := time.Now()
		r := b.pedir("POST", "/api/v1/activation/start", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero})
		return r, time.Since(inicio)
	}
	r1, t1 := medir(conocido)
	r2, t2 := medir(desconocido)
	r3, _ := medir(desconocido)
	if r1.estado != r2.estado || len(r1.cuerpo) != len(r2.cuerpo) {
		t.Fatalf("las respuestas difieren en forma: %s / %s", r1.crudo, r2.crudo)
	}
	if t1 < minimo || t2 < minimo || absDur(t1-t2) > 200*time.Millisecond {
		t.Errorf("los tiempos delatan: conocido %v, desconocido %v", t1, t2)
	}
	if fmt.Sprint(r2.cuerpo["channels"]) != fmt.Sprint(r3.cuerpo["channels"]) {
		t.Error("las máscaras del señuelo deben ser estables para el mismo documento")
	}
	senuelo := r2.texto("challengeId")
	enviados := len(b.correo.Enviados())
	if r := b.pedir("POST", "/api/v1/activation/send", map[string]string{"challengeId": senuelo, "channel": "email"}); r.estado != 202 || r.texto("sentTo") == "" {
		t.Errorf("send del señuelo: %d %s", r.estado, r.crudo)
	}
	if len(b.correo.Enviados()) != enviados {
		t.Error("un señuelo no envía nada")
	}
	if r := b.pedir("POST", "/api/v1/activation/verify", map[string]string{"challengeId": senuelo, "code": "123456"}); r.codigo() != "Canal.Codigo.Incorrecto" {
		t.Errorf("verify del señuelo: %s", r.crudo)
	}
	if b.erp.lecturas.Load() != 1 {
		t.Errorf("el ERP sólo se consulta para el documento habilitado: %d lecturas", b.erp.lecturas.Load())
	}

	// Ingreso: cuenta inexistente y clave errada responden igual.
	l1 := b.pedir("POST", "/api/v1/auth/login", map[string]string{"documentType": "CC", "documentNumber": desconocido.Numero, "password": "Cafetal2026x"})
	if l1.estado != 401 || l1.codigo() != "Canal.Ingreso.Invalido" {
		t.Errorf("ingreso inexistente: %s", l1.crudo)
	}
}

func absDur(d time.Duration) time.Duration {
	if d < 0 {
		return -d
	}
	return d
}

func sinDatosDeContacto(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool, time.Millisecond)
	coop := b.cooperativa("Cooperativa Sin Datos")
	doc, persona := documentoNuevo(), uuid.New()
	b.habilitar(coop, doc, persona, time.Now().Add(-time.Hour), "enabled")
	b.erp.poner(persona, "", "")
	r := b.pedir("POST", "/api/v1/activation/start", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero})
	if r.cuerpo["needsEntityUpdate"] != true || r.texto("entityName") != "Cooperativa Sin Datos" || r.cuerpo["support"] == nil {
		t.Errorf("FR-019: %s", r.crudo)
	}
}

func bloqueoPorFallos(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool, time.Millisecond)
	coop := b.cooperativa("Cooperativa Tres")
	doc, persona := documentoNuevo(), uuid.New()
	b.habilitar(coop, doc, persona, time.Now().Add(-time.Hour), "enabled")
	b.erp.poner(persona, "bloqueo@correo.test", "")
	b.activar(doc, "bloqueo@correo.test", "Cafetal2026x")

	for _, d := range []Documento{doc, documentoNuevo()} {
		for i := range FallosParaBloquear {
			r := b.pedir("POST", "/api/v1/auth/login", map[string]string{"documentType": d.Tipo, "documentNumber": d.Numero, "password": "Errada2026x"})
			if r.codigo() != "Canal.Ingreso.Invalido" {
				t.Fatalf("fallo %d: %s", i+1, r.crudo)
			}
		}
		r := b.pedir("POST", "/api/v1/auth/login", map[string]string{"documentType": d.Tipo, "documentNumber": d.Numero, "password": "Cafetal2026x"})
		if r.estado != 429 || r.codigo() != "Canal.Ingreso.Bloqueado" {
			t.Errorf("tras 5 fallos debe bloquear aunque la clave sea correcta (exista o no la cuenta): %s", r.crudo)
		}
	}
	esperar(t, func() bool { m, ok := b.correo.Ultimo("bloqueo@correo.test"); return ok && strings.Contains(m.Asunto, "Bloqueamos") })

	b.reloj.avanzar(DuracionDelBloqueo + time.Second)
	r := b.pedir("POST", "/api/v1/auth/login", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero, "password": "Cafetal2026x"})
	if r.estado != 200 {
		t.Errorf("pasado el bloqueo vuelve a entrar: %d %s", r.estado, r.crudo)
	}
}

func esperar(t *testing.T, cond func() bool) {
	t.Helper()
	for range 100 {
		if cond() {
			return
		}
		time.Sleep(20 * time.Millisecond)
	}
	t.Error("la condición no se cumplió a tiempo")
}

func vencimientoDeCodigos(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool, time.Millisecond)
	coop := b.cooperativa("Cooperativa Cuatro")
	doc, persona := documentoNuevo(), uuid.New()
	b.habilitar(coop, doc, persona, time.Now().Add(-time.Hour), "enabled")
	b.erp.poner(persona, "vence@correo.test", "")

	inicio := func() string {
		r := b.pedir("POST", "/api/v1/activation/start", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero})
		id := r.texto("challengeId")
		b.pedir("POST", "/api/v1/activation/send", map[string]string{"challengeId": id, "channel": "email"})
		return id
	}
	id := inicio()
	codigo := b.codigoEnviadoA("vence@correo.test")
	b.reloj.avanzar(VidaDelCodigo + time.Second)
	if r := b.pedir("POST", "/api/v1/activation/verify", map[string]string{"challengeId": id, "code": codigo}); r.codigo() != "Canal.Codigo.Vencido" {
		t.Errorf("código vencido: %s", r.crudo)
	}

	id = inicio()
	codigo = b.codigoEnviadoA("vence@correo.test")
	r := b.pedir("POST", "/api/v1/activation/verify", map[string]string{"challengeId": id, "code": "000000"})
	if r.codigo() != "Canal.Codigo.Incorrecto" || r.cuerpo["data"].(map[string]any)["intentosRestantes"] != float64(2) {
		t.Errorf("primer intento: %s", r.crudo)
	}
	b.pedir("POST", "/api/v1/activation/verify", map[string]string{"challengeId": id, "code": "000001"})
	if r := b.pedir("POST", "/api/v1/activation/verify", map[string]string{"challengeId": id, "code": "000002"}); r.codigo() != "Canal.Codigo.Agotado" {
		t.Errorf("tercer intento: %s", r.crudo)
	}
	if r := b.pedir("POST", "/api/v1/activation/verify", map[string]string{"challengeId": id, "code": codigo}); r.codigo() != "Canal.Codigo.Agotado" {
		t.Errorf("agotado, ni el código correcto sirve: %s", r.crudo)
	}

	// Límite de desafíos por documento en la hora.
	for range DesafiosPorDocumento {
		b.pedir("POST", "/api/v1/activation/start", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero})
	}
	if r := b.pedir("POST", "/api/v1/activation/start", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero}); r.estado != 429 {
		t.Errorf("límite por documento: %d %s", r.estado, r.crudo)
	}
}

func inactividadYMaximo(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool, time.Millisecond)
	coop := b.cooperativa("Cooperativa Cinco")
	doc, persona := documentoNuevo(), uuid.New()
	b.habilitar(coop, doc, persona, time.Now().Add(-time.Hour), "enabled")
	b.erp.poner(persona, "sesion@correo.test", "")
	b.activar(doc, "sesion@correo.test", "Cafetal2026x")

	// GET /session no cuenta como actividad.
	b.reloj.avanzar(4 * time.Minute)
	b.pedir("GET", "/api/v1/session", nil)
	b.reloj.avanzar(61 * time.Second)
	if r := b.pedir("GET", "/api/v1/me", nil); r.estado != 401 {
		t.Errorf("a los 5 min sin actividad la sesión termina: %d", r.estado)
	}

	// Con actividad cada 4 minutos, la sesión dura 8 h desde el ingreso y nada más.
	delete(b.cookies, CookieDeSesion)
	r := b.pedir("POST", "/api/v1/auth/login", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero, "password": "Cafetal2026x"})
	if r.estado != 200 || r.cuerpo["session"] == nil {
		t.Fatalf("ingreso con dispositivo de confianza (cookie): %s", r.crudo)
	}
	inicio := b.reloj.ahora()
	for b.reloj.ahora().Sub(inicio) < DuracionMaxima-4*time.Minute {
		b.reloj.avanzar(4 * time.Minute)
		if r := b.pedir("POST", "/api/v1/session/keepalive", nil); r.estado != 200 {
			t.Fatalf("a las %v la sesión debía seguir: %d %s", b.reloj.ahora().Sub(inicio), r.estado, r.crudo)
		}
	}
	b.reloj.avanzar(4 * time.Minute)
	if r := b.pedir("POST", "/api/v1/session/keepalive", nil); r.estado != 401 {
		t.Errorf("pasadas 8 h la sesión termina aunque haya actividad: %d", r.estado)
	}
	var motivo string
	_ = pool.QueryRow(context.Background(), `SELECT end_reason FROM sessions s JOIN accounts a ON a.id = s.account_id
		WHERE a.document_number = $1 ORDER BY s.created_at DESC LIMIT 1`, doc.Numero).Scan(&motivo)
	if motivo != "expired" {
		t.Errorf("motivo de cierre: %q", motivo)
	}
}

func selectorYAcotamiento(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool, time.Millisecond)
	uno, dos := b.cooperativa("Alfa Cooperativa"), b.cooperativa("Beta Cooperativa")
	doc, p1, p2 := documentoNuevo(), uuid.New(), uuid.New()
	b.habilitar(uno, doc, p1, time.Now().Add(-2*time.Hour), "enabled")
	b.erp.poner(p1, "selector@correo.test", "")
	b.activar(doc, "selector@correo.test", "Cafetal2026x")

	tenants := func() int { return len(b.pedir("GET", "/api/v1/me", nil).cuerpo["tenants"].([]any)) }
	if n := tenants(); n != 1 {
		t.Fatalf("con una cooperativa: %d", n)
	}
	b.habilitar(dos, doc, p2, time.Now().Add(-time.Hour), "enabled")
	if n := tenants(); n != 2 {
		t.Fatalf("con dos cooperativas: %d", n)
	}
	if r := b.pedir("GET", "/api/v1/prueba/acotada", nil); r.codigo() != "Canal.Cooperativa.SinElegir" {
		t.Errorf("sin elegir: %s", r.crudo)
	}
	r := b.pedir("POST", "/api/v1/session/tenant", map[string]any{"tenantPublicId": dos.TenantPublicID})
	if r.estado != 200 {
		t.Fatalf("elegir: %s", r.crudo)
	}
	r = b.pedir("GET", "/api/v1/prueba/acotada", nil)
	if r.texto("tenant") != dos.TenantPublicID.String() || r.texto("person") != p2.String() {
		t.Errorf("acotada: %s", r.crudo)
	}
	if r := b.pedir("POST", "/api/v1/session/tenant", map[string]any{"tenantPublicId": uuid.New()}); r.codigo() != "Canal.Cooperativa.NoDisponible" {
		t.Errorf("cooperativa ajena: %s", r.crudo)
	}

	// La cooperativa inhabilita: corta en la siguiente petición, sin cerrar la sesión.
	b.habilitar(dos, doc, p2, time.Now().Add(-time.Hour), "disabled")
	if r := b.pedir("GET", "/api/v1/prueba/acotada", nil); r.estado != 403 || r.codigo() != "Canal.Cooperativa.NoDisponible" {
		t.Errorf("inhabilitada: %d %s", r.estado, r.crudo)
	}
	if n := tenants(); n != 1 {
		t.Errorf("queda una cooperativa: %d", n)
	}
	b.habilitar(uno, doc, p1, time.Now().Add(-2*time.Hour), "disabled")
	if n := tenants(); n != 0 {
		t.Errorf("sin cooperativas habilitadas la persona sigue entrando, sin entidades: %d", n)
	}
	if r := b.pedir("GET", "/api/v1/me", nil); r.estado != 200 {
		t.Errorf("la inhabilitación no impide el ingreso: %d", r.estado)
	}
}

func dispositivoNuevo(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool, time.Millisecond)
	coop := b.cooperativa("Cooperativa Seis")
	doc, persona := documentoNuevo(), uuid.New()
	b.habilitar(coop, doc, persona, time.Now().Add(-time.Hour), "enabled")
	b.erp.poner(persona, "disp@correo.test", "")
	b.activar(doc, "disp@correo.test", "Cafetal2026x")

	b.cookies = map[string]*http.Cookie{} // otro navegador
	r := b.pedir("POST", "/api/v1/auth/login", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero, "password": "Cafetal2026x"})
	desafio := r.texto("challengeId")
	if desafio == "" || b.cookies[CookieDeSesion] != nil {
		t.Fatalf("dispositivo nuevo pide código: %s", r.crudo)
	}
	b.pedir("POST", "/api/v1/auth/login/send", map[string]string{"challengeId": desafio, "channel": "email"})
	r = b.pedir("POST", "/api/v1/auth/login/verify", map[string]any{"challengeId": desafio, "code": b.codigoEnviadoA("disp@correo.test"), "trustDevice": true})
	if r.estado != 200 || b.cookies[CookieDeSesion] == nil || b.cookies[CookieDeDispositivo] == nil {
		t.Fatalf("verify: %s", r.crudo)
	}
	delete(b.cookies, CookieDeSesion)
	r = b.pedir("POST", "/api/v1/auth/login", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero, "password": "Cafetal2026x"})
	if r.cuerpo["session"] == nil {
		t.Errorf("con la credencial de confianza entra sin código: %s", r.crudo)
	}

	// La app recibe token y credencial en el cuerpo.
	b.cookies = map[string]*http.Cookie{}
	req := httptest.NewRequest("POST", "/api/v1/auth/login", strings.NewReader(fmt.Sprintf(
		`{"documentType":"%s","documentNumber":"%s","password":"Cafetal2026x"}`, doc.Tipo, doc.Numero)))
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set(CabeceraPlataforma, "android")
	rec := httptest.NewRecorder()
	b.h.ServeHTTP(rec, req)
	var cuerpo map[string]any
	_ = json.Unmarshal(rec.Body.Bytes(), &cuerpo)
	desafio, _ = cuerpo["challengeId"].(string)
	b.pedir("POST", "/api/v1/auth/login/send", map[string]string{"challengeId": desafio, "channel": "email"})
	req = httptest.NewRequest("POST", "/api/v1/auth/login/verify", strings.NewReader(fmt.Sprintf(
		`{"challengeId":"%s","code":"%s","trustDevice":true}`, desafio, b.codigoEnviadoA("disp@correo.test"))))
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set(CabeceraPlataforma, "android")
	rec = httptest.NewRecorder()
	b.h.ServeHTTP(rec, req)
	_ = json.Unmarshal(rec.Body.Bytes(), &cuerpo)
	token, _ := cuerpo["token"].(string)
	if token == "" || cuerpo["deviceCredential"] == nil || len(rec.Result().Cookies()) != 0 {
		t.Fatalf("app: %s", rec.Body.Bytes())
	}
	req = httptest.NewRequest("GET", "/api/v1/me/devices", nil)
	req.Header.Set("Authorization", "Bearer "+token)
	rec = httptest.NewRecorder()
	b.h.ServeHTTP(rec, req)
	_ = json.Unmarshal(rec.Body.Bytes(), &cuerpo)
	if rec.Code != 200 || len(cuerpo["devices"].([]any)) != 3 {
		t.Errorf("dispositivos: %d %s", rec.Code, rec.Body.Bytes())
	}
}

func recuperacion(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool, time.Millisecond)
	coop := b.cooperativa("Cooperativa Siete")
	doc, persona := documentoNuevo(), uuid.New()
	b.habilitar(coop, doc, persona, time.Now().Add(-time.Hour), "enabled")
	b.erp.poner(persona, "recupera@correo.test", "")
	b.activar(doc, "recupera@correo.test", "Cafetal2026x")
	vieja := b.cookies[CookieDeSesion]

	r := b.pedir("POST", "/api/v1/auth/recovery/start", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero})
	desafio := r.texto("challengeId")
	b.pedir("POST", "/api/v1/auth/recovery/send", map[string]string{"challengeId": desafio, "channel": "email"})
	r = b.pedir("POST", "/api/v1/auth/recovery/verify", map[string]string{"challengeId": desafio, "code": b.codigoEnviadoA("recupera@correo.test")})
	permiso := r.texto("recoveryToken")
	if permiso == "" {
		t.Fatalf("verify: %s", r.crudo)
	}
	if r := b.pedir("POST", "/api/v1/auth/recovery/complete", map[string]string{"recoveryToken": permiso, "password": "NuevaClave2027"}); r.estado != 204 {
		t.Fatalf("complete: %d %s", r.estado, r.crudo)
	}
	b.cookies[CookieDeSesion] = vieja
	if r := b.pedir("GET", "/api/v1/me", nil); r.estado != 401 {
		t.Errorf("recuperar la clave cierra las sesiones: %d", r.estado)
	}
	r = b.pedir("POST", "/api/v1/auth/login", map[string]string{"documentType": doc.Tipo, "documentNumber": doc.Numero, "password": "NuevaClave2027"})
	if r.cuerpo["session"] == nil {
		t.Errorf("entra con la clave nueva: %s", r.crudo)
	}
	esperar(t, func() bool { m, ok := b.correo.Ultimo("recupera@correo.test"); return ok && strings.Contains(m.Asunto, "clave") })
}

func cambioDeContacto(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool, time.Millisecond)
	uno, dos := b.cooperativa("Gamma Cooperativa"), b.cooperativa("Delta Cooperativa")
	doc, p1, p2 := documentoNuevo(), uuid.New(), uuid.New()
	b.habilitar(uno, doc, p1, time.Now().Add(-2*time.Hour), "enabled")
	b.habilitar(dos, doc, p2, time.Now().Add(-time.Hour), "enabled")
	b.erp.poner(p1, "antes@correo.test", "")
	b.activar(doc, "antes@correo.test", "Cafetal2026x")

	if r := b.pedir("POST", "/api/v1/me/contact", map[string]string{"field": "email", "value": "antes@correo.test"}); r.codigo() != "Canal.Contacto.Igual" {
		t.Errorf("mismo dato: %s", r.crudo)
	}
	r := b.pedir("POST", "/api/v1/me/contact", map[string]string{"field": "email", "value": "Despues@Correo.test"})
	desafio := r.texto("challengeId")
	if r.estado != 200 || r.texto("sentTo") != "d•••@c•••.test" {
		t.Fatalf("contacto: %d %s", r.estado, r.crudo)
	}
	r = b.pedir("POST", "/api/v1/me/contact/verify", map[string]string{"challengeId": desafio, "code": b.codigoEnviadoA("despues@correo.test")})
	if r.estado != 200 {
		t.Fatalf("verify: %s", r.crudo)
	}
	me := b.pedir("GET", "/api/v1/me", nil)
	if me.cuerpo["account"].(map[string]any)["emailMasked"] != "d•••@c•••.test" {
		t.Errorf("el correo cambió: %s", me.crudo)
	}
	esperar(t, func() bool { m, ok := b.correo.Ultimo("antes@correo.test"); return ok && strings.Contains(m.Asunto, "contacto") })
	b.cola.mu.Lock()
	defer b.cola.mu.Unlock()
	if len(b.cola.jobs) != 2 {
		t.Fatalf("una novedad por cooperativa habilitada: %d", len(b.cola.jobs))
	}
	for _, j := range b.cola.jobs {
		n := j.(ArgsReportarNovedad)
		if n.Campo != "email" || n.Valor != "despues@correo.test" || (n.PersonPublicID != p1 && n.PersonPublicID != p2) {
			t.Errorf("novedad: %+v", n)
		}
	}
}
