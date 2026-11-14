package avisos

import (
	"context"
	"errors"
	"fmt"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
)

// T133 (FR-012): una vez al día el canal pregunta al ERP de cada cooperativa con aviso de cuota próxima las cuotas que vencen en
// su anticipación y encola un aviso por cuota a la cuenta de la persona. La llave del aviso es la cuota y su fecha: si el
// trabajo corre dos veces el mismo día, no se avisa dos veces.

// CuotasDelERP lo cumple *erp.Cliente.
type CuotasDelERP interface {
	CuotasProximas(ctx context.Context, cooperativa uuid.UUID, dias int) ([]erp.CuotaProxima, error)
}

// ArgsRecordarCuotas es el trabajo periódico due_reminders.
type ArgsRecordarCuotas struct{}

// Kind del trabajo.
func (ArgsRecordarCuotas) Kind() string { return "due_reminders" }

// TrabajadorDeRecordatorios corre due_reminders.
type TrabajadorDeRecordatorios struct {
	river.WorkerDefaults[ArgsRecordarCuotas]
	Q         *gen.Queries
	ERP       CuotasDelERP
	Encolador Encolador
	Publico   string // https://{host}
}

func (t *TrabajadorDeRecordatorios) Work(ctx context.Context, _ *river.Job[ArgsRecordarCuotas]) error {
	_, err := t.Recordar(ctx)
	return err
}

// Recordar encola los avisos de todas las cooperativas y devuelve cuántos encoló. Una cooperativa que no responde no detiene
// a las demás.
func (t *TrabajadorDeRecordatorios) Recordar(ctx context.Context) (int, error) {
	cooperativas, err := t.Q.ListTenantsWithDueReminders(ctx)
	if err != nil {
		return 0, err
	}
	encolados := 0
	var fallas []string
	for _, c := range cooperativas {
		cuotas, err := t.ERP.CuotasProximas(ctx, c.TenantPublicID, int(c.DueReminderDays))
		if err != nil {
			fallas = append(fallas, c.DisplayName)
			continue
		}
		for _, cuota := range cuotas {
			cuenta, err := t.Q.AccountOfTenantPerson(ctx, gen.AccountOfTenantPersonParams{TenantID: c.ID, PersonPublicID: cuota.Persona})
			if errors.Is(err, pgx.ErrNoRows) {
				continue // sin cuenta activada: no hay a quién avisar
			}
			if err != nil {
				return encolados, err
			}
			if _, err := t.Encolador.Insert(ctx, ArgsEnviarAviso{
				Cuenta: cuenta.ID, Clase: CuotaProxima, Titulo: "Tu cuota vence pronto",
				Cuerpo: fmt.Sprintf("%s: %s vence el %s en %s. Puedes pagarla en el canal.", cuota.Descripcion, Pesos(cuota.Valor), Fecha(cuota.Vence), c.DisplayName),
				URL:    t.Publico + "/cooperativa", Llave: "cuota:" + c.TenantPublicID.String() + ":" + cuota.Clave + ":" + cuota.Vence,
			}, nil); err != nil {
				return encolados, err
			}
			encolados++
		}
	}
	if len(fallas) > 0 {
		return encolados, fmt.Errorf("avisos: el ERP no dio las cuotas de %s", strings.Join(fallas, ", "))
	}
	return encolados, nil
}

// Pesos escribe «$ 332.500».
func Pesos(v float64) string {
	entero := int64(v + 0.5)
	s := fmt.Sprintf("%d", entero)
	var b strings.Builder
	for i, r := range s {
		if i > 0 && (len(s)-i)%3 == 0 {
			b.WriteByte('.')
		}
		b.WriteRune(r)
	}
	return "$ " + b.String()
}

// Fecha escribe «5 de diciembre» desde «2026-12-05».
func Fecha(iso string) string {
	t, err := time.Parse(time.DateOnly, iso)
	if err != nil {
		return iso
	}
	meses := []string{"enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"}
	return fmt.Sprintf("%d de %s", t.Day(), meses[t.Month()-1])
}
