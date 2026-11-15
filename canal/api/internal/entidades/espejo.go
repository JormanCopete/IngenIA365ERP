package entidades

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"regexp"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
)

// Espejo de habilitaciones (research R8). Reglas del arrastre:
//
//   - El cursor es el ChangeSequence del ERP: ticks UTC (100 ns), tomados antes de confirmar. Dos transacciones
//     del ERP pueden confirmar fuera de orden, así que cada arrastre pide desde el cursor menos TraslapeDelCursor y
//     se apoya en que aplicar un cambio es idempotente: un cambio sólo pisa la fila si su secuencia es >= la guardada.
//   - Cada página se aplica en una transacción junto con el avance del cursor (que nunca retrocede).
//   - La resincronización completa recorre todo desde 0 y deja 'disabled' lo que el ERP ya no lista: nunca borra.
//   - Antes de arrastrar se le pide al ERP que concilie la política automática de habilitación, a lo sumo una vez por
//     intervalo y por cooperativa (tenants.last_reconcile_at, compartido entre réplicas). Si la conciliación falla
//     se registra y se arrastra igual.

const (
	// TamanoDePagina es el limit de cada GET …/enablements.
	TamanoDePagina = 500
	// TraslapeDelCursor son 5 minutos en ticks de 100 ns: la ventana para commits fuera de orden.
	TraslapeDelCursor int64 = 3_000_000_000
	// maximoDePaginas corta un ERP que respondiera hasMore para siempre (5 millones de cambios).
	maximoDePaginas = 10_000
)

// FuenteDeHabilitaciones es lo que el espejo necesita del ERP (lo cumple *erp.Cliente).
type FuenteDeHabilitaciones interface {
	ListarCambiosDeHabilitacion(ctx context.Context, cooperativa uuid.UUID, desde int64, limite int) (erp.PaginaDeHabilitaciones, error)
	ConciliarHabilitaciones(ctx context.Context, cooperativa uuid.UUID) (erp.ConciliacionDeHabilitaciones, error)
}

// Espejo mantiene la tabla enablements al día con las habilitaciones de cada cooperativa.
type Espejo struct {
	pool             *pgxpool.Pool
	q                *gen.Queries
	fuente           FuenteDeHabilitaciones
	cadaConciliacion time.Duration
	logger           *slog.Logger
	ahora            func() time.Time
}

// NuevoEspejo crea el espejo. cadaConciliacion es la frecuencia máxima de la conciliación de la política por
// cooperativa (la misma del directorio, CANAL_FRECUENCIA_COOPERATIVAS).
func NuevoEspejo(pool *pgxpool.Pool, fuente FuenteDeHabilitaciones, cadaConciliacion time.Duration, logger *slog.Logger) *Espejo {
	if logger == nil {
		logger = slog.Default()
	}
	return &Espejo{pool: pool, q: gen.New(pool), fuente: fuente, cadaConciliacion: cadaConciliacion, logger: logger, ahora: time.Now}
}

// ResultadoDelEspejo resume un arrastre o una resincronización de una cooperativa.
type ResultadoDelEspejo struct {
	Conciliada    bool
	Paginas       int
	Recibidos     int
	Aplicados     int   // filas creadas o actualizadas
	Viejos        int   // cambios más viejos que lo guardado (llegaron por el traslape o fuera de orden)
	Rechazados    int   // filas ilegibles: se registran como error y no se reflejan
	Inhabilitados int   // sólo en la resincronización: lo que el ERP ya no lista
	Cursor        int64 // cursor al terminar
}

// ArrastrarCooperativa trae los cambios de habilitación de una cooperativa desde su cursor (con traslape).
func (e *Espejo) ArrastrarCooperativa(ctx context.Context, c gen.Tenant) (ResultadoDelEspejo, error) {
	ctx = contexto.ConCooperativa(ctx, contexto.Cooperativa{ID: c.ID, PublicID: c.TenantPublicID})
	res := ResultadoDelEspejo{Cursor: c.EnablementsCursor}
	res.Conciliada = e.conciliar(ctx, c)
	err := e.recorrer(ctx, c, max(0, c.EnablementsCursor-TraslapeDelCursor), &res, nil)
	return res, err
}

