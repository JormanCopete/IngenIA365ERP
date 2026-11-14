package entidades

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"regexp"
	"time"

	"github.com/google/uuid"
	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
)

// FuenteDeCooperativas es lo que el directorio necesita del ERP (lo cumple *erp.Cliente).
type FuenteDeCooperativas interface {
	ListarCooperativas(ctx context.Context) ([]erp.Cooperativa, error)
}

var codigoValido = regexp.MustCompile(`^[A-Z]{3}$`)

// Directorio mantiene la tabla tenants al día con lo que lista el ERP: las que aparecen se crean o se actualizan;
// las que dejan de aparecer (canal apagado) quedan inhabilitadas, nunca se borran.
type Directorio struct {
	q      *gen.Queries
	fuente FuenteDeCooperativas
	logger *slog.Logger
	ahora  func() time.Time
}

// NuevoDirectorio crea el directorio.
func NuevoDirectorio(q *gen.Queries, fuente FuenteDeCooperativas, logger *slog.Logger) *Directorio {
	if logger == nil {
		logger = slog.Default()
	}
	return &Directorio{q: q, fuente: fuente, logger: logger, ahora: time.Now}
}

// Resultado de una sincronización.
type Resultado struct {
	Vigentes      int
	Inhabilitadas int
	Omitidas      int
}

// Sincronizar trae el directorio del ERP y lo refleja en la base del canal. Si el ERP no responde, no toca nada:
// una caída del ERP no debe apagar el canal de todas las cooperativas.
func (d *Directorio) Sincronizar(ctx context.Context) (Resultado, error) {
	cooperativas, err := d.fuente.ListarCooperativas(ctx)
	if err != nil {
		return Resultado{}, fmt.Errorf("entidades: traer el directorio del ERP: %w", err)
	}

	ahora := d.ahora().UTC()
	var res Resultado
	listadas := make([]uuid.UUID, 0, len(cooperativas))
	for _, c := range cooperativas {
		if c.ChannelCode == nil || !codigoValido.MatchString(*c.ChannelCode) {
			// Sin código no hay referencias de pago posibles (R12): la cooperativa no se publica hasta tenerlo.
			d.logger.WarnContext(ctx, "cooperativa sin código de canal; se omite del directorio", "cooperativa", c.TenantPublicID)
			res.Omitidas++
			continue
		}
		marca, err := json.Marshal(c.Brand)
		if err != nil {
			return res, fmt.Errorf("entidades: serializar la marca: %w", err)
		}
		soporte, err := json.Marshal(c.Support)
		if err != nil {
			return res, fmt.Errorf("entidades: serializar el soporte: %w", err)
		}
		inactividad := c.InactivityMinutes
		if inactividad < 5 || inactividad > 15 {
			inactividad = 5
		}
		// FR-008: la sesión de pago dura 10–30 minutos; un valor fuera de rango (o un ERP anterior a T090) toma 15.
		sesionDePago := c.PaymentSessionMinutes
		if sesionDePago < 10 || sesionDePago > 30 {
			sesionDePago = 15
		}
		id, err := uuid.NewV7()
		if err != nil {
			return res, err
		}
		if _, err := d.q.UpsertTenant(ctx, gen.UpsertTenantParams{
			ID:                    id,
			TenantPublicID:        c.TenantPublicID,
			ChannelCode:           *c.ChannelCode,
			DisplayName:           c.DisplayName,
			Brand:                 marca,
			Support:               soporte,
			InactivityMinutes:     int32(inactividad),
			SmsEnabled:            c.SmsEnabled,
			PaymentSessionMinutes: int32(sesionDePago),
			RequireOtpToPay:       c.RequireOtpToPay,
			DueReminderDays:       int32(min(max(c.DueReminderDays, 0), 15)),
			LastSyncAt:            ahora,
		}); err != nil {
			return res, fmt.Errorf("entidades: guardar la cooperativa %s: %w", c.TenantPublicID, err)
		}
		listadas = append(listadas, c.TenantPublicID)
		res.Vigentes++
	}

	apagadas, err := d.q.DisableTenantsNotListed(ctx, gen.DisableTenantsNotListedParams{LastSyncAt: ahora, Listed: listadas})
	if err != nil {
		return res, fmt.Errorf("entidades: inhabilitar las que el ERP ya no lista: %w", err)
	}
	res.Inhabilitadas = len(apagadas)
	for _, a := range apagadas {
		d.logger.InfoContext(ctx, "cooperativa inhabilitada en el canal: el ERP ya no la lista", "cooperativa", a)
	}
	return res, nil
}

// ArgsSincronizarCooperativas es el trabajo periódico sync_tenants.
type ArgsSincronizarCooperativas struct{}

// Kind identifica el trabajo en River.
func (ArgsSincronizarCooperativas) Kind() string { return "sync_tenants" }

// TrabajadorDeDirectorio ejecuta sync_tenants.
type TrabajadorDeDirectorio struct {
	river.WorkerDefaults[ArgsSincronizarCooperativas]
	Directorio *Directorio
}

// Work sincroniza el directorio; un error lo reintenta River con espera creciente.
func (t *TrabajadorDeDirectorio) Work(ctx context.Context, _ *river.Job[ArgsSincronizarCooperativas]) error {
	res, err := t.Directorio.Sincronizar(ctx)
	if err != nil {
		return err
	}
	t.Directorio.logger.InfoContext(ctx, "directorio de cooperativas sincronizado",
		"vigentes", res.Vigentes, "inhabilitadas", res.Inhabilitadas, "omitidas", res.Omitidas)
	return nil
}
