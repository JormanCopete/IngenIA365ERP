package trabajos

import (
	"context"
	"sync/atomic"
	"testing"
	"time"

	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
)

type argsDePrueba struct{}

func (argsDePrueba) Kind() string { return "prueba_periodica" }

type trabajadorDePrueba struct {
	river.WorkerDefaults[argsDePrueba]
	corridas *atomic.Int32
}

func (t *trabajadorDePrueba) Work(context.Context, *river.Job[argsDePrueba]) error {
	t.corridas.Add(1)
	return nil
}

// El arranque del canal: migrar River dos veces (idempotente), arrancar el cliente y que el periódico corra al inicio.
func TestRiverMigraYCorreLosPeriodicos(t *testing.T) {
	pool := dbtest.Migrada(t)
	ctx := context.Background()
	for i := 0; i < 2; i++ {
		if err := MigrarRiver(ctx, pool); err != nil {
			t.Fatalf("migrar River (vuelta %d): %v", i+1, err)
		}
	}

	var corridas atomic.Int32
	workers := river.NewWorkers()
	river.AddWorker(workers, &trabajadorDePrueba{corridas: &corridas})
	cliente, err := NuevoCliente(pool, Opciones{
		Logger:     dbtest.Silencioso(),
		Workers:    workers,
		Periodicos: []Periodico{{ID: "prueba", Intervalo: time.Hour, Args: func() river.JobArgs { return argsDePrueba{} }}},
	})
	if err != nil {
		t.Fatalf("crear el cliente: %v", err)
	}
	if err := cliente.Start(ctx); err != nil {
		t.Fatalf("arrancar: %v", err)
	}
	defer func() {
		detener, cancelar := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancelar()
		if err := cliente.Stop(detener); err != nil {
			t.Errorf("detener: %v", err)
		}
	}()

	limite := time.Now().Add(30 * time.Second)
	for corridas.Load() == 0 && time.Now().Before(limite) {
		time.Sleep(200 * time.Millisecond)
	}
	if corridas.Load() == 0 {
		t.Fatalf("el trabajo periódico con RunOnStart no corrió en 30 s")
	}
}
