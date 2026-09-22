using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Holidays;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Vacations;

/// <summary>Lo que comparten las consultas de vacaciones.</summary>
public static class VacationQueriesSupport
{
    public static VacationBalanceSummaryDto Resumen(VacationBalanceResult b)
    {
        var e = b.Employee;
        var persona = e.Person;
        return new VacationBalanceSummaryDto(
            e.PublicId,
            persona is null ? string.Empty : $"{persona.FirstName} {persona.LastName}".Trim(),
            persona?.TaxId ?? string.Empty,
            e.JoinDate,
            b.AsOf,
            b.AccruedDays, b.OpeningDays, b.EnjoyedDays, b.CompensatedDays, b.AdjustedDays, b.SettlementPaidDays, b.PendingDays,
            b.LastEnjoymentTo, b.WorkedDays, b.SuspensionDays);
    }

    /// <summary>Los festivos del calendario de la cooperativa entre dos fechas, con su nombre.</summary>
    public static async Task<Dictionary<DateTime, string>> FestivosAsync(IApplicationDbContext db, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        await db.Holidays.AsNoTracking()
            .Where(h => h.Date >= desde && h.Date <= hasta)
            .ToDictionaryAsync(h => h.Date.ToDateTime(TimeOnly.MinValue), h => h.Name, ct);

    /// <summary>
    /// La vista previa de hábiles con la semana laboral vigente a la fecha de inicio y los festivos de la tabla (FR-015).
    /// Si el rango toca un año <b>sin ningún festivo cargado</b>, la cuenta sale igual (cada festivo se contaría como
    /// hábil) pero con el aviso <c>Payroll.Holiday.YearNotLoaded</c> y los años: la semilla cubre tres años y cada
    /// diciembre suma uno (D-20), y un disfrute se programa con meses de anticipación. Hasta la revisión de N1 el
    /// conteo callaba y un disfrute de enero de 2029 registrado en 2028 descontaba Reyes como hábil.
    /// </summary>
    public static async Task<WorkingDaysPreviewDto> ContarHabilesAsync(IApplicationDbContext db, PayrollPolicyReader policies, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var politicas = await policies.ReadAsync(desde, ct);
        var festivos = await FestivosAsync(db, desde, hasta, ct);
        var conteo = DiasHabiles.Contar(desde.ToDateTime(TimeOnly.MinValue), hasta.ToDateTime(TimeOnly.MinValue), politicas.SemanaLaboral, festivos);
        var sinFestivos = await AñosSinFestivosAsync(db, desde, hasta, ct);
        var avisos = new List<WarningDto>();
        if (sinFestivos.Count > 0) avisos.Add(HolidayErrors.YearNotLoaded(sinFestivos));
        return new WorkingDaysPreviewDto(desde, hasta, conteo.Habiles, conteo.Calendario, politicas.SemanaLaboral,
            conteo.Saltados.Select(SkippedDayDto.From).ToList(), avisos);
    }

    /// <summary>Los años del rango que no tienen ni un festivo en <c>PAY_Holidays</c>, de cualquier origen.</summary>
    public static async Task<IReadOnlyList<int>> AñosSinFestivosAsync(IApplicationDbContext db, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var primero = (short)desde.Year;
        var ultimo = (short)hasta.Year;
        var cargados = await db.Holidays.AsNoTracking()
            .Where(h => h.Year >= primero && h.Year <= ultimo)
            .Select(h => h.Year).Distinct().ToListAsync(ct);
        return Enumerable.Range(primero, ultimo - primero + 1).Where(a => !cargados.Contains((short)a)).ToList();
    }
}

// ------------------------------------------------------------------ saldos --

/// <summary>Saldo derivado de cada empleado vivo a una fecha (hoy por defecto); <c>Search</c> filtra por nombre o documento.</summary>
public sealed record GetVacationBalancesQuery(DateOnly? AsOf = null, string? Search = null) : IRequest<Result<IReadOnlyList<VacationBalanceSummaryDto>>>;

public sealed class GetVacationBalancesQueryValidator : AbstractValidator<GetVacationBalancesQuery>
{
    public GetVacationBalancesQueryValidator() => RuleFor(x => x.Search).MaximumLength(100);
}

public sealed class GetVacationBalancesQueryHandler(IApplicationDbContext db, VacationBalanceCalculator calculador, IDateTimeService clock)
    : IRequestHandler<GetVacationBalancesQuery, Result<IReadOnlyList<VacationBalanceSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<VacationBalanceSummaryDto>>> Handle(GetVacationBalancesQuery request, CancellationToken ct)
    {
        var asOf = request.AsOf ?? clock.TodayUtc;
        var asOfDt = asOf.ToDateTime(TimeOnly.MinValue);
        var empleados = db.Employees.AsNoTracking().Include(e => e.Person)
            .Where(e => e.Status != -1 && !e.IsDeleted && e.JoinDate <= asOfDt);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            empleados = empleados.Where(e => e.Person.TaxId.Contains(s) || e.Person.FirstName.Contains(s) || e.Person.LastName.Contains(s));
        }
        var fichas = await empleados.OrderBy(e => e.Person.LastName).ThenBy(e => e.Person.FirstName).ToListAsync(ct);
        if (fichas.Count == 0) return Result.Success<IReadOnlyList<VacationBalanceSummaryDto>>([]);

