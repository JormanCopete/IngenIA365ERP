using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Novelties;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Vacations;

/// <summary>Un período de nómina que el disfrute cubre y lo que le toca.</summary>
public sealed record TramoDeNovedad(PayPeriod Periodo, DateOnly From, DateOnly To, int Days, bool Retroactive, PayPeriod? Destino)
{
    /// <summary>El período donde queda la novedad: el mismo, o el abierto siguiente si aquél está aprobado.</summary>
    public PayPeriod PeriodoDestino => Destino ?? Periodo;
}

/// <summary>Un rango de días del disfrute que ningún período del plan cubre ni cubrirá por traslado.</summary>
public sealed record DiasSinPeriodo(DateOnly From, DateOnly To);

/// <summary>
/// Lo que salió de planear: los tramos por período, los avisos y los días que quedarían sin novedad
/// (<see cref="SinPeriodo"/>): ningún período existe para ellos y no los cubre el traslado del último
/// período. Aprobar con días sin período es un rechazo (D-33), porque la ordinaria volvería a pagarlos.
/// </summary>
public sealed record PlanDeNovedades(IReadOnlyList<TramoDeNovedad> Tramos, IReadOnlyList<WarningDto> Warnings, string ConceptCode, IReadOnlyList<DiasSinPeriodo> SinPeriodo)
{
    public PlanDeNovedades(IReadOnlyList<TramoDeNovedad> tramos, IReadOnlyList<WarningDto> warnings, string conceptCode)
        : this(tramos, warnings, conceptCode, []) { }

    public IReadOnlyList<VacationNoveltyDto> ComoDto() => Tramos
        .Select(t => new VacationNoveltyDto(t.PeriodoDestino.PublicId, Etiqueta(t.PeriodoDestino), t.From, t.To, t.Days, t.Retroactive,
            t.Retroactive ? t.Periodo.PublicId : null, ConceptCode))
        .ToList();

    public static string Etiqueta(PayPeriod p) => $"{p.StartDate:dd/MM/yyyy}–{p.EndDate:dd/MM/yyyy}";
}

