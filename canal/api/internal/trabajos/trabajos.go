package trabajos

import (
	"context"
	"fmt"
	"log/slog"
	"time"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/riverqueue/river"
	"github.com/riverqueue/river/riverdriver/riverpgxv5"
	"github.com/riverqueue/river/rivermigrate"
)

// Las tablas de River (river_job, river_leader, …) son de la librería, no del modelo del canal: las crea y
// actualiza rivermigrate al arrancar, dentro del mismo cerrojo de migración que goose (db.ConCerrojoDeMigracion).

// MigrarRiver aplica las migraciones de River.
func MigrarRiver(ctx context.Context, pool *pgxpool.Pool) error {
	migrador, err := rivermigrate.New(riverpgxv5.New(pool), nil)
	if err != nil {
		return fmt.Errorf("trabajos: crear el migrador de River: %w", err)
	}
	if _, err := migrador.Migrate(ctx, rivermigrate.DirectionUp, nil); err != nil {
		return fmt.Errorf("trabajos: migrar River: %w", err)
	}
	return nil
}

// Periodico es un trabajo que se encola solo cada Intervalo.
type Periodico struct {
	ID        string
	Intervalo time.Duration
	Args      func() river.JobArgs
}

// Opciones del cliente de trabajos.
type Opciones struct {
	Logger     *slog.Logger
	MaxWorkers int
	Workers    *river.Workers
	Periodicos []Periodico
}

// NuevoCliente crea el cliente de River sobre el pool del canal. Los trabajos periódicos corren en la réplica que
// River elige como líder, así que no se duplican aunque haya varias.
func NuevoCliente(pool *pgxpool.Pool, o Opciones) (*river.Client[pgx.Tx], error) {
	if o.MaxWorkers <= 0 {
		o.MaxWorkers = 10
	}
	periodicos := make([]*river.PeriodicJob, 0, len(o.Periodicos))
	for _, p := range o.Periodicos {
		args := p.Args
		periodicos = append(periodicos, river.NewPeriodicJob(
			river.PeriodicInterval(p.Intervalo),
			func() (river.JobArgs, *river.InsertOpts) { return args(), nil },
			&river.PeriodicJobOpts{ID: p.ID, RunOnStart: true},
		))
	}
	cliente, err := river.NewClient(riverpgxv5.New(pool), &river.Config{
		Logger:       o.Logger,
		Queues:       map[string]river.QueueConfig{river.QueueDefault: {MaxWorkers: o.MaxWorkers}},
		Workers:      o.Workers,
		PeriodicJobs: periodicos,
		JobTimeout:   2 * time.Minute,
	})
	if err != nil {
		return nil, fmt.Errorf("trabajos: crear el cliente de River: %w", err)
	}
	return cliente, nil
}
