package erp

import (
	"context"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
)

func llaveDePrueba(t *testing.T) *ecdsa.PrivateKey {
	t.Helper()
	llave, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		t.Fatalf("generar llave: %v", err)
	}
	return llave
}

func silencioso() *slog.Logger { return slog.New(slog.NewTextHandler(io.Discard, nil)) }

// El ERP de prueba valida el token como lo hace el esquema CanalServicio del ERP.
func TestClienteFirmaComoEsperaElERP(t *testing.T) {
	llave := llaveDePrueba(t)
	cooperativa := uuid.New()
	var recibido struct {
		sub, jti, ip, agente, cuenta, ruta string
		vida                               time.Duration
	}

	erpDePrueba := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		crudo := strings.TrimPrefix(r.Header.Get("Authorization"), "Bearer ")
		reclamos := &jwt.RegisteredClaims{}
		token, err := jwt.ParseWithClaims(crudo, reclamos, func(*jwt.Token) (any, error) { return &llave.PublicKey, nil },
			jwt.WithValidMethods([]string{"ES256"}), jwt.WithIssuer(EmisorDelCanal), jwt.WithAudience(AudienciaDelERP))
		if err != nil {
			w.WriteHeader(http.StatusUnauthorized)
			return
		}
		if token.Header["kid"] != "canal1" {
			w.WriteHeader(http.StatusUnauthorized)
			return
		}
		recibido.sub, recibido.jti = reclamos.Subject, reclamos.ID
		recibido.vida = reclamos.ExpiresAt.Sub(reclamos.IssuedAt.Time)
		recibido.ip, recibido.agente, recibido.cuenta = r.Header.Get(CabeceraIP), r.Header.Get(CabeceraAgente), r.Header.Get(CabeceraCuenta)
		recibido.ruta = r.URL.Path
		_ = json.NewEncoder(w).Encode([]map[string]any{{
			"tenantPublicId": cooperativa, "channelCode": "CFP", "displayName": "Cooperativa de prueba",
			"brand": map[string]any{"primary": "#123456"}, "support": map[string]any{}, "inactivityMinutes": 5, "smsEnabled": false,
		}})
	}))
	defer erpDePrueba.Close()

	base, _ := url.Parse(erpDePrueba.URL)
	cliente := NuevoCliente(base, llave, "canal1", erpDePrueba.Client())
	cuenta := uuid.New()
	ctx := contexto.ConIP(context.Background(), "186.80.28.28")
	ctx = contexto.ConAgente(ctx, "Navegador de prueba")
	ctx = contexto.ConCuenta(ctx, cuenta)

	cooperativas, err := cliente.ListarCooperativas(ctx)
	if err != nil {
		t.Fatalf("listar: %v", err)
	}
	if len(cooperativas) != 1 || cooperativas[0].TenantPublicID != cooperativa || *cooperativas[0].ChannelCode != "CFP" {
		t.Fatalf("respuesta inesperada: %+v", cooperativas)
	}
	if recibido.sub != SujetoDePlataforma || recibido.jti == "" || recibido.vida != VidaDelToken {
		t.Fatalf("token mal formado: sub=%q jti=%q vida=%v", recibido.sub, recibido.jti, recibido.vida)
	}
	if recibido.ip != "186.80.28.28" || recibido.agente != "Navegador de prueba" || recibido.cuenta != cuenta.String() {
		t.Fatalf("no reenvió el contexto del asociado: %+v", recibido)
	}
	if recibido.ruta != "/api/canal/v1/tenants" {
		t.Fatalf("ruta inesperada %q", recibido.ruta)
	}
}

func TestClienteTraduceElSobreDeError(t *testing.T) {
	erpDePrueba := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusConflict)
		_, _ = w.Write([]byte(`{"code":"Channel.Disabled","message":"La cooperativa no tiene encendido el canal digital."}`))
	}))
	defer erpDePrueba.Close()
	base, _ := url.Parse(erpDePrueba.URL)

	err := NuevoCliente(base, llaveDePrueba(t), "k", erpDePrueba.Client()).Hacer(context.Background(), http.MethodGet, uuid.NewString(), "/x", nil, nil)
	var delERP *ErrorDelERP
	if !asError(err, &delERP) || delERP.Codigo != "Channel.Disabled" || delERP.Estado != http.StatusConflict {
		t.Fatalf("se esperaba el sobre del ERP, llegó %v", err)
	}
}

func TestClienteSinERPDevuelveNoDisponible(t *testing.T) {
	base, _ := url.Parse("http://127.0.0.1:1")
	err := NuevoCliente(base, llaveDePrueba(t), "k", &http.Client{Timeout: time.Second}).Hacer(context.Background(), http.MethodGet, "platform", "/tenants", nil, nil)
	if err == nil || !strings.Contains(err.Error(), ErrERPNoDisponible.Error()) {
		t.Fatalf("se esperaba ErrERPNoDisponible, llegó %v", err)
	}
}

