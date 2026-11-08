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

// Periodico es un trabajo que se encola solo cada Intervalo (y una vez al arrancar), o según Horario.
type Periodico struct {
	ID        string
	Intervalo time.Duration
	// Horario, si no es nil, manda sobre Intervalo y el trabajo NO corre al arrancar: un trabajo nocturno no debe
	// repetirse en cada despliegue (p. ej. Diario(3, 0, HoraDeColombia)).
	Horario river.PeriodicSchedule
	Args    func() river.JobArgs
}

// HoraDeColombia: Colombia no tiene horario de verano, así que basta una zona fija (sin depender de tzdata).
var HoraDeColombia = time.FixedZone("COT", -5*60*60)

// Diario es un horario de una vez al día, a hora:minuto de la zona dada.
func Diario(hora, minuto int, zona *time.Location) river.PeriodicSchedule {
	return diario{hora: hora, minuto: minuto, zona: zona}
}

type diario struct {
	hora, minuto int
	zona         *time.Location
}

// Next devuelve la siguiente hora:minuto estrictamente posterior a t.
func (d diario) Next(t time.Time) time.Time {
	local := t.In(d.zona)
	siguiente := time.Date(local.Year(), local.Month(), local.Day(), d.hora, d.minuto, 0, 0, d.zona)
	if !siguiente.After(local) {
		siguiente = siguiente.AddDate(0, 0, 1)
	}
	return siguiente
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
		horario, alArrancar := p.Horario, false
		if horario == nil {
			horario, alArrancar = river.PeriodicInterval(p.Intervalo), true
		}
		periodicos = append(periodicos, river.NewPeriodicJob(
			horario,
			func() (river.JobArgs, *river.InsertOpts) { return args(), nil },
			&river.PeriodicJobOpts{ID: p.ID, RunOnStart: alArrancar},
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
