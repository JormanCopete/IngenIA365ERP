using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Concepts.Queries;

// ----------------------------------------------------------- catálogo heredado --

/// <summary>Un concepto del catálogo heredado (<c>PAY_PayrollConcepts</c>), sólo lectura, con lo traducible a la definición nueva y si ya se tradujo.</summary>
public sealed record LegacyConceptDto(
    int LegacyConceptId,
    int ConceptCode,
    string Name,
    string ShortName,
    int ConceptClass,
    int Nature,
    decimal Value,
    decimal Factor,
    int Base,
    bool IsAutomatic,
    bool AffectsSalary,
    bool AffectsBenefits,
    bool AffectsWithholding,
    bool IsBenefit,
    bool IntegralSalary,
    string? TranslatedToCode,
    string TranslationHint);

public sealed record ListLegacyConceptsQuery(string? Search = null) : IRequest<Result<IReadOnlyList<LegacyConceptDto>>>;

public sealed class ListLegacyConceptsQueryValidator : AbstractValidator<ListLegacyConceptsQuery>
{
    public ListLegacyConceptsQueryValidator() => RuleFor(x => x.Search).MaximumLength(100);
}

public sealed class ListLegacyConceptsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListLegacyConceptsQuery, Result<IReadOnlyList<LegacyConceptDto>>>
{
    public async Task<Result<IReadOnlyList<LegacyConceptDto>>> Handle(ListLegacyConceptsQuery request, CancellationToken ct)
    {
        var query = db.PayrollConcepts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(c => c.Name.Contains(s) || c.ShortName.Contains(s));
        }
        var heredados = await query.OrderBy(c => c.ConceptCode).ToListAsync(ct);
        var traducidos = await db.PayrollConceptDefinitions.AsNoTracking()
            .Where(d => d.LegacyConceptId != null)
            .Select(d => new { d.LegacyConceptId, d.Code })
            .ToListAsync(ct);
        var porLegado = traducidos.GroupBy(t => t.LegacyConceptId!.Value).ToDictionary(g => g.Key, g => g.First().Code);

        var dtos = heredados.Select(c => new LegacyConceptDto(
            c.Id, c.ConceptCode, c.Name, c.ShortName, c.ConceptClass, c.Nature, c.Value, c.Factor, c.Base,
            c.IsAutomatic.Trim().ToUpperInvariant() is "S" or "1" or "Y",
            c.AffectsSalary != 0, c.AffectsBenefits != 0, c.AffectsWithholding != 0, c.IsBenefit != 0, c.IntegralSalary != 0,
            porLegado.GetValueOrDefault(c.Id),
            Pista(c))).ToList();

        return Result.Success<IReadOnlyList<LegacyConceptDto>>(dtos);
    }

    /// <summary>Lo que se puede deducir sin inventar (spec, edge case «sin forma equivalente»).</summary>
    private static string Pista(Domain.Entities.Payroll.PayrollConcept c)
    {
        if (c.Factor > 0m && c.Value == 0m) return "Probable «cantidad × unidad» con factor de recargo; confirme la unidad.";
        if (c.Value > 0m && c.Value <= 100m) return "Probable «porcentaje sobre base»; confirme la base.";
        if (c.Value > 100m) return "Probable «valor fijo».";
        return "Sin forma equivalente evidente: revise el concepto heredado antes de traducirlo.";
    }
}

// ------------------------------------------------------------------ prueba en seco --

/// <summary>Una línea del ensayo, con su explicación, sin guardar nada.</summary>
public sealed record DryRunLineDto(string ConceptCode, string ConceptName, string Nature, decimal? Quantity, decimal? BaseAmount, decimal? Factor, decimal Amount, JsonElement Explanation);

public sealed record DryRunResultDto(IReadOnlyList<DryRunLineDto> Lines, IReadOnlyList<string> Refusals, IReadOnlyList<string> Skips, RunTotalsDto Totals);

