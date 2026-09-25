using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.OpeningBalances;

/// <summary>Lo que comparten las consultas y los comandos de saldos iniciales.</summary>
public static class BenefitBalanceRules
{
    /// <summary>La fila que manda: la de corte más reciente y, a igual corte, la última registrada (el ajuste sobre la apertura).</summary>
    public static EmployeeBenefitOpeningBalance? Vigente(IEnumerable<EmployeeBenefitOpeningBalance> filas) =>
        filas.OrderByDescending(f => f.AsOfDate).ThenByDescending(f => f.Id).FirstOrDefault();

    /// <summary>
    /// Fecha desde la que la nómina corre en esta plataforma: la política
    /// <c>ArranqueNominaFecha</c> vigente a la fecha (por <see cref="PayrollPolicyReader"/>, el único
    /// lector de políticas); si nadie la registró, el inicio del primer período de la cooperativa;
    /// sin períodos, nula (nadie «ingresó antes del arranque»).
    /// </summary>
    public static async Task<DateOnly?> ArranqueAsync(IApplicationDbContext db, PayrollPolicyReader politicas, DateOnly asOf, CancellationToken ct)
    {
        var vigentes = await politicas.ReadAsync(asOf, ct);
        if (vigentes.ArranqueNominaFecha is { } fecha) return fecha;

        var primerPeriodo = await db.PayPeriods.AsNoTracking()
            .OrderBy(p => p.StartDate)
            .Select(p => (DateTime?)p.StartDate)
            .FirstOrDefaultAsync(ct);
        return primerPeriodo is { } inicio ? DateOnly.FromDateTime(inicio) : null;
    }

    /// <summary>
    /// Las liquidaciones aprobadas (no reversadas) del empleado que leyeron su saldo inicial: toda corrida
    /// especial con corte igual o posterior a la fecha de corte del saldo más antiguo, porque el cargador
    /// toma el saldo por <c>AsOfDate &lt;= corte</c> sin mirar quién lo consumió. <c>ConsumedByRunId</c> sólo
    /// guarda la más antigua; la edición y la reversión preguntan aquí (revisión N1 de la feature 010).
    /// </summary>
    public static async Task<List<PayrollRun>> ConsumidoresAsync(IApplicationDbContext db, int employeeId, IEnumerable<EmployeeBenefitOpeningBalance> filas,
        int? exceptoRunId, CancellationToken ct)
    {
        var vivas = filas.Where(f => !f.IsDeleted).ToList();
        if (vivas.Count == 0) return [];
        var desde = vivas.Min(f => f.AsOfDate);
        return await db.PayrollRuns.AsNoTracking()
            .Where(r => r.Kind != PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Approved && r.Id != exceptoRunId
                        && r.CutoffDate != null && r.CutoffDate >= desde
                        && db.PayrollRunEmployees.Any(re => re.PayrollRunId == r.Id && re.EmployeeId == employeeId))
            .OrderBy(r => r.CutoffDate).ThenBy(r => r.Id)
            .ToListAsync(ct);
    }

    public static BenefitBalanceRowDto Map(EmployeeBenefitOpeningBalance f, Guid? ajustaA) => new(
        f.PublicId, f.Kind, f.AsOfDate, f.PendingVacationDays, f.AccruedSeverance, f.AccruedSeveranceInterest, f.AccruedServiceBonus,
        f.ServiceBonusDaysAccrued, f.SeveranceDaysAccrued, f.Notes, ajustaA, f.AdjustmentReason,
        f.ConsumedByRun is null ? null : new BenefitBalanceConsumerDto(f.ConsumedByRun.PublicId, f.ConsumedByRun.Kind),
        f.CreatedBy, f.CreatedAt, f.UpdatedBy, f.UpdatedAt);
}

// ------------------------------------------------------------------ listado --

/// <summary>
/// Un renglón por empleado vivo con su saldo vigente. <c>OnlyMissing</c> deja sólo a quien
/// ingresó antes del arranque de la nómina y no tiene saldo: son los que saldrían cortos en la
/// prima y las cesantías (R3).
/// </summary>
public sealed record ListBenefitBalancesQuery(DateOnly? AsOf = null, bool OnlyMissing = false, string? Search = null)
    : IRequest<Result<IReadOnlyList<BenefitBalanceSummaryDto>>>;

public sealed class ListBenefitBalancesQueryValidator : AbstractValidator<ListBenefitBalancesQuery>
{
    public ListBenefitBalancesQueryValidator() => RuleFor(x => x.Search).MaximumLength(100);
}

