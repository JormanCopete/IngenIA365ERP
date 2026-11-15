// Package dbtest: PostgreSQL de prueba en un contenedor (testcontainers-go) para las pruebas del canal.
//
// Si Docker no está disponible la prueba se salta con t.Skip y el motivo queda en la salida de la prueba; en el CI
// hay Docker y no se salta.
package dbtest

import (
	"context"
	"io"
	"log/slog"
	"testing"

	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/testcontainers/testcontainers-go"
	"github.com/testcontainers/testcontainers-go/modules/postgres"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db"
)

// Imagen es la misma de canal/docker-compose.yml.
const Imagen = "postgres:17-alpine"

// Base levanta un PostgreSQL vacío y devuelve un pool sobre él. El contenedor se retira al terminar la prueba.
func Base(t *testing.T) *pgxpool.Pool {
	t.Helper()
	testcontainers.SkipIfProviderIsNotHealthy(t)

	ctx := context.Background()
	ctr, err := postgres.Run(ctx, Imagen,
		postgres.WithDatabase("ingenia365_canal"),
		postgres.WithUsername("canal"),
		postgres.WithPassword("canal-solo-pruebas"),
		postgres.BasicWaitStrategies(),
	)
	testcontainers.CleanupContainer(t, ctr)
	if err != nil {
		t.Fatalf("no se pudo levantar PostgreSQL de prueba: %v", err)
	}
	dsn, err := ctr.ConnectionString(ctx, "sslmode=disable")
	if err != nil {
		t.Fatalf("cadena de conexión del contenedor: %v", err)
	}
	pool, err := db.Abrir(ctx, dsn)
	if err != nil {
		t.Fatalf("abrir la base de prueba: %v", err)
	}
	t.Cleanup(pool.Close)
	return pool
}

// Migrada levanta la base y le aplica las migraciones goose del canal.
func Migrada(t *testing.T) *pgxpool.Pool {
	t.Helper()
	pool := Base(t)
	ctx := context.Background()
	if err := db.ConCerrojoDeMigracion(ctx, pool, func(ctx context.Context) error {
		return db.Migrar(ctx, pool, Silencioso())
	}); err != nil {
		t.Fatalf("migrar la base de prueba: %v", err)
	}
	return pool
}

// Silencioso es un logger que descarta todo, para no ensuciar la salida de las pruebas.
func Silencioso() *slog.Logger { return slog.New(slog.NewTextHandler(io.Discard, nil)) }
