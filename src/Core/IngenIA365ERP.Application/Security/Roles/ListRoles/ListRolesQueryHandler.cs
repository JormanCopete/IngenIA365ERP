using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Roles.ListRoles;

public sealed class ListRolesQueryHandler
    : IRequestHandler<ListRolesQuery, Result<PagedResult<RoleListItemDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ListRolesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<RoleListItemDto>>> Handle(
        ListRolesQuery request, CancellationToken ct)
    {
        var paging = request.Paging ?? new PageRequest();
        var page = paging.SafePage;
        var pageSize = paging.SafePageSize;
        var tenantId = int.TryParse(_currentUser.TenantId, out var tid) ? tid : (int?)null;

        // Incluye roles del tenant + roles SaaS-globales (TenantId == null).
        var query = _db.Roles
            .Where(r => r.TenantId == tenantId || r.TenantId == null);

        if (!request.IncludeBuiltIn)
        {
            query = query.Where(r => !r.IsBuiltIn);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(r =>
                EF.Functions.Like(r.Code, $"%{s}%") ||
                EF.Functions.Like(r.Name, $"%{s}%"));
        }

        var total = await query.LongCountAsync(ct);

        var pageItems = await query
            .OrderBy(r => r.IsBuiltIn ? 0 : 1).ThenBy(r => r.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RoleListItemDto(
                r.PublicId,
                r.Code,
                r.Name,
                r.Description,
                r.IsBuiltIn,
                r.IsAssignable,
                r.IsActive,
                _db.RolePermissions.Count(rp => rp.RoleId == r.Id && !rp.IsDeleted),
                r.Users.Count))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<RoleListItemDto>(pageItems, page, pageSize, total));
    }
}