// Resincronizar recorre todas las habilitaciones de la cooperativa e inhabilita las filas del espejo que el ERP ya
// no lista. Con un listado incompleto (error a mitad) no inhabilita nada.
func (e *Espejo) Resincronizar(ctx context.Context, c gen.Tenant) (ResultadoDelEspejo, error) {
	ctx = contexto.ConCooperativa(ctx, contexto.Cooperativa{ID: c.ID, PublicID: c.TenantPublicID})
	res := ResultadoDelEspejo{Cursor: c.EnablementsCursor}
	vistas := map[uuid.UUID]struct{}{}
	if err := e.recorrer(ctx, c, 0, &res, vistas); err != nil {
		return res, err
	}
	listadas := make([]uuid.UUID, 0, len(vistas)) // nunca nil: un arreglo NULL no inhabilitaría nada
	for p := range vistas {
		listadas = append(listadas, p)
	}
	// Las filas con secuencia posterior a lo visto (y al cursor inicial) las escribió un arrastre concurrente.
	apagadas, err := e.q.DisableEnablementsNotListed(ctx, gen.DisableEnablementsNotListedParams{
		TenantID: c.ID, UpTo: res.Cursor, Listed: listadas,
	})
	if err != nil {
		return res, fmt.Errorf("entidades: inhabilitar las habilitaciones que el ERP ya no lista: %w", err)
	}
	res.Inhabilitados = len(apagadas)
	if res.Inhabilitados > 0 {
		e.logger.InfoContext(ctx, "resincronización: habilitaciones que el ERP ya no lista quedaron inhabilitadas",
			"inhabilitadas", res.Inhabilitados)
	}
	return res, nil
}

// ArrastrarTodas arrastra cada cooperativa con el canal encendido. Una que falla no detiene a las demás: se
// registra y al final se devuelve el error para que River reintente.
func (e *Espejo) ArrastrarTodas(ctx context.Context) error {
	return e.paraCadaCooperativa(ctx, "arrastre de habilitaciones", e.ArrastrarCooperativa)
}

// ResincronizarTodas resincroniza cada cooperativa con el canal encendido, con la misma regla de fallas.
func (e *Espejo) ResincronizarTodas(ctx context.Context) error {
	return e.paraCadaCooperativa(ctx, "resincronización de habilitaciones", e.Resincronizar)
}

func (e *Espejo) paraCadaCooperativa(ctx context.Context, operacion string,
	fn func(context.Context, gen.Tenant) (ResultadoDelEspejo, error)) error {
	cooperativas, err := e.q.ListEnabledTenants(ctx)
	if err != nil {
		return fmt.Errorf("entidades: listar las cooperativas para el %s: %w", operacion, err)
	}
	var fallas []error
	for _, c := range cooperativas {
		if err := ctx.Err(); err != nil {
			fallas = append(fallas, err)
			break
		}
		res, err := fn(ctx, c)
		ctxCoop := contexto.ConCooperativa(ctx, contexto.Cooperativa{ID: c.ID, PublicID: c.TenantPublicID})
		if err != nil {
			e.logger.ErrorContext(ctxCoop, operacion+" falló para una cooperativa; se sigue con las demás", "error", err)
			fallas = append(fallas, fmt.Errorf("cooperativa %s: %w", c.TenantPublicID, err))
			continue
		}
		nivel := slog.LevelDebug
		if res.Aplicados > 0 || res.Rechazados > 0 || res.Inhabilitados > 0 {
			nivel = slog.LevelInfo
		}
		e.logger.Log(ctxCoop, nivel, operacion,
			"paginas", res.Paginas, "recibidos", res.Recibidos, "aplicados", res.Aplicados, "viejos", res.Viejos,
			"rechazados", res.Rechazados, "inhabilitados", res.Inhabilitados, "cursor", res.Cursor, "conciliada", res.Conciliada)
	}
	if len(fallas) > 0 {
		return fmt.Errorf("entidades: el %s falló en %d de %d cooperativas: %w",
			operacion, len(fallas), len(cooperativas), errors.Join(fallas...))
	}
	return nil
}

