using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Memberships.DemoteFromTenantAdmin;

public sealed record DemoteFromTenantAdminCommand(
    Guid TenantPublicId,
    Guid MembershipPublicId
) : IRequest<Result>;
