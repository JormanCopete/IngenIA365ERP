using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Sessions.SwitchTenant;

public sealed class SwitchTenantCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    ICentralJwtIssuer jwtIssuer,
    ICentralRefreshTokenStore refreshStore,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<SwitchTenantCommandHandler> logger)
    : IRequestHandler<SwitchTenantCommand, Result<SwitchTenantResult>>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);

    public async Task<Result<SwitchTenantResult>> Handle(SwitchTenantCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is null)
            return Result.Failure<SwitchTenantResult>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure<SwitchTenantResult>(
                "Identity.WrongTokenPurpose", "Este endpoint requiere purpose=full.");

        var centralUserId = currentUser.CentralUserId.Value;
        var fromTenantId = currentUser.ActiveTenantPublicId;

        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
            return Result.Failure<SwitchTenantResult>(
                "Identity.Unauthenticated", "Usuario no encontrado.");

        var active = await memberships.GetActiveMembershipsAsync(centralUserId, ct);
        var target = active.FirstOrDefault(m => m.TenantId == request.TenantPublicId);
        if (target is null)
            return Result.Failure<SwitchTenantResult>(
                "Membership.NotActive",
                "No tienes membresía activa con la empresa indicada.");

        // Enforce MFA policy del tenant destino.
        if (target.IsMfaRequiredByTenant && !user.TwoFactorEnabled)
        {
            return Result.Failure<SwitchTenantResult>(
                "Tenant.MfaPolicyEnforced",
                $"La empresa '{target.TenantName}' requiere MFA. Configúralo desde tu perfil para entrar.");
        }

        var now = clock.UtcNow;
        var access = jwtIssuer.IssueAccessToken(
            centralUserId, user.Email, user.IsGlobalMasterAdmin,
            target.TenantId, target.IsTenantAdmin, user.TwoFactorEnabled);
        var refresh = jwtIssuer.IssueRefreshToken();
        var familyId = Guid.NewGuid();
        await refreshStore.StoreAsync(refresh.HashHex, new CentralRefreshSession(
            centralUserId, target.TenantId, familyId, now, null, null, null,
            user.SecurityStamp),
            RefreshTokenTtl, ct);

        await EmitAuditAsync(centralUserId, user.Email, fromTenantId, target.TenantId, now, ct);

        return Result.Success(new SwitchTenantResult(
            access.Jwt, access.ExpiresAt,
            refresh.Token, refresh.ExpiresAt,
            (int)(access.ExpiresAt - now).TotalSeconds,
            new SwitchedTenantInfo(target.TenantId, target.TenantName, target.IsTenantAdmin)));
    }

    private async Task EmitAuditAsync(
        Guid centralUserId, string email, Guid? fromTenantId, Guid toTenantId,
        DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: toTenantId.ToString("N"),
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: AuditEventTypes.SessionTenantSwitched,
                EntityType: nameof(Domain.Entities.Admin.TenantMembership),
                EntityPublicId: toTenantId.ToString("N"),
                Module: "Identity",
                OldValuesJson: fromTenantId?.ToString("N"),
                NewValuesJson: toTenantId.ToString("N"),
                ChangedFields: new[] { "active_tenant_id" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/sessions/switch-tenant",
                HttpMethod: "POST", HttpStatusCode: 200, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para Session.TenantSwitched");
        }
    }
}