public sealed class ListBenefitBalancesQueryHandler(IApplicationDbContext db, PayrollPolicyReader politicas, IDateTimeService clock)
    : IRequestHandler<ListBenefitBalancesQuery, Result<IReadOnlyList<BenefitBalanceSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<BenefitBalanceSummaryDto>>> Handle(ListBenefitBalancesQuery request, CancellationToken ct)
    {
        var asOf = request.AsOf ?? clock.TodayUtc;
        var arranque = await BenefitBalanceRules.ArranqueAsync(db, politicas, asOf, ct);
        var arranqueDt = arranque?.ToDateTime(TimeOnly.MinValue);

        var empleados = db.Employees.AsNoTracking().Include(e => e.Person).Where(e => e.Status != -1 && !e.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            empleados = empleados.Where(e => e.Person.TaxId.Contains(s) || e.Person.FirstName.Contains(s) || e.Person.LastName.Contains(s)
                || (e.Person.OtherNames != null && e.Person.OtherNames.Contains(s))
                || (e.Person.SecondLastName != null && e.Person.SecondLastName.Contains(s)));
        }
        var fichas = await empleados.OrderBy(e => e.Person.LastName).ThenBy(e => e.Person.FirstName).ToListAsync(ct);

        var ids = fichas.Select(f => f.Id).ToList();
        var saldos = await db.EmployeeBenefitOpeningBalances.AsNoTracking()
            .Include(b => b.ConsumedByRun)
            .Where(b => ids.Contains(b.EmployeeId) && !b.IsDeleted)
            .ToListAsync(ct);
        var porEmpleado = saldos.ToLookup(b => b.EmployeeId);

        var items = new List<BenefitBalanceSummaryDto>();
        foreach (var e in fichas)
        {
            var filas = porEmpleado[e.Id].ToList();
            var vigente = BenefitBalanceRules.Vigente(filas);
            var antesDelArranque = arranqueDt is { } a && e.JoinDate < a;
            if (request.OnlyMissing && (vigente is not null || !antesDelArranque)) continue;

            var consumidores = filas.Where(f => f.ConsumedByRun is not null)
                .Select(f => new BenefitBalanceConsumerDto(f.ConsumedByRun!.PublicId, f.ConsumedByRun.Kind))
                .DistinctBy(c => c.RunPublicId)
                .ToList();

            items.Add(new BenefitBalanceSummaryDto(
                e.PublicId, NombreDePersona.Completo(e.Person), e.Person.TaxId, e.JoinDate, antesDelArranque,
                vigente?.AsOfDate, vigente?.PendingVacationDays, vigente?.AccruedSeverance, vigente?.AccruedSeveranceInterest, vigente?.AccruedServiceBonus,
                consumidores, consumidores.Count == 0,
                vigente?.UpdatedAt ?? vigente?.CreatedAt, vigente?.UpdatedBy ?? vigente?.CreatedBy));
        }

        return Result.Success<IReadOnlyList<BenefitBalanceSummaryDto>>(items);
    }
}

// ------------------------------------------------------------------ detalle --

public sealed record GetBenefitBalanceQuery(Guid EmployeePublicId) : IRequest<Result<BenefitBalanceDetailDto>>;

public sealed class GetBenefitBalanceQueryValidator : AbstractValidator<GetBenefitBalanceQuery>
{
    public GetBenefitBalanceQueryValidator() => RuleFor(x => x.EmployeePublicId).NotEmpty();
}

public sealed class GetBenefitBalanceQueryHandler(IApplicationDbContext db, PayrollPolicyReader politicas, IDateTimeService clock)
    : IRequestHandler<GetBenefitBalanceQuery, Result<BenefitBalanceDetailDto>>
{
    public async Task<Result<BenefitBalanceDetailDto>> Handle(GetBenefitBalanceQuery request, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().Include(x => x.Person).FirstOrDefaultAsync(x => x.PublicId == request.EmployeePublicId, ct);
        if (e is null) return Result.Failure<BenefitBalanceDetailDto>(BenefitBalanceErrors.EmployeeNotFound);

        var filas = await db.EmployeeBenefitOpeningBalances.AsNoTracking()
            .Include(b => b.ConsumedByRun)
            .Where(b => b.EmployeeId == e.Id && !b.IsDeleted)
            .ToListAsync(ct);
        var publicIdPorId = filas.ToDictionary(f => f.Id, f => f.PublicId);
        var arranque = await BenefitBalanceRules.ArranqueAsync(db, politicas, clock.TodayUtc, ct);
        var vigente = BenefitBalanceRules.Vigente(filas);

        BenefitBalanceRowDto Map(EmployeeBenefitOpeningBalance f) =>
            BenefitBalanceRules.Map(f, f.AdjustsBalanceId is { } id && publicIdPorId.TryGetValue(id, out var p) ? p : null);

        return Result.Success(new BenefitBalanceDetailDto(
            e.PublicId, NombreDePersona.Completo(e.Person), e.Person.TaxId, e.JoinDate, arranque,
            arranque is { } a && e.JoinDate < a.ToDateTime(TimeOnly.MinValue),
            vigente is null ? null : Map(vigente),
            filas.OrderByDescending(f => f.AsOfDate).ThenByDescending(f => f.Id).Select(Map).ToList()));
    }
}
