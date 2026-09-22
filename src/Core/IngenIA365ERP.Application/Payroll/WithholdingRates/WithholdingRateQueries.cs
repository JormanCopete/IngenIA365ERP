using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Withholding;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingRates;

public sealed record ListWithholdingRateCalculationsQuery(short? Year = null, byte? Semester = null, WithholdingRateCalculationStatus? Status = null, Guid? EmployeePublicId = null)
    : IRequest<Result<IReadOnlyList<WithholdingRateItemDto>>>;

public sealed class ListWithholdingRateCalculationsQueryValidator : AbstractValidator<ListWithholdingRateCalculationsQuery>;

public sealed class ListWithholdingRateCalculationsQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<ListWithholdingRateCalculationsQuery, Result<IReadOnlyList<WithholdingRateItemDto>>>
{
    public async Task<Result<IReadOnlyList<WithholdingRateItemDto>>> Handle(ListWithholdingRateCalculationsQuery request, CancellationToken ct)
    {
        var q = from c in db.WithholdingRateCalculations.AsNoTracking()
                join e in db.Employees.AsNoTracking() on c.EmployeeId equals e.Id
                join p in db.People.AsNoTracking() on e.PersonId equals p.Id
                select new { Calc = c, e.PublicId, p.FirstName, p.LastName, p.TaxId, e.Id };
        if (request.Year is { } y) q = q.Where(x => x.Calc.TargetYear == y);
        if (request.Semester is { } s) q = q.Where(x => x.Calc.TargetSemester == s);
        if (request.Status is { } st) q = q.Where(x => x.Calc.Status == st);
        if (request.EmployeePublicId is { } ep) q = q.Where(x => x.PublicId == ep);
        var lista = await q.OrderByDescending(x => x.Calc.TargetYear).ThenByDescending(x => x.Calc.TargetSemester).ThenBy(x => x.LastName).ThenByDescending(x => x.Calc.Version).Take(500).ToListAsync(ct);

        var hoy = clock.UtcNow;
        var ids = lista.Select(x => x.Id).Distinct().ToList();
        var vigentes = await db.EmployeeWithholdingRates.AsNoTracking()
            .Where(t => ids.Contains(t.EmployeeId) && t.ValidFrom <= hoy && (t.ValidTo == null || t.ValidTo >= hoy)).ToListAsync(ct);
        var vigentePor = vigentes.GroupBy(t => t.EmployeeId).ToDictionary(g => g.Key, g => (decimal?)g.OrderByDescending(t => t.ValidFrom).First().RatePercent);

        return Result.Success<IReadOnlyList<WithholdingRateItemDto>>(lista.Select(x =>
        {
            var (_, desde, hasta) = WithholdingRateInputLoader.Semestre(x.Calc.TargetYear, x.Calc.TargetSemester);
            return WithholdingRateMappers.Item(x.Calc, x.PublicId, $"{x.FirstName} {x.LastName}", x.TaxId, desde, hasta, vigentePor.GetValueOrDefault(x.Id));
        }).ToList());
    }
}

public sealed record GetWithholdingRateCalculationQuery(Guid CalculationPublicId) : IRequest<Result<WithholdingRateDetailDto>>;

public sealed class GetWithholdingRateCalculationQueryValidator : AbstractValidator<GetWithholdingRateCalculationQuery>
{
    public GetWithholdingRateCalculationQueryValidator() => RuleFor(x => x.CalculationPublicId).NotEmpty();
}

public sealed class GetWithholdingRateCalculationQueryHandler(IApplicationDbContext db) : IRequestHandler<GetWithholdingRateCalculationQuery, Result<WithholdingRateDetailDto>>
{
    private sealed record Explicacion(string? DivisorSource, string? RangeText, string? TableCode, DateTime? TableValidFrom, List<ExplanationStep>? Steps, List<ExplanationStep>? Depuration, string? Rounding);

    public async Task<Result<WithholdingRateDetailDto>> Handle(GetWithholdingRateCalculationQuery request, CancellationToken ct)
    {
        var x = await (from calc in db.WithholdingRateCalculations.AsNoTracking().Include(w => w.Months).Include(w => w.TableParameter).ThenInclude(tp => tp!.Ranges)
                       join e in db.Employees.AsNoTracking() on calc.EmployeeId equals e.Id
                       join p in db.People.AsNoTracking() on e.PersonId equals p.Id
                       where calc.PublicId == request.CalculationPublicId
                       select new { Calc = calc, e.PublicId, p.FirstName, p.LastName, p.TaxId }).FirstOrDefaultAsync(ct);
        if (x is null) return Result.Failure<WithholdingRateDetailDto>(WithholdingRateErrors.CalculationNotFound);
        var c = x.Calc;
        var (_, desde, hasta) = WithholdingRateInputLoader.Semestre(c.TargetYear, c.TargetSemester);
        var ex = JsonSerializer.Deserialize<Explicacion>(c.ExplanationJson, CalculateWithholdingRatesCommandHandler.JsonWeb) ?? new Explicacion(null, null, null, null, null, null, null);
        var meses = c.Months.OrderBy(m => m.Year).ThenBy(m => m.Month).Select(m => new WithholdingRateMonthDto(m.Year, m.Month, m.GrossIncome, m.MandatoryContributions, m.IncludedSpecialRuns,
            JsonSerializer.Deserialize<List<FixedRateSourceRun>>(m.SourceRunsJson, CalculateWithholdingRatesCommandHandler.JsonWeb) ?? [])).ToList();
        var t = c.TableParameter;
        var tabla = new WithholdingRateTableDto(ex.TableCode ?? t?.Code ?? string.Empty, ex.TableValidFrom ?? t?.ValidFrom ?? default,
            c.PlanTableUsed ? "Tabla de retención del plan de nómina" : t?.Source ?? string.Empty,
            (t?.Ranges ?? []).Where(r => !r.IsDeleted).OrderBy(r => r.Order).Select(r => new WithholdingRateRangeDto(r.FromValue, r.ToValue, r.Rate, r.FixedValue)).ToList());
        var secuencia = Enum.TryParse<DepurationSequence>(c.DepurationSequence, out var seq) ? seq : DepurationSequence.DepurateThenDivide;
        var resumen = WithholdingRateMappers.Item(c, x.PublicId, $"{x.FirstName} {x.LastName}", x.TaxId, desde, hasta, null);
        return Result.Success(new WithholdingRateDetailDto(resumen, meses, c.Divisor, ex.DivisorSource ?? string.Empty, secuencia,
            c.TotalGrossIncome, c.TotalMandatoryContributions, c.TotalDeclaredDeductions, c.TotalExemptIncome, c.DepuratedBase,
            c.AverageMonthlyBase, c.UvtValueUsed, c.AverageInUvt, tabla, ex.RangeText ?? string.Empty, c.TheoreticalWithholding, c.RatePercent,
            ex.Rounding ?? string.Empty, ex.Steps ?? [], ex.Depuration ?? []));
    }
}
