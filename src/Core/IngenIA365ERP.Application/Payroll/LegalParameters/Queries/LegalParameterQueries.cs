using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.LegalParameters.Queries;

public sealed record LegalParameterRangeDto(decimal FromValue, decimal? ToValue, decimal? Rate, decimal? FixedValue, int Order);

public sealed record LegalParameterDto(
    Guid PublicId,
    string Code,
    string Name,
    string Kind,
    decimal? Value,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Source,
    string? RangeUnitParameterCode,
    bool RangeIsMarginal,
    IReadOnlyList<LegalParameterRangeDto> Ranges,
    bool IsRequired,
    int VersionCount,
    string? CreatedBy,
    DateTime CreatedAt);

/// <summary>La vigencia por código a una fecha, y los códigos requeridos sin vigencia para el año en curso y el siguiente (la misma comprobación que hace el cálculo).</summary>
public sealed record LegalParametersOverviewDto(
    DateTime AsOf,
    IReadOnlyList<LegalParameterDto> Items,
    IReadOnlyList<string> MissingThisYear,
    IReadOnlyList<string> MissingNextYear);

public sealed record ListLegalParametersQuery(DateTime? AsOf = null) : IRequest<Result<LegalParametersOverviewDto>>;

public sealed class ListLegalParametersQueryValidator : AbstractValidator<ListLegalParametersQuery>;

public sealed class ListLegalParametersQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<ListLegalParametersQuery, Result<LegalParametersOverviewDto>>
{
    public async Task<Result<LegalParametersOverviewDto>> Handle(ListLegalParametersQuery request, CancellationToken ct)
    {
        var asOf = (request.AsOf ?? clock.UtcNow).Date;
        var todos = await db.PayrollLegalParameters.AsNoTracking().Include(p => p.Ranges).ToListAsync(ct);

        var porCodigo = todos.GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var items = new List<LegalParameterDto>();
        foreach (var g in porCodigo.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            // La vigente a la fecha; si no hay ninguna (todas futuras o vencidas), la más reciente, para que el código se vea.
            var vigente = g.Where(p => p.IsValidAt(asOf)).OrderByDescending(p => p.ValidFrom).FirstOrDefault()
                          ?? g.OrderByDescending(p => p.ValidFrom).First();
            items.Add(Map(vigente, g.Count()));
        }

        var finDeEsteAno = new DateTime(asOf.Year, 12, 31);
        var finDelSiguiente = new DateTime(asOf.Year + 1, 12, 31);
        var faltanEste = PayrollCalculationEngine.MissingRequiredParameters(todos, finDeEsteAno);
        var faltanSiguiente = PayrollCalculationEngine.MissingRequiredParameters(todos, finDelSiguiente);

        return Result.Success(new LegalParametersOverviewDto(asOf, items, faltanEste, faltanSiguiente));
    }

    public static LegalParameterDto Map(PayrollLegalParameter p, int versiones) => new(
        p.PublicId, p.Code, p.Name, p.Kind.ToString(), p.Value, p.ValidFrom, p.ValidTo, p.Source,
        p.RangeUnitParameterCode, p.RangeIsMarginal,
        p.Ranges.Where(r => !r.IsDeleted).OrderBy(r => r.Order).ThenBy(r => r.FromValue)
            .Select(r => new LegalParameterRangeDto(r.FromValue, r.ToValue, r.Rate, r.FixedValue, r.Order)).ToList(),
        LegalParameterCodes.Required.Contains(p.Code, StringComparer.OrdinalIgnoreCase),
        versiones, p.CreatedBy, p.CreatedAt);
}

public sealed record ListLegalParameterVersionsQuery(string Code) : IRequest<Result<IReadOnlyList<LegalParameterDto>>>;

public sealed class ListLegalParameterVersionsQueryValidator : AbstractValidator<ListLegalParameterVersionsQuery>
{
    public ListLegalParameterVersionsQueryValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
}

public sealed class ListLegalParameterVersionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListLegalParameterVersionsQuery, Result<IReadOnlyList<LegalParameterDto>>>
{
    public async Task<Result<IReadOnlyList<LegalParameterDto>>> Handle(ListLegalParameterVersionsQuery request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var versiones = await db.PayrollLegalParameters.AsNoTracking().Include(p => p.Ranges)
            .Where(p => p.Code == code).OrderByDescending(p => p.ValidFrom).ToListAsync(ct);
        if (versiones.Count == 0)
            return Result.Failure<IReadOnlyList<LegalParameterDto>>(new Error("Payroll.LegalParameterNotFound", $"No existe el parámetro {code}."));
        return Result.Success<IReadOnlyList<LegalParameterDto>>(versiones.Select(v => ListLegalParametersQueryHandler.Map(v, versiones.Count)).ToList());
    }
}
