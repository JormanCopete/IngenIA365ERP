using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Memberships.ListTenantMembers;

public sealed class ListTenantMembersQueryHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb)
    : IRequestHandler<ListTenantMembersQuery, Result<ListTenantMembersResult>>
{
    public async Task<Result<ListTenantMembersResult>> Handle(
        ListTenantMembersQuery request, CancellationToken ct)
    {
        var guard = await Authz.EnsureTenantAdminOrMasterAsync(
            currentUser, adminDb, request.TenantPublicId, ct);
        if (!guard.IsAllowed)
            return Result.Failure<ListTenantMembersResult>(guard.ErrorCode!, guard.Message!);

        var query = adminDb.TenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == request.TenantPublicId);

        if (request.StatusFilter.HasValue)
            query = query.Where(m => m.Status == request.StatusFilter.Value);

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(m => m.IsTenantAdmin)
            .ThenByDescending(m => m.InvitedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new TenantMemberDto(
                m.PublicId,
                m.CentralUserId,
                m.Status,
                m.IsTenantAdmin,
                m.InvitedAt,
                m.ActivatedAt))
            .ToListAsync(ct);

        return Result.Success(new ListTenantMembersResult(items, total, page, pageSize));
    }
}
