using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Auth.Me;

public sealed class GetMeQueryHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships)
    : IRequestHandler<GetMeQuery, Result<MeResult>>
{
    public async Task<Result<MeResult>> Handle(GetMeQuery request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is null)
        {
            return Result.Failure<MeResult>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");
        }

        var centralUserId = currentUser.CentralUserId.Value;
        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
        {
            return Result.Failure<MeResult>(
                "Identity.Unauthenticated", "Usuario no encontrado.");
        }

        var active = await memberships.GetActiveMembershipsAsync(centralUserId, ct);

        ActiveTenantSummary? activeTenant = null;
        if (currentUser.ActiveTenantPublicId.HasValue)
        {
            var current = active.FirstOrDefault(m => m.TenantId == currentUser.ActiveTenantPublicId.Value);
            if (current is not null)
            {
                activeTenant = new ActiveTenantSummary(
                    TenantPublicId: current.TenantId,
                    TenantName: current.TenantName,
                    IsTenantAdmin: current.IsTenantAdmin,
                    IsMfaRequiredByPolicy: current.IsMfaRequiredByTenant);
            }
        }

        var available = active
            .Select(m => new AvailableTenantSummary(m.TenantId, m.TenantName, m.IsTenantAdmin))
            .ToList();

        return Result.Success(new MeResult(
            CentralUserId: user.Id,
            Email: user.Email,
            IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            MfaEnabled: user.TwoFactorEnabled,
            ActiveTenant: activeTenant,
            AvailableTenants: available,
            DefaultTenantPublicId: user.DefaultTenantId));
    }
}
