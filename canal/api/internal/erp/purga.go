package erp

import (
	"context"
	"log/slog"
	"time"

	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
)

// ArgsPurgarJti es el trabajo periódico purge_service_jti: borra los jti ya vencidos. Un jti vencido no sirve para
// nada (su token ya no valida), y no es un dato de usuario.
type ArgsPurgarJti struct{}

// Kind identifica el trabajo en River.
func (ArgsPurgarJti) Kind() string { return "purge_service_jti" }

// TrabajadorDePurga ejecuta purge_service_jti.
type TrabajadorDePurga struct {
	river.WorkerDefaults[ArgsPurgarJti]
	Q      *gen.Queries
	Logger *slog.Logger
}

// Work purga los jti vencidos hace más de un minuto.
func (t *TrabajadorDePurga) Work(ctx context.Context, _ *river.Job[ArgsPurgarJti]) error {
	filas, err := t.Q.PurgeExpiredServiceJti(ctx, time.Now().UTC().Add(-time.Minute))
	if err != nil {
		return err
	}
	if filas > 0 && t.Logger != nil {
		t.Logger.DebugContext(ctx, "jti vencidos purgados", "filas", filas)
	}
	return nil
}
