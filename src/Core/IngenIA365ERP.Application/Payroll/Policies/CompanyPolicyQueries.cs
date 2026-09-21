using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Policies;

/// <summary>
/// Una política a una fecha (contracts/api.md §10.1). <see cref="Source"/> dice de dónde sale el
/// valor: de una vigencia registrada en <c>PAY_CompanyPolicies</c> o del defecto del catálogo
/// cuando nadie ha registrado ninguna. <see cref="Value"/> es nulo sólo en una clave sin defecto
/// y sin vigencia (<c>ArranqueNominaFecha</c> antes de la semilla).
/// </summary>
public sealed record CompanyPolicyDto(
    string Key,
    string? Value,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<string> Allowed,
    string? Format,
    string Description,
    string Source,
    Guid? PublicId,
    string? Notes,
    int VersionCount);

public sealed record CompanyPolicyVersionDto(
    Guid PublicId,
    string Key,
    string Value,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Notes,
    string? CreatedBy,
    DateTime CreatedAt,
    bool IsCurrent);

// ------------------------------------------------------------------ listado --

/// <summary>Las once claves del catálogo con su valor vigente a la fecha (hoy si no viene).</summary>
public sealed record ListCompanyPoliciesQuery(DateOnly? AsOf = null) : IRequest<Result<IReadOnlyList<CompanyPolicyDto>>>;

public sealed class ListCompanyPoliciesQueryValidator : AbstractValidator<ListCompanyPoliciesQuery>;

public sealed class ListCompanyPoliciesQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<ListCompanyPoliciesQuery, Result<IReadOnlyList<CompanyPolicyDto>>>
{
    public const string FuenteRegistrada = "PAY_CompanyPolicies";
    public const string FuenteDefecto = "Defecto del catálogo (sin vigencia registrada)";

    public async Task<Result<IReadOnlyList<CompanyPolicyDto>>> Handle(ListCompanyPoliciesQuery request, CancellationToken ct)
    {
        var asOf = request.AsOf ?? clock.TodayUtc;
        var filas = await db.CompanyPolicies.AsNoTracking().Where(p => !p.IsDeleted).ToListAsync(ct);
        var porClave = filas.ToLookup(f => f.Key, StringComparer.Ordinal);

        var items = new List<CompanyPolicyDto>(CompanyPolicyKeys.Todas.Count);
        foreach (var def in CompanyPolicyKeys.Todas)
        {
            var versiones = porClave[def.Clave].ToList();
            var vigente = versiones.Where(v => v.IsValidAt(asOf)).OrderByDescending(v => v.ValidFrom).FirstOrDefault();
            items.Add(vigente is null
                ? new CompanyPolicyDto(def.Clave, def.Defecto, null, null, def.Admitidos, def.Formato, def.Descripcion, FuenteDefecto, null, null, versiones.Count)
                : new CompanyPolicyDto(def.Clave, vigente.Value, vigente.ValidFrom, vigente.ValidTo, def.Admitidos, def.Formato, def.Descripcion,
                    $"{FuenteRegistrada} · vigente desde {vigente.ValidFrom:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(vigente.CreatedBy) ? string.Empty : $" · registrada por {vigente.CreatedBy}"),
                    vigente.PublicId, vigente.Notes, versiones.Count));
        }

        return Result.Success<IReadOnlyList<CompanyPolicyDto>>(items);
    }
}

// ---------------------------------------------------------------- historial --

/// <summary>Todas las vigencias de una clave, de la más reciente a la más antigua.</summary>
public sealed record GetPolicyVersionsQuery(string Key, DateOnly? AsOf = null) : IRequest<Result<IReadOnlyList<CompanyPolicyVersionDto>>>;

public sealed class GetPolicyVersionsQueryValidator : AbstractValidator<GetPolicyVersionsQuery>
{
    public GetPolicyVersionsQueryValidator() => RuleFor(x => x.Key).NotEmpty().MaximumLength(60);
}

public sealed class GetPolicyVersionsQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<GetPolicyVersionsQuery, Result<IReadOnlyList<CompanyPolicyVersionDto>>>
{
    public async Task<Result<IReadOnlyList<CompanyPolicyVersionDto>>> Handle(GetPolicyVersionsQuery request, CancellationToken ct)
    {
        var clave = request.Key.Trim();
        if (!CompanyPolicyKeys.Existe(clave))
            return Result.Failure<IReadOnlyList<CompanyPolicyVersionDto>>(CompanyPolicyErrors.KeyUnknown(clave));

        var asOf = request.AsOf ?? clock.TodayUtc;
        var versiones = await db.CompanyPolicies.AsNoTracking()
            .Where(p => p.Key == clave && !p.IsDeleted)
            .OrderByDescending(p => p.ValidFrom)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<CompanyPolicyVersionDto>>(versiones.Select(v => Map(v, v.IsValidAt(asOf))).ToList());
    }

    public static CompanyPolicyVersionDto Map(CompanyPolicy v, bool vigente) =>
        new(v.PublicId, v.Key, v.Value, v.ValidFrom, v.ValidTo, v.Notes, v.CreatedBy, v.CreatedAt, vigente);
}
