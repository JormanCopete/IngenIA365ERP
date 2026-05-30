using IngenIA365ERP.Application.Admin.Branches.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Branches.ListBranches;

/// <summary>
/// Lista de sucursales. Si <see cref="TenantPublicId"/> está presente,
/// filtra al tenant indicado; si no, devuelve todas (uso SaaS-global).
/// </summary>
public sealed record ListBranchesQuery(
    Guid? TenantPublicId,
    string? Search,
    bool IncludeInactive,
    PageRequest Paging) : IRequest<Result<PagedResult<BranchDto>>>;

public sealed class ListBranchesQueryHandler
    : IRequestHandler<ListBranchesQuery, Result<PagedResult<BranchDto>>>
{
    private readonly IAdminDbContext _db;

    public ListBranchesQueryHandler(IAdminDbContext db) => _db = db;

    public async Task<Result<PagedResult<BranchDto>>> Handle(
        ListBranchesQuery request, CancellationToken ct)
    {
        var paging = request.Paging ?? new PageRequest();
        var page = paging.SafePage;
        var pageSize = paging.SafePageSize;

        var query = _db.TenantBranches.AsQueryable();

        if (request.TenantPublicId is { } tpid)
        {
            var tenantInternalId = await _db.Tenants
                .Where(t => t.PublicId == tpid)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync(ct);
            if (tenantInternalId is null)
            {
                return Result.Failure<PagedResult<BranchDto>>(
                    "Generic.NotFound", "Cooperativa no encontrada.");
            }
            query = query.Where(b => b.TenantId == tenantInternalId);
        }

        if (!request.IncludeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(b =>
                EF.Functions.Like(b.Code, $"%{s}%") ||
                EF.Functions.Like(b.Name, $"%{s}%"));
        }

        var total = await query.LongCountAsync(ct);

        var pageItems = await query
            .OrderByDescending(b => b.IsHeadquarters).ThenBy(b => b.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_db.Tenants,
                b => b.TenantId,
                t => t.Id,
                (b, t) => new BranchDto(
                    b.PublicId,
                    t.PublicId,
                    b.Code,
                    b.Name,
                    b.Address,
                    null, // City — no en el schema actual
                    null, // Department — no en el schema actual
                    b.Phone,
                    b.IsActive,
                    b.IsHeadquarters))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<BranchDto>(pageItems, page, pageSize, total));
    }
}