        var saldos = await calculador.CalcularVariosAsync(fichas, asOf, ct);
        if (saldos.IsFailure) return Result.Failure<IReadOnlyList<VacationBalanceSummaryDto>>(saldos.Error);
        return Result.Success<IReadOnlyList<VacationBalanceSummaryDto>>(fichas.Select(f => VacationQueriesSupport.Resumen(saldos.Value[f.Id])).ToList());
    }
}

/// <summary>El saldo de un empleado con su explicación y el máximo compensable (contracts/api.md §5).</summary>
public sealed record GetEmployeeVacationBalanceQuery(Guid EmployeePublicId, DateOnly? AsOf = null) : IRequest<Result<VacationBalanceDetailDto>>;

public sealed class GetEmployeeVacationBalanceQueryValidator : AbstractValidator<GetEmployeeVacationBalanceQuery>
{
    public GetEmployeeVacationBalanceQueryValidator() => RuleFor(x => x.EmployeePublicId).NotEmpty();
}

public sealed class GetEmployeeVacationBalanceQueryHandler(IApplicationDbContext db, VacationBalanceCalculator calculador, IDateTimeService clock)
    : IRequestHandler<GetEmployeeVacationBalanceQuery, Result<VacationBalanceDetailDto>>
{
    public async Task<Result<VacationBalanceDetailDto>> Handle(GetEmployeeVacationBalanceQuery request, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().Include(x => x.Person).FirstOrDefaultAsync(x => x.PublicId == request.EmployeePublicId && !x.IsDeleted, ct);
        if (e is null) return Result.Failure<VacationBalanceDetailDto>(SettlementErrors.EmployeeNotFound);

        var saldo = await calculador.CalcularAsync(e, request.AsOf ?? clock.TodayUtc, ct);
        if (saldo.IsFailure) return Result.Failure<VacationBalanceDetailDto>(saldo.Error);
        var maximo = await calculador.MaximoCompensableAsync(saldo.Value, ct);

        return Result.Success(new VacationBalanceDetailDto(
            VacationQueriesSupport.Resumen(saldo.Value),
            saldo.Value.Explanation,
            maximo.IsSuccess ? Math.Max(0m, maximo.Value.MaxDays - saldo.Value.CompensatedDays) : null,
            maximo.IsSuccess ? maximo.Value.Fraction * 100m : null));
    }
}

// ------------------------------------------------------------- movimientos --

