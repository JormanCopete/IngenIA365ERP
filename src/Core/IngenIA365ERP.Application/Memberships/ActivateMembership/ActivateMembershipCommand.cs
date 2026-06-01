using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Memberships.ActivateMembership;

public sealed record ActivateMembershipCommand(
    Guid TenantPublicId,
    Guid MembershipPublicId
) : IRequest<Result>;
