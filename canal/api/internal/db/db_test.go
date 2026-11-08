package db_test

import (
	"context"
	"encoding/json"
	"sync"
	"testing"
	"time"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
)

func TestMigracionesSubenBajanYVuelvenASubir(t *testing.T) {
	pool := dbtest.Base(t)
	ctx := context.Background()
	logger := dbtest.Silencioso()

	// Dos réplicas que arrancan a la vez: el candado las serializa y ninguna falla.
	var wg sync.WaitGroup
	errores := make(chan error, 2)
	for range 2 {
		wg.Go(func() {
			errores <- db.ConCerrojoDeMigracion(ctx, pool, func(ctx context.Context) error {
				return db.Migrar(ctx, pool, logger)
			})
		})
	}
	wg.Wait()
	close(errores)
	for err := range errores {
		if err != nil {
			t.Fatalf("migración concurrente: %v", err)
		}
	}

	if err := db.MigrarHasta(ctx, pool, logger, 0); err != nil {
		t.Fatalf("Down hasta 0: %v", err)
	}
	var tablas int
	if err := pool.QueryRow(ctx, `SELECT count(*) FROM information_schema.tables
		WHERE table_schema = 'public' AND table_name IN ('tenants', 'audit_log', 'service_token_jti')`).Scan(&tablas); err != nil {
		t.Fatal(err)
	}
	if tablas != 0 {
		t.Fatalf("tras el Down quedaron %d tablas", tablas)
	}
	if err := db.Migrar(ctx, pool, logger); err != nil {
		t.Fatalf("volver a subir: %v", err)
	}
}

func TestAuditoriaEsSoloDeAnexar(t *testing.T) {
	pool := dbtest.Migrada(t)
	ctx := context.Background()
	q := gen.New(pool)

	id := uuid.Must(uuid.NewV7())
	if err := q.InsertAuditEntry(ctx, gen.InsertAuditEntryParams{
		ID: id, At: time.Now(), Actor: "system", Action: "Prueba", Data: json.RawMessage(`{}`),
	}); err != nil {
		t.Fatal(err)
	}
	for _, sentencia := range []string{
		"UPDATE audit_log SET action = 'Otra' WHERE id = $1",
		"DELETE FROM audit_log WHERE id = $1",
	} {
		if _, err := pool.Exec(ctx, sentencia, id); err == nil {
			t.Errorf("la base aceptó: %s", sentencia)
		}
	}
	if _, err := pool.Exec(ctx, "TRUNCATE audit_log"); err == nil {
		t.Error("la base aceptó TRUNCATE de audit_log")
	}
}

func TestInhabilitarLasNoListadas(t *testing.T) {
	pool := dbtest.Migrada(t)
	ctx := context.Background()
	q := gen.New(pool)
	ahora := time.Now()

	crear := func(codigo string) uuid.UUID {
		publico := uuid.New()
		if _, err := q.UpsertTenant(ctx, gen.UpsertTenantParams{
			ID: uuid.Must(uuid.NewV7()), TenantPublicID: publico, ChannelCode: codigo, DisplayName: "Cooperativa " + codigo,
			Brand: json.RawMessage(`{}`), Support: json.RawMessage(`{}`), InactivityMinutes: 5, LastSyncAt: ahora,
		}); err != nil {
			t.Fatal(err)
		}
		return publico
	}
	a, b := crear("AAA"), crear("BBB")

	apagadas, err := q.DisableTenantsNotListed(ctx, gen.DisableTenantsNotListedParams{LastSyncAt: ahora, Listed: []uuid.UUID{a}})
	if err != nil {
		t.Fatal(err)
	}
	if len(apagadas) != 1 || apagadas[0] != b {
		t.Fatalf("apagadas = %v, se esperaba sólo %v", apagadas, b)
	}
	todas, err := q.ListTenants(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if len(todas) != 2 {
		t.Fatalf("la fila no debía borrarse: quedan %d", len(todas))
	}
}