/// <summary>
/// La novedad que un disfrute de vacaciones deja en la nómina ordinaria (feature 010, D-01, R6):
/// una por cada período del plan del empleado que las fechas cubren, con
/// <c>NoveltyOrigin.VacationLeave</c> y el movimiento que la originó. El concepto lo decide la
/// política vigente al corte: <c>AUSENCIA_VACACIONES</c> cuando la liquidación paga los días
/// (<c>VacacionesPagoAnticipado</c> = sí, la ordinaria sólo descuenta los días del salario) o
/// <c>VACACIONES</c> cuando los paga la ordinaria.
///
/// <para>
/// Un período <b>aprobado</b> es inmutable (FR-005): la novedad se ofrece como ajuste retroactivo en
/// el período abierto siguiente del plan (<c>RetroactiveOfPeriodId</c>), igual que en la ordinaria;
/// si quien registra no lo aceptó, la respuesta es <c>Payroll.Vacation.PeriodApproved</c> con el
/// período y el destino. Si el último período existente termina antes que el disfrute, la novedad
/// de ese período lleva los días que sobran como traslado (<c>CarryOverDays</c>) y
/// <see cref="CarryOverNoveltiesService"/> la materializa al abrir el período siguiente (FR-003).
/// Anular un movimiento anula sus novedades sólo en períodos aún editables.
/// </para>
/// </summary>
public sealed class VacationNoveltyPlanner(
    IApplicationDbContext db,
    PayrollPolicyReader policies,
    IPayrollRunStaleMarker staleMarker,
    CarryOverNoveltiesService carryOver,
    IDateTimeService clock,
    ICurrentUserService user)
{
    public const string AnuladaPorReversion = "Anulada por la reversión de la liquidación de vacaciones";
    public const string AnuladaPorCancelacion = "Anulada por la cancelación del movimiento de vacaciones";
    public const string PeriodMissingCode = "Payroll.Vacation.PeriodMissing";

    /// <summary>Qué novedad deja el disfrute según la política vigente a la fecha.</summary>
    public async Task<string> ConceptoAsync(DateOnly asOf, CancellationToken ct)
    {
        var politicas = await policies.ReadAsync(asOf, ct);
        return politicas.VacacionesPagoAnticipado ? WellKnownConceptCodes.VacationLeave : WellKnownConceptCodes.Vacation;
    }

    /// <summary>
    /// Reparte el disfrute entre los períodos del plan del empleado. Con <paramref name="aceptarRetroactivo"/>
    /// en falso, un período aprobado es un rechazo (<c>PeriodApproved</c>); en verdadero, la novedad va al
    /// período abierto siguiente como ajuste retroactivo; sin período abierto siguiente, rechazo igual.
    /// Los días sin ningún período y sin traslado salen en <see cref="PlanDeNovedades.SinPeriodo"/>.
    /// </summary>
    public async Task<Result<PlanDeNovedades>> PlanearAsync(Employee employee, DateOnly from, DateOnly to, DateOnly asOf, bool aceptarRetroactivo, CancellationToken ct)
    {
        var concepto = await ConceptoAsync(asOf, ct);
        var desde = from.ToDateTime(TimeOnly.MinValue);
        var hasta = to.ToDateTime(TimeOnly.MinValue);
        var periodos = await db.PayPeriods
            .Where(p => p.PayrollPlanId == employee.PayrollPlanId && p.StartDate <= hasta && p.EndDate >= desde)
            .OrderBy(p => p.StartDate)
            .ToListAsync(ct);

        var tramos = new List<TramoDeNovedad>();
        var avisos = new List<WarningDto>();
        var sinPeriodo = new List<DiasSinPeriodo>();
        var cursor = desde;
        for (var i = 0; i < periodos.Count; i++)
        {
            var p = periodos[i];
            var tramoDesde = p.StartDate.Date > desde ? p.StartDate.Date : desde;
            var tramoHasta = p.EndDate.Date < hasta ? p.EndDate.Date : hasta;
            var esElUltimo = i == periodos.Count - 1;
            // El último período existente carga con lo que sigue: el traslado lo crea al abrir el próximo.
            var finNovedad = esElUltimo && hasta > p.EndDate.Date ? hasta : tramoHasta;
            var dias = CalendarConventions.Days(tramoDesde, tramoHasta);

            if (p.StartDate.Date > cursor)
                sinPeriodo.Add(new DiasSinPeriodo(DateOnly.FromDateTime(cursor), DateOnly.FromDateTime(p.StartDate.Date.AddDays(-1))));
            cursor = p.EndDate.Date.AddDays(1);

            if (p.Status is PayPeriodStatus.Open or PayPeriodStatus.Calculated)
            {
                tramos.Add(new TramoDeNovedad(p, DateOnly.FromDateTime(tramoDesde), DateOnly.FromDateTime(finNovedad), dias, Retroactive: false, Destino: null));
                continue;
            }

            var destino = await db.PayPeriods
                .Where(x => x.PayrollPlanId == p.PayrollPlanId && x.StartDate > p.EndDate && (x.Status == PayPeriodStatus.Open || x.Status == PayPeriodStatus.Calculated))
                .OrderBy(x => x.StartDate)
                .FirstOrDefaultAsync(ct);
            if (!aceptarRetroactivo || destino is null)
                return Result.Failure<PlanDeNovedades>(SettlementErrors.VacationPeriodApproved(p.PublicId, destino?.PublicId));
            tramos.Add(new TramoDeNovedad(p, DateOnly.FromDateTime(tramoDesde), DateOnly.FromDateTime(finNovedad), dias, Retroactive: true, Destino: destino));
        }

        if (periodos.Count == 0)
        {
            sinPeriodo.Add(new DiasSinPeriodo(from, to));
            avisos.Add(new WarningDto(PeriodMissingCode,
                $"El plan del empleado no tiene períodos de nómina entre el {from:dd/MM/yyyy} y el {to:dd/MM/yyyy}: sin ellos no queda la novedad de ausencia y la nómina pagaría esos días como salario. Cree los períodos antes de aprobar.",
                new { from, to }));
        }
        else if (cursor <= hasta)
        {
            avisos.Add(new WarningDto(PeriodMissingCode,
                $"El último período existente termina el {cursor.AddDays(-1):dd/MM/yyyy}: los días del disfrute posteriores quedan como traslado y entrarán al período siguiente cuando se cree (FR-003).",
                new { from = DateOnly.FromDateTime(cursor), to }));
        }
        foreach (var hueco in sinPeriodo.Where(h => periodos.Count > 0))
            avisos.Add(new WarningDto(PeriodMissingCode,
                $"No hay período de nómina del plan que cubra del {hueco.From:dd/MM/yyyy} al {hueco.To:dd/MM/yyyy}: esos días no tendrían novedad de ausencia. Cree el período antes de aprobar.",
                new { from = hueco.From, to = hueco.To }));

        return Result.Success(new PlanDeNovedades(tramos, avisos, concepto, sinPeriodo));
    }

    /// <summary>
    /// Deja las novedades del plan en la base (sin guardar: el comando guarda) y marca desactualizados
    /// los borradores de los períodos tocados. Devuelve cuántas creó.
    /// </summary>
    public async Task<Result<int>> CrearAsync(Employee employee, VacationMovement movimiento, PlanDeNovedades plan, CancellationToken ct)
    {
        if (plan.Tramos.Count == 0) return Result.Success(0);
        var ahora = clock.UtcNow;
        var quien = user.UserName;
        var creadas = 0;

        foreach (var t in plan.Tramos)
        {
            var destino = t.PeriodoDestino;
            var editable = await NoveltyRules.EnsureEditableAsync(db, destino, ct);
            if (editable.IsFailure) return Result.Failure<int>(editable.Error);

            var definicion = await db.PayrollConceptDefinitions.AsNoTracking()
                .Where(c => c.Code == plan.ConceptCode && c.IsActive && c.ValidFrom <= destino.EndDate && (c.ValidTo == null || c.ValidTo >= destino.EndDate))
                .OrderByDescending(c => c.ValidFrom)
                .FirstOrDefaultAsync(ct);
            if (definicion is null)
                return Result.Failure<int>(new Error("Payroll.ConceptNotFound",
                    $"No hay una versión vigente al {destino.EndDate:dd/MM/yyyy} del concepto {plan.ConceptCode}: reaplique la semilla de nómina antes de aprobar."));
            if (!definicion.AppliesTo(employee.EmployeeClass))
                return Result.Failure<int>(new Error("Payroll.ConceptNotApplicable",
                    $"El concepto {definicion.Name} no aplica a la clase de empleado {employee.EmployeeClass}: la ausencia por vacaciones no se puede registrar."));

            var desde = t.From.ToDateTime(TimeOnly.MinValue);
            var hasta = t.To.ToDateTime(TimeOnly.MinValue);
            var total = CalendarConventions.Days(desde, hasta);
            var novedad = new PayrollNovelty
            {
                PayPeriodId = destino.Id,
                EmployeeId = employee.Id,
                ConceptDefinitionId = definicion.Id,
                ConceptCode = definicion.Code,
                StartDate = desde,
                EndDate = hasta,
                // Retroactiva: las fechas caen fuera del período destino, así que los días viajan como
                // cantidad para que la ordinaria los vea (la misma limitación del ajuste retroactivo de la 005).
                Quantity = t.Retroactive ? t.Days : null,
                DaysInPeriod = t.Days,
                CarryOverDays = Math.Max(0, total - t.Days),
                Notes = t.Retroactive
                    ? $"Vacaciones del {movimiento.StartDate:dd/MM/yyyy} al {movimiento.EndDate:dd/MM/yyyy}: ajuste retroactivo del período {PlanDeNovedades.Etiqueta(t.Periodo)} (ya aprobado)"
                    : $"Vacaciones del {movimiento.StartDate:dd/MM/yyyy} al {movimiento.EndDate:dd/MM/yyyy}",
                Status = NoveltyStatus.Active,
                Origin = NoveltyOrigin.VacationLeave,
                RetroactiveOfPeriodId = t.Retroactive ? t.Periodo.Id : null,
                VacationMovementId = movimiento.Id,
                CreatedAt = ahora,
                CreatedBy = quien,
            };
            db.PayrollNovelties.Add(novedad);
            creadas++;
            await staleMarker.MarkStaleAsync(destino.Id, $"novedad {definicion.Code} de vacaciones registrada", ct);
        }
        return Result.Success(creadas);
    }

    /// <summary>
    /// Anula las novedades vivas que dejó el movimiento en períodos aún editables, con sus traslados.
    /// Las de períodos aprobados no se tocan (la nómina ya las pagó): se devuelven para avisar.
    /// </summary>
    public async Task<(int Anuladas, IReadOnlyList<PayPeriod> EnPeriodosAprobados)> AnularAsync(VacationMovement movimiento, string motivo, CancellationToken ct)
    {
        var novedades = await db.PayrollNovelties.Include(n => n.PayPeriod)
            .Where(n => n.VacationMovementId == movimiento.Id && n.Status == NoveltyStatus.Active)
            .ToListAsync(ct);
        var ahora = clock.UtcNow;
        var quien = user.UserName;
        var anuladas = 0;
        var aprobados = new List<PayPeriod>();
        foreach (var n in novedades)
        {
            if (n.PayPeriod is not { Status: PayPeriodStatus.Open or PayPeriodStatus.Calculated })
            {
                if (n.PayPeriod is not null) aprobados.Add(n.PayPeriod);
                continue;
            }
            n.Status = NoveltyStatus.Cancelled;
            n.StatusReason = motivo;
            n.UpdatedAt = ahora;
            n.UpdatedBy = quien;
            anuladas++;
            anuladas += await carryOver.CancelCarryOversAsync(n, motivo, ct);
            await staleMarker.MarkStaleAsync(n.PayPeriodId, $"novedad {n.ConceptCode} de vacaciones anulada", ct);
        }
        return (anuladas, aprobados);
    }
}
