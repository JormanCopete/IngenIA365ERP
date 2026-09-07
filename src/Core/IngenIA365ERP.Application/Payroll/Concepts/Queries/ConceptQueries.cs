using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Concepts.Queries;

/// <summary>Una versión de concepto hacia afuera: todo lo que el formulario y la lista de novedades necesitan.</summary>
public sealed record ConceptDefinitionDto(
    Guid PublicId,
    string Code,
    string Name,
    string Nature,
    string CalculationKind,
    decimal? FixedAmount,
    string? AmountParameterCode,
    bool ProrateByDays,
    string? BaseKind,
    decimal? Percent,
    string? PercentParameterCode,
    string? UnitKind,
    decimal? UnitFactor,
    string? TableParameterCode,
    string? ComponentConceptCodes,
    bool AffectsSalaryBase,
    bool AffectsContributionBase,
    bool AffectsBenefitsBase,
    bool AffectsWithholdingBase,
    bool IsBenefitRelated,
    bool AllowsRepeatInPeriod,
    decimal? MaxQuantity,
    decimal? MaxAmount,
    int ApplicableClasses,
    IReadOnlyList<string> ApplicableClassNames,
    bool RequiresDates,
    bool RequiresQuantity,
    bool RequiresAmount,
    bool IsAutomatic,
    bool ReducesWorkedDays,
    string Origin,
    int? LegacyConceptId,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsActive,
    bool HasAccounts)
{
    /// <summary>Se registra como novedad: no es automático o admite datos de la novedad.</summary>
    public bool RegistrableComoNovedad => !IsAutomatic || RequiresAmount || RequiresQuantity || RequiresDates;
}

public static class ConceptDefinitionMapper
{
    public static ConceptDefinitionDto ToDto(PayrollConceptDefinition c, bool hasAccounts) => new(
        c.PublicId, c.Code, c.Name, c.Nature.ToString(), c.CalculationKind.ToString(),
        c.FixedAmount, c.AmountParameterCode, c.ProrateByDays,
        c.BaseKind?.ToString(), c.Percent, c.PercentParameterCode,
        c.UnitKind?.ToString(), c.UnitFactor, c.TableParameterCode, c.ComponentConceptCodes,
        c.AffectsSalaryBase, c.AffectsContributionBase, c.AffectsBenefitsBase, c.AffectsWithholdingBase, c.IsBenefitRelated,
        c.AllowsRepeatInPeriod, c.MaxQuantity, c.MaxAmount,
        c.ApplicableClasses, ClassNames(c.ApplicableClasses),
        c.RequiresDates, c.RequiresQuantity, c.RequiresAmount, c.IsAutomatic, c.ReducesWorkedDays,
        c.Origin.ToString(), c.LegacyConceptId, c.ValidFrom, c.ValidTo, c.IsActive, hasAccounts);

    public static IReadOnlyList<string> ClassNames(int mask)
    {
        if (mask == 0) return ["Todas"];
        return Enum.GetValues<EmployeeClass>().Where(c => (mask & (1 << (int)c)) != 0).Select(c => c.ToString()).ToList();
    }
}

/// <summary>Versión vigente por código a una fecha (hoy si no se indica). Con <c>IncludeInactive</c> también las desactivadas.</summary>
public sealed record ListConceptDefinitionsQuery(DateTime? AsOf = null, bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<ConceptDefinitionDto>>>;

public sealed class ListConceptDefinitionsQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<ListConceptDefinitionsQuery, Result<IReadOnlyList<ConceptDefinitionDto>>>
{
    public async Task<Result<IReadOnlyList<ConceptDefinitionDto>>> Handle(ListConceptDefinitionsQuery request, CancellationToken ct)
    {
        var asOf = (request.AsOf ?? clock.UtcNow).Date;
        var versiones = await db.PayrollConceptDefinitions.AsNoTracking()
            .Where(c => c.ValidFrom <= asOf && (c.ValidTo == null || c.ValidTo >= asOf))
            .Where(c => request.IncludeInactive || c.IsActive)
            .ToListAsync(ct);
        var conCuentas = (await db.PayrollConceptDefinitionAccounts.AsNoTracking().Select(a => a.ConceptCode).Distinct().ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var vigentes = versiones
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.ValidFrom).First())
            .OrderBy(c => c.Nature).ThenBy(c => c.Code, StringComparer.Ordinal)
            .Select(c => ConceptDefinitionMapper.ToDto(c, conCuentas.Contains(c.Code)))
            .ToList();

        return Result.Success<IReadOnlyList<ConceptDefinitionDto>>(vigentes);
    }
}

/// <summary>Todas las versiones de un código, de la más reciente a la más antigua (FR-029).</summary>
public sealed record ListConceptVersionsQuery(string Code) : IRequest<Result<IReadOnlyList<ConceptDefinitionDto>>>;

public sealed class ListConceptVersionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListConceptVersionsQuery, Result<IReadOnlyList<ConceptDefinitionDto>>>
{
    public async Task<Result<IReadOnlyList<ConceptDefinitionDto>>> Handle(ListConceptVersionsQuery request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var versiones = await db.PayrollConceptDefinitions.AsNoTracking()
            .Where(c => c.Code == code)
            .OrderByDescending(c => c.ValidFrom)
            .ToListAsync(ct);
        if (versiones.Count == 0)
            return Result.Failure<IReadOnlyList<ConceptDefinitionDto>>(new Error("Payroll.ConceptNotFound", $"No existe el concepto {code}."));
        var tieneCuentas = await db.PayrollConceptDefinitionAccounts.AsNoTracking().AnyAsync(a => a.ConceptCode == code, ct);
        return Result.Success<IReadOnlyList<ConceptDefinitionDto>>(versiones.Select(v => ConceptDefinitionMapper.ToDto(v, tieneCuentas)).ToList());
    }
}

// Principio VIII: toda consulta lleva su validador.
public sealed class ListConceptDefinitionsQueryValidator : AbstractValidator<ListConceptDefinitionsQuery>;

public sealed class ListConceptVersionsQueryValidator : AbstractValidator<ListConceptVersionsQuery>
{
    public ListConceptVersionsQueryValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
}
