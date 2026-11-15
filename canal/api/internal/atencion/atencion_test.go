package atencion

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"strings"
	"sync/atomic"
	"testing"
	"time"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/identidad"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

var n atomic.Int64

// T123 (FR-059): una cooperativa no ve, no desbloquea ni cierra sesiones de una cuenta cuyo documento no está habilitado
// en ella, ni la cuenta en sus indicadores.
func TestAtencionAcotadaALaCooperativa(t *testing.T) {
	pool := dbtest.Migrada(t)
	ctx := context.Background()
	q := gen.New(pool)
	ahora := time.Now().UTC().Truncate(time.Second)
	k := n.Add(1)

	cooperativa := func(letra rune) gen.Tenant {
		c, err := q.UpsertTenant(ctx, gen.UpsertTenantParams{ID: uuid.Must(uuid.NewV7()), TenantPublicID: uuid.New(),
			ChannelCode: fmt.Sprintf("T%c%c", letra, 'A'+k%26), DisplayName: "Cooperativa " + string(letra), Brand: json.RawMessage(`{}`),
			Support: json.RawMessage(`{}`), InactivityMinutes: 5, LastSyncAt: ahora})
		if err != nil {
			t.Fatal(err)
		}
		return c
	}
	a, b := cooperativa('A'), cooperativa('B')
	doc := identidad.Documento{Tipo: "CC", Numero: fmt.Sprintf("%d", 2000000000+k)}
	if _, err := q.UpsertEnablement(ctx, gen.UpsertEnablementParams{ID: uuid.New(), TenantID: a.ID, PersonPublicID: uuid.New(), DocumentType: doc.Tipo,
		DocumentNumber: doc.Numero, Status: "enabled", EnabledAt: ahora, ChangeSequence: 1}); err != nil {
		t.Fatal(err)
	}
	cuenta := uuid.Must(uuid.NewV7())
	correo, celular := "ana@ejemplo.test", "3001234567"
	if err := q.InsertAccount(ctx, gen.InsertAccountParams{ID: cuenta, DocumentType: doc.Tipo, DocumentNumber: doc.Numero, FirstNames: "Ana",
		LastNames: "Núñez", Email: &correo, Mobile: &celular, PasswordHash: strings.Repeat("x", 60), ActivatedAt: ahora, ValidatingTenantID: a.ID}); err != nil {
		t.Fatal(err)
	}
	sesion := uuid.New()
	if err := q.InsertSession(ctx, gen.InsertSessionParams{ID: sesion, AccountID: cuenta, TokenHash: []byte(strings.Repeat("s", 32)), InactivityMinutes: 5,
		CreatedAt: ahora.Add(-time.Minute), ExpiresAt: ahora.Add(time.Hour)}); err != nil {
		t.Fatal(err)
	}
	if err := q.SetSessionTenant(ctx, gen.SetSessionTenantParams{ID: sesion, TenantID: &a.ID, InactivityMinutes: 5, At: ahora}); err != nil {
		t.Fatal(err)
	}
	if _, err := q.RecordLoginFailure(ctx, gen.RecordLoginFailureParams{DocumentKey: doc.Llave(), At: ahora, WindowStart: ahora.Add(-time.Hour)}); err != nil {
		t.Fatal(err)
	}
	if err := q.LockThrottle(ctx, gen.LockThrottleParams{DocumentKey: doc.Llave(), LockedUntil: ahora.Add(15 * time.Minute)}); err != nil {
		t.Fatal(err)
	}
	s := Nuevo(q, auditoria.Nuevo(pool), func() time.Time { return ahora })
	esNoExiste := func(err error) {
		t.Helper()
		var e *sobre.Error
		if !errors.As(err, &e) || e.Codigo != ErrCuentaNoExiste.Codigo {
			t.Fatalf("se esperaba CuentaNoExiste: %v", err)
		}
	}

	// La otra cooperativa no la ve ni sabe si existe.
	if c, err := s.BuscarCuenta(ctx, b.ID, "CC", doc.Numero); err != nil || c != nil {
		t.Fatalf("la cooperativa B ve la cuenta: %+v %v", c, err)
	}
	esNoExiste(s.Desbloquear(ctx, b.ID, cuenta))
	_, err := s.CerrarSesiones(ctx, b.ID, cuenta)
	esNoExiste(err)
	if m, err := s.Indicadores(ctx, b.ID, ahora.Add(-24*time.Hour), ahora.Add(time.Hour)); err != nil || m.Habilitadas != 0 || m.CuentasActivas != 0 || m.Ingresos != 0 {
		t.Fatalf("indicadores de B: %+v %v", m, err)
	}

	// La suya sí: cuenta con el contacto enmascarado, el bloqueo y la sesión abierta.
	c, err := s.BuscarCuenta(ctx, a.ID, "cc", doc.Numero)
	if err != nil || c == nil || c.ID != cuenta || c.BloqueadaHasta == nil || c.SesionesAbiertas != 1 || c.UltimoIngreso == nil ||
		c.Correo == nil || strings.Contains(*c.Correo, "ana@") || c.EstadoHabilitacion != "enabled" {
		t.Fatalf("cuenta para A: %+v %v", c, err)
	}
	if err := s.Desbloquear(ctx, a.ID, cuenta); err != nil {
		t.Fatal(err)
	}
	if c, _ := s.BuscarCuenta(ctx, a.ID, "CC", doc.Numero); c.BloqueadaHasta != nil {
		t.Fatalf("sigue bloqueada: %v", c.BloqueadaHasta)
	}
	if m, err := s.Indicadores(ctx, a.ID, ahora.Add(-24*time.Hour), ahora.Add(time.Hour)); err != nil || m.Habilitadas != 1 || m.CuentasActivas != 1 || m.Ingresos != 1 {
		t.Fatalf("indicadores de A: %+v %v", m, err)
	}
	if cerradas, err := s.CerrarSesiones(ctx, a.ID, cuenta); err != nil || cerradas != 1 {
		t.Fatalf("sesiones cerradas: %d %v", cerradas, err)
	}
	if _, err := s.Indicadores(ctx, a.ID, ahora, ahora.Add(-time.Hour)); !errors.Is(err, ErrRangoInvalido) {
		t.Fatalf("rango invertido: %v", err)
	}
}