// conciliar le pide al ERP materializar la política de habilitación si le toca a esta cooperativa. La reserva del
// turno es un UPDATE condicional: con varias réplicas, una sola llama.
func (e *Espejo) conciliar(ctx context.Context, c gen.Tenant) bool {
	ahora := e.ahora().UTC()
	filas, err := e.q.ClaimEnablementsReconcile(ctx, gen.ClaimEnablementsReconcileParams{
		Now: ahora, ID: c.ID, DueBefore: ahora.Add(-e.cadaConciliacion),
	})
	if err != nil {
		e.logger.ErrorContext(ctx, "no se pudo reservar el turno de conciliación de habilitaciones; se arrastra igual", "error", err)
		return false
	}
	if filas == 0 {
		return false
	}
	r, err := e.fuente.ConciliarHabilitaciones(ctx, c.TenantPublicID)
	if err != nil {
		e.logger.ErrorContext(ctx, "la conciliación de la política de habilitación falló; se arrastra igual", "error", err)
		return false
	}
	if r.Enabled > 0 || r.Disabled > 0 {
		e.logger.InfoContext(ctx, "política de habilitación conciliada en el ERP", "habilitadas", r.Enabled, "inhabilitadas", r.Disabled)
	}
	return true
}

// recorrer pide páginas desde desde mientras el ERP diga hasMore y las aplica. Si vistas no es nil, anota cada
// persona listada (resincronización).
func (e *Espejo) recorrer(ctx context.Context, c gen.Tenant, desde int64, res *ResultadoDelEspejo, vistas map[uuid.UUID]struct{}) error {
	for range maximoDePaginas {
		pagina, err := e.fuente.ListarCambiosDeHabilitacion(ctx, c.TenantPublicID, desde, TamanoDePagina)
		if err != nil {
			return fmt.Errorf("entidades: traer las habilitaciones desde %d: %w", desde, err)
		}
		if err := e.aplicarPagina(ctx, c, pagina.Items, res, vistas); err != nil {
			return err
		}
		res.Paginas++
		if !pagina.HasMore {
			return nil
		}
		if pagina.Next <= desde {
			return fmt.Errorf("entidades: el ERP respondió hasMore sin avanzar el cursor (since=%d, next=%d)", desde, pagina.Next)
		}
		desde = pagina.Next
	}
	return fmt.Errorf("entidades: el arrastre pasó de %d páginas sin terminar", maximoDePaginas)
}

func (e *Espejo) aplicarPagina(ctx context.Context, c gen.Tenant, items []erp.CambioDeHabilitacion,
	res *ResultadoDelEspejo, vistas map[uuid.UUID]struct{}) error {
	if len(items) == 0 {
		return nil
	}
	tx, err := e.pool.Begin(ctx)
	if err != nil {
		return fmt.Errorf("entidades: abrir la transacción de la página: %w", err)
	}
	defer func() {
		if err := tx.Rollback(context.WithoutCancel(ctx)); err != nil && !errors.Is(err, pgx.ErrTxClosed) {
			e.logger.ErrorContext(ctx, "no se pudo deshacer la transacción de la página de habilitaciones", "error", err)
		}
	}()
	q := e.q.WithTx(tx)

	var aplicados, viejos, rechazados int
	cursor := res.Cursor
	for _, it := range items {
		cursor = max(cursor, it.Sequence)
		if vistas != nil && it.PersonPublicID != uuid.Nil {
			vistas[it.PersonPublicID] = struct{}{}
		}
		filas, rechazado, err := e.aplicar(ctx, q, c, it)
		if err != nil {
			return err
		}
		switch {
		case rechazado:
			rechazados++
		case filas == 1:
			aplicados++
		default:
			viejos++
		}
	}
	if err := q.AdvanceEnablementsCursor(ctx, gen.AdvanceEnablementsCursorParams{Cursor: cursor, ID: c.ID}); err != nil {
		return fmt.Errorf("entidades: avanzar el cursor de habilitaciones: %w", err)
	}
	if err := tx.Commit(ctx); err != nil {
		return fmt.Errorf("entidades: confirmar la página de habilitaciones: %w", err)
	}
	res.Recibidos += len(items)
	res.Aplicados += aplicados
	res.Viejos += viejos
	res.Rechazados += rechazados
	res.Cursor = cursor
	return nil
}

