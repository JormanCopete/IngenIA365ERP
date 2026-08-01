using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;

namespace IngenIA365ERP.Application.Memberships.ListTenantMembers;

/// <summary>
/// T098 — Lista paginada de miembros del tenant. Filtra por status si se
/// especifica. La página típica es 20.
/// </summary>
public sealed record ListTenantMembersQuery(
    Guid TenantPublicId,
    MembershipStatus? StatusFilter = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<ListTenantMembersResult>>;

public sealed record ListTenantMembersResult(
    IReadOnlyList<TenantMemberDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record TenantMemberDto(
    Guid MembershipPublicId,
    Guid CentralUserId,
    MembershipStatus Status,
    bool IsTenantAdmin,
    DateTime InvitedAt,
    DateTime? ActivatedAt,
    string? Email = null);
