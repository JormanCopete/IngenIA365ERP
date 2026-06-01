using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Sessions.GetMyActiveTenants;

public sealed class GetMyActiveTenantsQueryHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships)
    : IRequestHandler<GetMyActiveTenantsQuery, Result<IReadOnlyList<MyActiveTenantInfo>>>
{
    public async Task<Result<IReadOnlyList<MyActiveTenantInfo>>> Handle(
        GetMyActiveTenantsQuery request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure<IReadOnlyList<MyActiveTenantInfo>>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");

        var centralUserId = currentUser.CentralUserId.Value;
        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
            return Result.Failure<IReadOnlyList<MyActiveTenantInfo>>(
                "Identity.Unauthenticated", "Usuario no encontrado.");

        var active = await memberships.GetActiveMembershipsAsync(centralUserId, ct);
        IReadOnlyList<MyActiveTenantInfo> result = active
            .Select(m => new MyActiveTenantInfo(
                m.TenantId, m.TenantName, m.IsTenantAdmin,
                IsDefault: user.DefaultTenantId == m.TenantId))
            .ToList();
        return Result.Success(result);
    }
}