public sealed record GetEmployeeVacationMovementsQuery(Guid EmployeePublicId, bool IncludeCancelled = true) : IRequest<Result<IReadOnlyList<VacationMovementDto>>>;

public sealed class GetEmployeeVacationMovementsQueryValidator : AbstractValidator<GetEmployeeVacationMovementsQuery>
{
    public GetEmployeeVacationMovementsQueryValidator() => RuleFor(x => x.EmployeePublicId).NotEmpty();
}

public sealed class GetEmployeeVacationMovementsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeeVacationMovementsQuery, Result<IReadOnlyList<VacationMovementDto>>>
{
    public async Task<Result<IReadOnlyList<VacationMovementDto>>> Handle(GetEmployeeVacationMovementsQuery request, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.EmployeePublicId && !x.IsDeleted, ct);
        if (e is null) return Result.Failure<IReadOnlyList<VacationMovementDto>>(SettlementErrors.EmployeeNotFound);

        var movimientos = await db.VacationMovements.AsNoTracking().Include(m => m.Employee)
            .Where(m => m.EmployeeId == e.Id && (request.IncludeCancelled || m.Status != VacationMovementStatus.Cancelled))
            .OrderByDescending(m => m.StartDate).ThenByDescending(m => m.Id)
            .ToListAsync(ct);
        var ids = movimientos.Select(m => m.Id).ToList();
        // La corrida vigente de cada movimiento: la que lo liquidó, o el borrador que lo espera.
        var corridas = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.VacationMovementId != null && ids.Contains(r.VacationMovementId.Value) && r.Status != PayrollRunStatus.Superseded)
            .OrderByDescending(r => r.Version)
            .ToListAsync(ct);
        var corridaPorMovimiento = corridas.GroupBy(r => r.VacationMovementId!.Value).ToDictionary(g => g.Key, g => g.First());

        return Result.Success<IReadOnlyList<VacationMovementDto>>(
            movimientos.Select(m => VacationMovementDto.From(m, corridaPorMovimiento.GetValueOrDefault(m.Id))).ToList());
    }
}

// ------------------------------------------------------------- vista previa --

/// <summary>
/// La vista previa obligatoria antes de guardar un disfrute (FR-015): hábiles según la
/// <c>SemanaLaboral</c> vigente a la fecha de inicio y los festivos de <c>PAY_Holidays</c>, con cada
/// día saltado explicado. El empleado es opcional: hoy la semana laboral es por empresa (R6).
/// </summary>
public sealed record PreviewWorkingDaysQuery(DateOnly From, DateOnly To, Guid? EmployeePublicId = null) : IRequest<Result<WorkingDaysPreviewDto>>;

public sealed class PreviewWorkingDaysQueryValidator : AbstractValidator<PreviewWorkingDaysQuery>
{
    public PreviewWorkingDaysQueryValidator()
    {
        RuleFor(x => x.From).NotEmpty().WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.To).NotEmpty().WithMessage("La fecha final es obligatoria.");
    }
}

public sealed class PreviewWorkingDaysQueryHandler(IApplicationDbContext db, PayrollPolicyReader policies)
    : IRequestHandler<PreviewWorkingDaysQuery, Result<WorkingDaysPreviewDto>>
{
    public async Task<Result<WorkingDaysPreviewDto>> Handle(PreviewWorkingDaysQuery request, CancellationToken ct)
    {
        if (request.To < request.From) return Result.Failure<WorkingDaysPreviewDto>(SettlementErrors.VacationDatesInvalid);
        if (request.EmployeePublicId is { } id && !await db.Employees.AsNoTracking().AnyAsync(e => e.PublicId == id && !e.IsDeleted, ct))
            return Result.Failure<WorkingDaysPreviewDto>(SettlementErrors.EmployeeNotFound);
        return Result.Success(await VacationQueriesSupport.ContarHabilesAsync(db, policies, request.From, request.To, ct));
    }
}
