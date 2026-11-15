package pagos

import (
	"context"
	"time"

	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
)

// T093 (FR-040, R11): la pasarela no reintenta las notificaciones, así que la consulta periódica es obligatoria. Los
// pendientes se consultan cada ConsultaFrecuente (15 min por defecto, CANAL_FRECUENCIA_PENDIENTES) durante sus primeras
// 24 h, una vez al día hasta los 7 días, y después se alertan una sola vez. La sonda sólo actúa sobre pendientes.

const (
	// VentanaFrecuente: las primeras horas de un pendiente, con consulta frecuente.
	VentanaFrecuente = 24 * time.Hour
	// VentanaDeSonda: después de esto el pendiente ya no se consulta solo: se alerta.
	VentanaDeSonda = 7 * 24 * time.Hour
	// PorCorrida limita cuántos intentos consulta una corrida.
	PorCorrida = 200
)

// Sondear consulta los pendientes que tocan y alerta los que pasaron la ventana. Devuelve cuántos consultó y cuántos
// alertó.
func (s *Servicio) Sondear(ctx context.Context) (consultados, alertados int, err error) {
	ctx = contexto.ConActor(ctx, contexto.ActorSistema)
	ahora := s.ahora()
	lote, err := s.q.ListAttemptsToProbe(ctx, gen.ListAttemptsToProbeParams{
		WindowSince:    ahora.Add(-VentanaDeSonda),
		RecentSince:    ahora.Add(-VentanaFrecuente),
		FrequentBefore: ahora.Add(-s.o.ConsultaFrecuente),
		DailyBefore:    ahora.Add(-24 * time.Hour),
		MaxRows:        PorCorrida,
	})
	if err != nil {
		return 0, 0, err
	}
	for _, a := range lote {
		if ctx.Err() != nil {
			return consultados, alertados, ctx.Err()
		}
		if _, err := s.Actualizar(contexto.ConIntento(ctx, a.ID), a.ID, FuenteSonda); err != nil {
			s.o.Logger.WarnContext(ctx, "la sonda no pudo consultar el intento", "intento", a.ID, "error", err)
		}
		consultados++
	}

	viejos, err := s.q.ListStaleAttempts(ctx, ahora.Add(-VentanaDeSonda))
	if err != nil {
		return consultados, 0, err
	}
	for _, a := range viejos {
		// La alerta a la cooperativa por su consola llega con la atención (US12); por ahora, auditoría y log de error.
		s.o.Logger.ErrorContext(ctx, "ALERTA: intento de pago pendiente por más de 7 días; la cooperativa debe revisarlo con la pasarela",
			"intento", a.ID, "referencia", a.Reference, "cooperativa", a.TenantID)
		ctxIntento := contexto.ConCooperativa(contexto.ConIntento(ctx, a.ID), contexto.Cooperativa{ID: a.TenantID})
		s.auditar(ctxIntento, "Canal.Pago.PendienteSinResolver", a.ID.String(), map[string]any{"reference": a.Reference, "createdAt": a.CreatedAt})
		if err := s.q.MarkAttemptStaleAlerted(ctx, gen.MarkAttemptStaleAlertedParams{ID: a.ID, At: ahora}); err != nil {
			return consultados, alertados, err
		}
		alertados++
	}
	return consultados, alertados, nil
}

// ArgsSondearPendientes es el trabajo periódico probe_pending_attempts.
type ArgsSondearPendientes struct{}

// Kind del trabajo.
func (ArgsSondearPendientes) Kind() string { return "probe_pending_attempts" }

// TrabajadorDeSonda corre la sonda.
type TrabajadorDeSonda struct {
	river.WorkerDefaults[ArgsSondearPendientes]
	Pagos *Servicio
}

// Work corre una pasada.
func (t *TrabajadorDeSonda) Work(ctx context.Context, _ *river.Job[ArgsSondearPendientes]) error {
	consultados, alertados, err := t.Pagos.Sondear(ctx)
	if consultados > 0 || alertados > 0 {
		t.Pagos.o.Logger.InfoContext(ctx, "sonda de pagos pendientes", "consultados", consultados, "alertados", alertados)
	}
	return err
}