// Catálogo fijo de tipos de documento del ERP (TiposDeDocumento) y forma del número normalizado.
var (
	tiposDeDocumento = map[string]bool{"CC": true, "CE": true, "PPT": true, "PEP": true, "TI": true, "PA": true, "RC": true, "NIT": true}
	numeroValido     = regexp.MustCompile(`^[A-Z0-9]{1,20}$`)
)

// aplicar refleja un cambio. Devuelve las filas tocadas, o rechazado = true si el cambio es ilegible (se registra
// como error con la persona y el motivo, nunca con el número de documento).
func (e *Espejo) aplicar(ctx context.Context, q *gen.Queries, c gen.Tenant, it erp.CambioDeHabilitacion) (int64, bool, error) {
	var estado string
	switch {
	case strings.EqualFold(it.Status, erp.HabilitacionHabilitada):
		estado = "enabled"
	case strings.EqualFold(it.Status, erp.HabilitacionInhabilitada):
		estado = "disabled"
	}
	tipo := strings.ToUpper(strings.TrimSpace(it.DocumentType))
	numero := strings.ToUpper(strings.TrimSpace(it.DocumentNumber))

	var motivo string
	switch {
	case it.PersonPublicID == uuid.Nil:
		motivo = "sin personPublicId"
	case estado == "":
		motivo = fmt.Sprintf("estado «%s» desconocido", it.Status)
	case !tiposDeDocumento[tipo]:
		motivo = fmt.Sprintf("tipo de documento «%s» fuera del catálogo", tipo)
	case !numeroValido.MatchString(numero):
		motivo = "número de documento vacío o con caracteres no admitidos"
	case it.EnabledAt.IsZero():
		motivo = "sin enabledAt"
	}

	if motivo != "" {
		if estado == "disabled" && it.PersonPublicID != uuid.Nil {
			// Una inhabilitación se refleja aunque el documento venga ilegible: el estado es lo que importa.
			e.logger.WarnContext(ctx, "inhabilitación con datos ilegibles: se refleja sólo el estado",
				"persona", it.PersonPublicID, "motivo", motivo, "secuencia", it.Sequence)
			filas, err := q.DisableEnablement(ctx, gen.DisableEnablementParams{
				ChangeSequence: it.Sequence, TenantID: c.ID, PersonPublicID: it.PersonPublicID,
			})
			if err != nil {
				return 0, false, fmt.Errorf("entidades: inhabilitar la persona %s: %w", it.PersonPublicID, err)
			}
			return filas, false, nil
		}
		e.logger.ErrorContext(ctx, "habilitación del ERP ilegible: no se refleja en el espejo",
			"persona", it.PersonPublicID, "motivo", motivo, "secuencia", it.Sequence)
		return 0, true, nil
	}

	id, err := uuid.NewV7()
	if err != nil {
		return 0, false, err
	}
	filas, err := q.UpsertEnablement(ctx, gen.UpsertEnablementParams{
		ID:             id,
		TenantID:       c.ID,
		PersonPublicID: it.PersonPublicID,
		DocumentType:   tipo,
		DocumentNumber: numero,
		Status:         estado,
		EnabledAt:      it.EnabledAt.UTC(),
		ChangeSequence: it.Sequence,
	})
	if err != nil {
		return 0, false, fmt.Errorf("entidades: guardar la habilitación de %s: %w", it.PersonPublicID, err)
	}
	return filas, false, nil
}

// ArgsArrastrarHabilitaciones es el trabajo periódico pull_enablements (todas las cooperativas).
type ArgsArrastrarHabilitaciones struct{}

// Kind identifica el trabajo en River.
func (ArgsArrastrarHabilitaciones) Kind() string { return "pull_enablements" }

// TrabajadorDeHabilitaciones ejecuta pull_enablements.
type TrabajadorDeHabilitaciones struct {
	river.WorkerDefaults[ArgsArrastrarHabilitaciones]
	Espejo *Espejo
}