// almacenEnMemoria imita service_token_jti para las pruebas sin base.
type almacenEnMemoria struct {
	mu     sync.Mutex
	vistos map[string]bool
}

func (a *almacenEnMemoria) Registrar(_ context.Context, jti string, _ time.Time) (bool, error) {
	a.mu.Lock()
	defer a.mu.Unlock()
	if a.vistos[jti] {
		return false, nil
	}
	a.vistos[jti] = true
	return true, nil
}

func tokenDelERP(t *testing.T, llave *ecdsa.PrivateKey, mutar func(*jwt.RegisteredClaims)) string {
	t.Helper()
	ahora := time.Now()
	r := &jwt.RegisteredClaims{
		Issuer: EmisorDelERP, Audience: jwt.ClaimStrings{AudienciaDelCanal}, Subject: "c4f1a7d2-0000-4000-8000-000000000001",
		IssuedAt: jwt.NewNumericDate(ahora), ExpiresAt: jwt.NewNumericDate(ahora.Add(VidaDelToken)), ID: uuid.NewString(),
	}
	if mutar != nil {
		mutar(r)
	}
	firmado, err := jwt.NewWithClaims(jwt.SigningMethodES256, r).SignedString(llave)
	if err != nil {
		t.Fatalf("firmar: %v", err)
	}
	return firmado
}

func TestVerificador(t *testing.T) {
	llave := llaveDePrueba(t)
	otra := llaveDePrueba(t)
	verificador := NuevoVerificador(&llave.PublicKey, &almacenEnMemoria{vistos: map[string]bool{}}, silencioso())
	protegido := verificador.Middleware(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		sujeto, _ := SujetoDe(r.Context())
		_, _ = w.Write([]byte(sujeto))
	}))
	pedir := func(ruta, token string) *httptest.ResponseRecorder {
		r := httptest.NewRequest(http.MethodGet, ruta, nil)
		if token != "" {
			r.Header.Set("Authorization", "Bearer "+token)
		}
		w := httptest.NewRecorder()
		protegido.ServeHTTP(w, r)
		return w
	}
	const ruta = "/internal/v1/tenants/c4f1a7d2-0000-4000-8000-000000000001/sync"

	valido := tokenDelERP(t, llave, nil)
	if w := pedir(ruta, valido); w.Code != http.StatusOK || w.Body.String() != "c4f1a7d2-0000-4000-8000-000000000001" {
		t.Fatalf("token válido rechazado: %d %s", w.Code, w.Body.String())
	}
	if w := pedir(ruta, valido); w.Code != http.StatusUnauthorized || !strings.Contains(w.Body.String(), "Canal.Servicio.TokenRepetido") {
		t.Fatalf("el jti repetido debía rechazarse: %d %s", w.Code, w.Body.String())
	}

	casos := map[string]string{
		"sin token":      "",
		"otra llave":     tokenDelERP(t, otra, nil),
		"otra audiencia": tokenDelERP(t, llave, func(r *jwt.RegisteredClaims) { r.Audience = jwt.ClaimStrings{"ingenia365-erp"} }),
		"otro emisor":    tokenDelERP(t, llave, func(r *jwt.RegisteredClaims) { r.Issuer = "ingenia365-canal" }),
		"vencido": tokenDelERP(t, llave, func(r *jwt.RegisteredClaims) {
			r.IssuedAt = jwt.NewNumericDate(time.Now().Add(-5 * time.Minute))
			r.ExpiresAt = jwt.NewNumericDate(time.Now().Add(-4 * time.Minute))
		}),
		"vida de una hora": tokenDelERP(t, llave, func(r *jwt.RegisteredClaims) { r.ExpiresAt = jwt.NewNumericDate(time.Now().Add(time.Hour)) }),
		"sin jti":          tokenDelERP(t, llave, func(r *jwt.RegisteredClaims) { r.ID = "" }),
	}
	for nombre, token := range casos {
		if w := pedir(ruta, token); w.Code != http.StatusUnauthorized {
			t.Errorf("%s: se esperaba 401, llegó %d %s", nombre, w.Code, w.Body.String())
		}
	}

	deOtra := tokenDelERP(t, llave, func(r *jwt.RegisteredClaims) { r.Subject = uuid.NewString() })
	if w := pedir(ruta, deOtra); w.Code != http.StatusForbidden {
		t.Fatalf("un token de otra cooperativa debía dar 403: %d %s", w.Code, w.Body.String())
	}
}

func asError(err error, destino **ErrorDelERP) bool {
	e, ok := err.(*ErrorDelERP)
	if ok {
		*destino = e
	}
	return ok
}
