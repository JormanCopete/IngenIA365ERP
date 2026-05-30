using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.ListUsers;

public sealed class ListUsersQueryHandler
    : IRequestHandler<ListUsersQuery, Result<PagedResult<UserListItemDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;

    public ListUsersQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<PagedResult<UserListItemDto>>> Handle(
        ListUsersQuery request, CancellationToken ct)
    {
        var paging = request.Paging ?? new PageRequest();
        var page = paging.SafePage;
        var pageSize = paging.SafePageSize;
        var now = _clock.UtcNow;

        var query = request.IncludeDisabled
            ? _db.Users.IgnoreQueryFilters()
            : _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(u =>
                EF.Functions.Like(u.Username, $"%{s}%") ||
                (u.Email != null && EF.Functions.Like(u.Email, $"%{s}%")));
        }

        if (!string.IsNullOrWhiteSpace(request.RoleCode))
        {
            var code = request.RoleCode;
            query = query.Where(u => u.Roles.Any(r => r.Code == code));
        }

        var total = await query.LongCountAsync(ct);

        // Proyección a DTO. EF traduce Roles.Select por la junction UserRole.
        var pageItems = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItemDto(
                u.PublicId,
                u.Username,
                u.Email,
                u.IsActive && !u.IsDeleted,
                u.IsMfaEnabled,
                u.LockoutEndAt != null && u.LockoutEndAt > now,
                u.LastLoginAt,
                u.Roles.Select(r => r.Code).ToList()))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<UserListItemDto>(pageItems, page, pageSize, total));
    }
}
