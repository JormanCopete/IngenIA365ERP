using IngenIA365ERP.Application.Admin.Tenants.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Tenants.ListTenants;

/// <summary>
/// Listado SaaS-global de cooperativas. Solo para operadores con permiso
/// <c>Admin.Tenants.View</c>. Filtros: substring en Name/Nit/Subdomain,
/// IncludeSuspended (default false).
/// </summary>
public sealed record ListTenantsQuery(
    string? Search,
    bool IncludeSuspended,
    PageRequest Paging) : IRequest<Result<PagedResult<TenantListItemDto>>>;

public sealed class ListTenantsQueryHandler
    : IRequestHandler<ListTenantsQuery, Result<PagedResult<TenantListItemDto>>>
{
    private readonly IAdminDbContext _db;

    public ListTenantsQueryHandler(IAdminDbContext db) => _db = db;

    public async Task<Result<PagedResult<TenantListItemDto>>> Handle(
        ListTenantsQuery request, CancellationToken ct)
    {
        var paging = request.Paging ?? new PageRequest();
        var page = paging.SafePage;
        var pageSize = paging.SafePageSize;

        var query = _db.Tenants.AsQueryable();

        if (!request.IncludeSuspended)
        {
            query = query.Where(t => t.SuspendedAt == null);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(t =>
                EF.Functions.Like(t.Name, $"%{s}%") ||
                (t.Nit != null && EF.Functions.Like(t.Nit, $"%{s}%")) ||
                (t.Subdomain != null && EF.Functions.Like(t.Subdomain, $"%{s}%")));
        }

        var total = await query.LongCountAsync(ct);

        var pageItems = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TenantListItemDto(
                t.PublicId,
                t.Name,
                t.Nit,
                t.Subdomain,
                t.PlanType,
                t.IsActive,
                t.SuspendedAt != null,
                _db.TenantBranches.Count(b => b.TenantId == t.Id && !b.IsDeleted),
                t.ActivatedAt,
                t.SuspendedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<TenantListItemDto>(pageItems, page, pageSize, total));
    }
}
