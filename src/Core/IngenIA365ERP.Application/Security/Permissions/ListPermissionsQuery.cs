using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Permissions;

/// <summary>
/// Catálogo inmutable de permisos del sistema. Devuelve la lista completa
/// (no paginada) — cabe en memoria fácilmente y la usa la matriz de roles
/// en el front. Cliente puede filtrar por <c>Module</c> (prefijo del Resource).
/// </summary>
public sealed record ListPermissionsQuery(string? ModuleFilter)
    : IRequest<Result<IReadOnlyList<PermissionCatalogItemDto>>>;

/// <summary>Proyección del catálogo de permisos.</summary>
public sealed record PermissionCatalogItemDto(
    Guid PublicId,
    string Code,
    string Resource,
    string Action,
    string Module,
    string? Description);

public sealed class ListPermissionsQueryHandler
    : IRequestHandler<ListPermissionsQuery, Result<IReadOnlyList<PermissionCatalogItemDto>>>
{
    private readonly IApplicationDbContext _db;

    public ListPermissionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<PermissionCatalogItemDto>>> Handle(
        ListPermissionsQuery request, CancellationToken ct)
    {
        var query = _db.Permissions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ModuleFilter))
        {
            var prefix = request.ModuleFilter.Trim();
            query = query.Where(p => EF.Functions.Like(p.Resource, $"{prefix}.%")
                                  || p.Resource == prefix);
        }

        var rows = await query
            .OrderBy(p => p.Resource).ThenBy(p => p.Action)
            .Select(p => new
            {
                p.PublicId,
                p.Resource,
                p.Action,
                p.Description
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new PermissionCatalogItemDto(
                r.PublicId,
                Code: $"{r.Resource}.{r.Action}",
                Resource: r.Resource,
                Action: r.Action,
                Module: ExtractModule(r.Resource),
                Description: r.Description))
            .ToList();

        return Result.Success<IReadOnlyList<PermissionCatalogItemDto>>(items);
    }

    /// <summary>El módulo es la primera parte del Resource (<c>"Admin.Tenants"</c> → <c>"Admin"</c>).</summary>
    private static string ExtractModule(string resource)
    {
        var dotIndex = resource.IndexOf('.');
        return dotIndex > 0 ? resource[..dotIndex] : resource;
    }
}
