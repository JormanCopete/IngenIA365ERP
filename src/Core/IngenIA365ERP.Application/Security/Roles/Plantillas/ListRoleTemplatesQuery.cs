using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Roles.Plantillas;

/// <summary>Una plantilla de rol con sus códigos ya expandidos contra el catálogo de la cooperativa (contracts/api.md §1.5).</summary>
public sealed record RoleTemplateDto(
    string Key,
    string Actor,
    string Description,
    IReadOnlyList<string> PermissionCodes,
    string Notes);

/// <summary>
/// <c>GET /api/admin/roles/templates?module=Inventory</c> (feature 012, T128; <c>Security.Roles.Create</c>): la única
/// consulta de plantillas de rol. Sin <see cref="Module"/> devuelve todas.
/// </summary>
public sealed record ListRoleTemplatesQuery(string? Module) : IRequest<Result<IReadOnlyList<RoleTemplateDto>>>;

public sealed class ListRoleTemplatesQueryValidator : AbstractValidator<ListRoleTemplatesQuery>
{
    public ListRoleTemplatesQueryValidator()
    {
        RuleFor(x => x.Module).MaximumLength(40);
    }
}

public sealed class ListRoleTemplatesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListRoleTemplatesQuery, Result<IReadOnlyList<RoleTemplateDto>>>
{
    public async Task<Result<IReadOnlyList<RoleTemplateDto>>> Handle(ListRoleTemplatesQuery request, CancellationToken ct)
    {
        var catalogo = await CatalogoDePermisos.CodigosAsync(db, ct);

        IReadOnlyList<RoleTemplateDto> plantillas = PerfilesSugeridos.DelModulo(request.Module)
            .Select(p => new RoleTemplateDto(
                p.Key, p.Actor, p.Description, PerfilesSugeridos.Expandir(p, catalogo).Codigos, p.Notes))
            .ToList();

        return Result.Success(plantillas);
    }
}

/// <summary>El catálogo sembrado de la cooperativa, como códigos <c>Resource.Action</c>.</summary>
internal static class CatalogoDePermisos
{
    public static async Task<List<string>> CodigosAsync(IApplicationDbContext db, CancellationToken ct) =>
        (await db.Permissions
            .Select(p => new { p.Resource, p.Action })
            .ToListAsync(ct))
        .Select(p => $"{p.Resource}.{p.Action}")
        .ToList();
}