/// <summary>
/// «Probar en seco» (US3): calcula al empleado en el período con la definición dada como
/// si fuera la vigente (y, si no es automática, con una novedad de ensayo) y devuelve las
/// líneas de ese concepto con su explicación. No persiste.
/// </summary>
public sealed record DryRunConceptQuery(
    ConceptDefinitionInput Definition,
    Guid EmployeePublicId,
    Guid PeriodPublicId,
    decimal? Quantity = null,
    decimal? Amount = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IRequest<Result<DryRunResultDto>>;

public sealed class DryRunConceptQueryValidator : AbstractValidator<DryRunConceptQuery>
{
    public DryRunConceptQueryValidator()
    {
        RuleFor(x => x.Definition).NotNull().SetValidator(new ConceptDefinitionInputValidator());
        RuleFor(x => x.EmployeePublicId).NotEmpty();
        RuleFor(x => x.PeriodPublicId).NotEmpty();
    }
}

public sealed class DryRunConceptQueryHandler(IApplicationDbContext db, CalculationInputLoader loader, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<DryRunConceptQuery, Result<DryRunResultDto>>
{
    public async Task<Result<DryRunResultDto>> Handle(DryRunConceptQuery request, CancellationToken ct)
    {
        var period = await db.PayPeriods.AsNoTracking().Include(p => p.PayrollPlan).FirstOrDefaultAsync(p => p.PublicId == request.PeriodPublicId, ct);
        if (period is null) return Result.Failure<DryRunResultDto>(new Error("Payroll.PeriodNotFound", "No existe el período indicado."));

        var candidato = request.Definition.ToEntity(ConceptOrigin.Custom, null, clock.UtcNow, user.UserName);
        candidato.ValidFrom = period.StartDate.Date; // rige para el ensayo aunque la vigencia real sea futura
        var reglas = await ConceptDefinitionRules.ValidateAsync(db, candidato, ct);
        if (reglas.IsFailure) return Result.Failure<DryRunResultDto>(reglas.Error);

        var batch = await loader.LoadAsync(period, ct);
        var empleado = batch.Employees.FirstOrDefault(e => e.Employee.PublicId == request.EmployeePublicId);
        if (empleado is null)
            return Result.Failure<DryRunResultDto>(new Error("Payroll.EmployeeNotActiveInPeriod", "El empleado no está vigente en el período o pertenece a otro plan."));
        if (batch.MissingRequiredParameters.Count > 0)
            return Result.Failure<DryRunResultDto>(new Error("Payroll.LegalParameterMissing",
                $"No hay vigencia para: {string.Join(", ", batch.MissingRequiredParameters)}."));

        // La definición de ensayo reemplaza a la vigente del mismo código (si la hay).
        var conceptos = batch.Concepts.Where(c => !c.Code.Equals(candidato.Code, StringComparison.OrdinalIgnoreCase)).Append(candidato).ToList();
        var novedades = empleado.Novelties.ToList();
        if (!candidato.IsAutomatic || request.Quantity is not null || request.Amount is not null || request.StartDate is not null)
        {
            novedades.Add(new NoveltyInput
            {
                PublicId = Guid.Empty,
                ConceptCode = candidato.Code,
                Quantity = request.Quantity,
                Amount = request.Amount,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Description = "Novedad de ensayo (prueba en seco)",
            });
        }

        var input = new CalculationInput
        {
            Period = batch.PeriodInput,
            Employee = empleado.Input,
            Novelties = novedades,
            Concepts = conceptos,
            Parameters = batch.Parameters,
            Policies = batch.Policies,
        };

        CalculationResult resultado;
        try { resultado = new PayrollCalculationEngine().Calculate(input); }
        catch (CalculationRefusedException ex) { return Result.Failure<DryRunResultDto>(new Error("Payroll.CalculationRefused", ex.Message)); }

        var lineas = resultado.Lines
            .Where(l => l.Code.Equals(candidato.Code, StringComparison.OrdinalIgnoreCase))
            .Select(l => new DryRunLineDto(l.Code, l.Name, l.Nature.ToString(), l.Quantity, l.BaseAmount, l.Factor, l.Amount,
                JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(l.Explanation, RunJson.Options), RunJson.Options)))
            .ToList();
        var t = resultado.Totals;
        return Result.Success(new DryRunResultDto(lineas, resultado.Refusals, resultado.Skips.Where(s => s.StartsWith(candidato.Code, StringComparison.OrdinalIgnoreCase)).ToList(),
            new RunTotalsDto(t.Earnings, t.Deductions, t.EmployerContributions, t.Provisions, t.Net, t.RoundingAdjustment)));
    }
}
