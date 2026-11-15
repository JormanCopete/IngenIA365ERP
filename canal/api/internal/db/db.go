// Package db: pool de conexiones a la base del canal (ingenia365_canal) y sus migraciones goose.
//
// Es la ÚNICA base que abre el canal: nunca una del ERP (constitución, «Sistemas satélite», regla 1).
// Las consultas tipadas viven en internal/db/gen y las genera sqlc desde queries/ (ver sqlc.yaml).
package db

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"log/slog"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/jackc/pgx/v5/stdlib"
	"github.com/pressly/goose/v3"

	"github.com/jormancopete/ingenia365erp/canal/api/migrations"
)

// Abrir crea el pool y comprueba que la base responde.
func Abrir(ctx context.Context, dsn string) (*pgxpool.Pool, error) {
	cfg, err := pgxpool.ParseConfig(dsn)
	if err != nil {
		// El error de pgx podría repetir la cadena: no se envuelve.
		return nil, errors.New("db: CANAL_BASE_URL no es una cadena de conexión válida")
	}
	cfg.ConnConfig.RuntimeParams["application_name"] = "ingenia365-canal"
	cfg.MaxConnIdleTime = 5 * time.Minute

	pool, err := pgxpool.NewWithConfig(ctx, cfg)
	if err != nil {
		return nil, fmt.Errorf("db: crear el pool: %w", err)
	}
	ping, cancelar := context.WithTimeout(ctx, 10*time.Second)
	defer cancelar()
	if err := pool.Ping(ping); err != nil {
		pool.Close()
		return nil, fmt.Errorf("db: la base del canal no responde: %w", err)
	}
	return pool, nil
}

// claveDeMigracion es la llave del candado de asesoría que serializa las migraciones entre réplicas
// ("canalmig" en ASCII). River usa su propio prefijo de candados (Config.AdvisoryLockPrefix).
const claveDeMigracion int64 = 0x63616e616c6d6967

// ConCerrojoDeMigracion corre fn con el candado de asesoría de migraciones tomado en una conexión dedicada:
// si arrancan dos réplicas a la vez, la segunda espera a que la primera termine.
func ConCerrojoDeMigracion(ctx context.Context, pool *pgxpool.Pool, fn func(context.Context) error) error {
	conn, err := pool.Acquire(ctx)
	if err != nil {
		return fmt.Errorf("db: conexión para el candado de migración: %w", err)
	}
	defer conn.Release()

	if _, err := conn.Exec(ctx, "SELECT pg_advisory_lock($1)", claveDeMigracion); err != nil {
		return fmt.Errorf("db: tomar el candado de migración: %w", err)
	}
	defer func() {
		// Con un contexto propio: el del llamador puede estar cancelado y el candado debe soltarse igual.
		soltar, cancelar := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancelar()
		if _, err := conn.Exec(soltar, "SELECT pg_advisory_unlock($1)", claveDeMigracion); err != nil {
			// El candado es de sesión: cerrar la conexión lo suelta, y el pool descarta la conexión cerrada.
			slog.Error("db: no se pudo soltar el candado de migración; se cierra la conexión", "error", err)
			if err := conn.Conn().Close(soltar); err != nil {
				slog.Error("db: tampoco se pudo cerrar la conexión del candado", "error", err)
			}
		}
	}()
	return fn(ctx)
}

func proveedor(pool *pgxpool.Pool, logger *slog.Logger) (*goose.Provider, *sql.DB, error) {
	sqlDB := stdlib.OpenDBFromPool(pool)
	p, err := goose.NewProvider(goose.DialectPostgres, sqlDB, migrations.FS,
		goose.WithSlog(logger.With("componente", "goose")),
		goose.WithDisableGlobalRegistry(true),
	)
	if err != nil {
		sqlDB.Close()
		return nil, nil, fmt.Errorf("db: preparar migraciones: %w", err)
	}
	return p, sqlDB, nil
}

// Migrar aplica las migraciones goose pendientes. Debe correr dentro de ConCerrojoDeMigracion.
func Migrar(ctx context.Context, pool *pgxpool.Pool, logger *slog.Logger) error {
	p, sqlDB, err := proveedor(pool, logger)
	if err != nil {
		return err
	}
	defer sqlDB.Close()
	resultados, err := p.Up(ctx)
	if err != nil {
		return fmt.Errorf("db: aplicar migraciones: %w", err)
	}
	for _, r := range resultados {
		logger.Info("migración aplicada", "version", r.Source.Version, "archivo", r.Source.Path, "duracion", r.Duration)
	}
	return nil
}

// MigrarHasta lleva el esquema a una versión (hacia abajo). Lo usan las pruebas que comprueban los Down.
func MigrarHasta(ctx context.Context, pool *pgxpool.Pool, logger *slog.Logger, version int64) error {
	p, sqlDB, err := proveedor(pool, logger)
	if err != nil {
		return err
	}
	defer sqlDB.Close()
	if _, err := p.DownTo(ctx, version); err != nil {
		return fmt.Errorf("db: revertir migraciones hasta %d: %w", version, err)
	}
	return nil
}