// Timeout: recorre todas las cooperativas, cada una con sus llamadas al ERP.
func (*TrabajadorDeHabilitaciones) Timeout(*river.Job[ArgsArrastrarHabilitaciones]) time.Duration {
	return 10 * time.Minute
}

// Work arrastra todas; si alguna falló, River reintenta.
func (t *TrabajadorDeHabilitaciones) Work(ctx context.Context, _ *river.Job[ArgsArrastrarHabilitaciones]) error {
	return t.Espejo.ArrastrarTodas(ctx)
}

// ArgsResincronizarHabilitaciones es el trabajo periódico full_resync (nocturno).
type ArgsResincronizarHabilitaciones struct{}

// Kind identifica el trabajo en River.
func (ArgsResincronizarHabilitaciones) Kind() string { return "full_resync" }

// TrabajadorDeResincronizacion ejecuta full_resync.
type TrabajadorDeResincronizacion struct {
	river.WorkerDefaults[ArgsResincronizarHabilitaciones]
	Espejo *Espejo
}

// Timeout: el listado completo de todas las cooperativas.
func (*TrabajadorDeResincronizacion) Timeout(*river.Job[ArgsResincronizarHabilitaciones]) time.Duration {
	return time.Hour
}

// Work resincroniza todas; si alguna falló, River reintenta.
func (t *TrabajadorDeResincronizacion) Work(ctx context.Context, _ *river.Job[ArgsResincronizarHabilitaciones]) error {
	return t.Espejo.ResincronizarTodas(ctx)
}

// ArgsArrastrarCooperativa es el arrastre a pedido de una cooperativa (POST /internal/v1/tenants/{t}/sync, p. ej.
// tras una carga masiva de habilitaciones en el ERP).
type ArgsArrastrarCooperativa struct {
	TenantPublicID uuid.UUID `json:"tenant_public_id"`
}

// Kind identifica el trabajo en River.
func (ArgsArrastrarCooperativa) Kind() string { return "pull_tenant_enablements" }

// InsertOpts: pocos reintentos; si el ERP sigue caído, el arrastre periódico lo cubre.
func (ArgsArrastrarCooperativa) InsertOpts() river.InsertOpts {
	return river.InsertOpts{MaxAttempts: 5}
}

// TrabajadorDeCooperativa ejecuta pull_tenant_enablements.
type TrabajadorDeCooperativa struct {
	river.WorkerDefaults[ArgsArrastrarCooperativa]
	Espejo     *Espejo
	Directorio *Directorio
}

// Work arrastra la cooperativa. Si todavía no está en el directorio (se acaba de encender el canal), sincroniza el
// directorio primero; si aun así no está o tiene el canal apagado, cancela el trabajo (reintentar no lo arregla).
func (t *TrabajadorDeCooperativa) Work(ctx context.Context, job *river.Job[ArgsArrastrarCooperativa]) error {
	publico := job.Args.TenantPublicID
	c, err := t.Espejo.q.GetTenantByPublicID(ctx, publico)
	if errors.Is(err, pgx.ErrNoRows) && t.Directorio != nil {
		if _, err := t.Directorio.Sincronizar(ctx); err != nil {
			return err
		}
		c, err = t.Espejo.q.GetTenantByPublicID(ctx, publico)
	}
	if errors.Is(err, pgx.ErrNoRows) || (err == nil && !c.IsEnabled) {
		motivo := fmt.Errorf("entidades: la cooperativa %s no está en el directorio del canal o tiene el canal apagado", publico)
		t.Espejo.logger.WarnContext(ctx, "arrastre a pedido cancelado", "cooperativa", publico, "error", motivo)
		return river.JobCancel(motivo)
	}
	if err != nil {
		return fmt.Errorf("entidades: leer la cooperativa %s: %w", publico, err)
	}
	res, err := t.Espejo.ArrastrarCooperativa(ctx, c)
	if err != nil {
		return err
	}
	t.Espejo.logger.InfoContext(contexto.ConCooperativa(ctx, contexto.Cooperativa{ID: c.ID, PublicID: c.TenantPublicID}),
		"arrastre de habilitaciones a pedido", "recibidos", res.Recibidos, "aplicados", res.Aplicados,
		"rechazados", res.Rechazados, "cursor", res.Cursor)
	return nil
}
