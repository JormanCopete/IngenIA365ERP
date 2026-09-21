using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Terminations;

// ------------------------------------------------------------------- listar --

/// <summary>Las terminaciones registradas, por año de retiro, estado y empleado (contracts/api.md §3.4 <c>GET /</c>).</summary>
public sealed record ListTerminationsQuery(int? Year = null, TerminationStatus? Status = null, Guid? EmployeePublicId = null)
    : IRequest<Result<IReadOnlyList<TerminationListItemDto>>>;

public sealed class ListTerminationsQueryValidator : AbstractValidator<ListTerminationsQuery>
{
    public ListTerminationsQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(1900, 2200).When(x => x.Year is not null).WithMessage("Año fuera de rango.");
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
    }
}

public sealed class ListTerminationsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListTerminationsQuery, Result<IReadOnlyList<TerminationListItemDto>>>
{
    public async Task<Result<IReadOnlyList<TerminationListItemDto>>> Handle(ListTerminationsQuery request, CancellationToken ct)
    {
        var consulta =
            from t in db.EmploymentTerminations.AsNoTracking()
            join r in db.TerminationReasons.AsNoTracking() on t.TerminationReasonId equals r.Id
            join e in db.Employees.AsNoTracking() on t.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            select new { t, r, e.PublicId, Nombre = (p.FirstName + " " + p.LastName).Trim(), p.TaxId };
        if (request.Year is { } anio)
        {
            var desde = new DateOnly(anio, 1, 1);
            var hasta = new DateOnly(anio, 12, 31);
            consulta = consulta.Where(x => x.t.TerminationDate >= desde && x.t.TerminationDate <= hasta);
        }
        if (request.Status is { } estado) consulta = consulta.Where(x => x.t.Status == estado);
        if (request.EmployeePublicId is { } empleado) consulta = consulta.Where(x => x.PublicId == empleado);

        var filas = await consulta.OrderByDescending(x => x.t.TerminationDate).ThenByDescending(x => x.t.Id).ToListAsync(ct);
        var ids = filas.Select(x => x.t.Id).ToList();
        var corridas = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.Kind == PayrollRunKind.Settlement && r.TerminationId != null && ids.Contains(r.TerminationId.Value))
            .Select(r => new { r.TerminationId, r.PublicId, r.Version, r.Status, r.TotalNet, r.ApprovedAt })
            .ToListAsync(ct);
        // La corrida que representa a la terminación: la viva (borrador o aprobada), o la última versión.
        var corridaPor = corridas.GroupBy(r => r.TerminationId!.Value).ToDictionary(g => g.Key, g =>
            g.OrderByDescending(r => r.Status is PayrollRunStatus.Approved or PayrollRunStatus.Draft or PayrollRunStatus.Stale ? 1 : 0)
             .ThenByDescending(r => r.Version).First());

        var resultado = filas.Select(x =>
        {
            corridaPor.TryGetValue(x.t.Id, out var run);
            return new TerminationListItemDto(
                x.t.PublicId, run?.PublicId, run?.Version, run?.Status.ToString(), x.PublicId, x.Nombre, x.TaxId,
                x.t.TerminationDate, x.r.Code, x.r.Name, x.r.GeneratesSeverancePay, x.t.ContractTypeAtTermination, x.t.ContractEndDate,
                x.t.Status, run?.TotalNet ?? 0m, run?.ApprovedAt, x.t.Notes, x.t.SettlementDocumentAttachmentPublicId);
        }).ToList();
        return Result.Success<IReadOnlyList<TerminationListItemDto>>(resultado);
    }
}

// ------------------------------------------------------- sanción moratoria --

/// <summary>
/// «Si pagara hoy, la sanción sería…» (CST art. 65; research R7): un día del último salario por cada
/// día de retardo desde el retiro, hasta el tope en meses del parámetro
/// <c>SANCION_MORA_ART65_TOPE_MESES</c> vigente al retiro. Informativo bajo demanda: nunca una
/// línea, porque su procedencia depende de la mala fe y la decide un juez.
/// </summary>
public sealed record GetLatePaymentPenaltyQuery(Guid RunPublicId, DateOnly? AsOf = null) : IRequest<Result<LatePaymentPenaltyDto>>;

public sealed class GetLatePaymentPenaltyQueryValidator : AbstractValidator<GetLatePaymentPenaltyQuery>
{
    public GetLatePaymentPenaltyQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class GetLatePaymentPenaltyQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<GetLatePaymentPenaltyQuery, Result<LatePaymentPenaltyDto>>
{
    public const string ParametroTopeMeses = "SANCION_MORA_ART65_TOPE_MESES";

    public async Task<Result<LatePaymentPenaltyDto>> Handle(GetLatePaymentPenaltyQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<LatePaymentPenaltyDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Settlement || run.CutoffDate is null || run.EmployeeId is null)
            return Result.Failure<LatePaymentPenaltyDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Settlement));

        var retiro = run.CutoffDate.Value;
        var hoy = request.AsOf ?? clock.TodayUtc;
        var retiroDt = retiro.ToDateTime(TimeOnly.MinValue);

        // El último salario mensual: el cambio vigente al retiro, o el de la ficha.
        var salario = await db.SalaryChanges.AsNoTracking()
            .Where(s => s.EmployeeId == run.EmployeeId && s.EffectiveDate <= retiroDt)
            .OrderByDescending(s => s.EffectiveDate).Select(s => (decimal?)s.NewSalary).FirstOrDefaultAsync(ct)
            ?? await db.Employees.AsNoTracking().Where(e => e.Id == run.EmployeeId).Select(e => e.Salary).FirstAsync(ct);

        var parametros = await db.PayrollLegalParameters.AsNoTracking()
            .Where(p => p.Code == ParametroTopeMeses && p.ValidFrom <= retiroDt && (p.ValidTo == null || p.ValidTo >= retiroDt))
            .ToListAsync(ct);
        if (parametros.Count == 0)
            return Result.Failure<LatePaymentPenaltyDto>(SettlementErrors.ParametersMissing([ParametroTopeMeses], retiroDt));
        var topeMeses = (int)new ParameterSet(parametros, retiroDt).Value(ParametroTopeMeses);

        var diasDeRetardo = Math.Max(0, hoy.DayNumber - retiro.DayNumber);
        var diasCobrables = Math.Min(diasDeRetardo, topeMeses * 30);
        var diario = Math.Round(salario / 30m, 2, MidpointRounding.AwayFromZero);
        var sancion = Math.Round(diario * diasCobrables, 0, MidpointRounding.AwayFromZero);
        var nota = diasDeRetardo == 0
            ? "Pagar el mismo día del retiro no causa sanción."
            : diasDeRetardo > topeMeses * 30
                ? $"Pasados los {topeMeses} meses del tope la sanción deja de correr por día y se causan intereses moratorios a la tasa máxima; esa parte no se calcula aquí."
                : "Cálculo informativo (CST art. 65): la sanción sólo procede si hay mala fe y la declara un juez. No es una línea de la liquidación.";

        return Result.Success(new LatePaymentPenaltyDto(retiro, hoy, diasDeRetardo, diasCobrables, salario, diario, sancion, topeMeses, nota));
    }
}
