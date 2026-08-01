using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Memberships.PromoteToTenantAdmin;

public sealed record PromoteToTenantAdminCommand(
    Guid TenantPublicId,
    Guid MembershipPublicId
) : IRequest<Result>;
