using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Memberships.RevokeMembership;

public sealed record RevokeMembershipCommand(
    Guid TenantPublicId,
    Guid MembershipPublicId
) : IRequest<Result>;
