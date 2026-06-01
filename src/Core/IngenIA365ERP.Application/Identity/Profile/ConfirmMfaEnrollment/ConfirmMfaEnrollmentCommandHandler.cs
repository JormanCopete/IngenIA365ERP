using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.ConfirmMfaEnrollment;

/// <summary>
/// T079b — Confirma el enrollment de MFA. Si el JWT entrante era
/// <c>purpose=mfa-enroll</c>, eleva la sesión emitiendo el access+refresh
/// operativos (con tenant resuelto si el usuario tiene 1 membresía activa).
/// </summary>
public sealed class ConfirmMfaEnrollmentCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IMfaPendingStore pendingStore,
    ITenantMembershipReader memberships,
    ICentralJwtIssuer jwtIssuer,
    ICentralRefreshTokenStore refreshStore,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<ConfirmMfaEnrollmentCommandHandler> logger)
    : IRequestHandler<ConfirmMfaEnrollmentCommand, Result<ConfirmMfaEnrollmentResult>>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);

    public async Task<Result<ConfirmMfaEnrollmentResult>> Handle(
        ConfirmMfaEnrollmentCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<ConfirmMfaEnrollmentResult>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");
        }
        if (currentUser.Purpose != CentralJwtPurposes.Full
            && currentUser.Purpose != CentralJwtPurposes.MfaEnroll)
        {
            return Result.Failure<ConfirmMfaEnrollmentResult>(
                "Identity.WrongTokenPurpose",
                $"Este endpoint requiere purpose=full o mfa-enroll.");
        }

        var centralUserId = currentUser.CentralUserId.Value;

        var pending = await pendingStore.GetAsync(centralUserId, ct);
        if (pending is null)
        {
            return Result.Failure<ConfirmMfaEnrollmentResult>(
                "Profile.Mfa.NoPendingEnrollment",
                "No hay enrollment pendiente. Reinicia el flujo.");
        }

        var confirm = await centralIdentity.ConfirmMfaSetupAsync(
            centralUserId, pending.SecretBase32, request.Code, ct);
        if (!confirm.Succeeded)
        {
            var code = confirm.ErrorCodes.FirstOrDefault() ?? "Profile.Mfa.InvalidCode";
            return Result.Failure<ConfirmMfaEnrollmentResult>(
                code, "El código TOTP no es válido.");
        }

        await pendingStore.ClearAsync(centralUserId, ct);
        await memberships.InvalidateLocalCacheAsync(centralUserId, ct);

        var now = clock.UtcNow;
        await EmitAuditAsync(centralUserId, currentUser.Email ?? string.Empty,
            AuditEventTypes.ProfileMfaEnrolled, now, ct);

        var recoveryCodes = confirm.RecoveryCodes ?? Array.Empty<string>();

        // Si el JWT era mfa-enroll, elevamos la sesión emitiendo tokens
        // operativos para que el usuario no tenga que re-loguearse.
        if (currentUser.Purpose != CentralJwtPurposes.MfaEnroll)
        {
            return Result.Success(new ConfirmMfaEnrollmentResult(RecoveryCodes: recoveryCodes));
        }

        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
        {
            // Edge: usuario desapareció justo después de confirmar — devolvemos
            // OK con los recovery codes pero sin tokens; el usuario debe re-loguear.
            logger.LogWarning("CentralUser {Id} desapareció tras confirm MFA.", centralUserId);
            return Result.Success(new ConfirmMfaEnrollmentResult(RecoveryCodes: recoveryCodes));
        }

        var active = await memberships.GetActiveMembershipsAsync(centralUserId, ct);
        if (active.Count != 1)
        {
            // Para multi-tenant, devolvemos sin tokens — el cliente debe pasar por
            // tenant-select. (No se devuelve challenge token aquí; el cliente
            // re-llamará /api/auth/login si necesita esa pantalla.)
            return Result.Success(new ConfirmMfaEnrollmentResult(RecoveryCodes: recoveryCodes));
        }

        var m = active[0];
        var access = jwtIssuer.IssueAccessToken(
            centralUserId: centralUserId,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            activeTenantId: m.TenantId,
            tenantAdmin: m.IsTenantAdmin,
            mfaVerified: true);
        var refresh = jwtIssuer.IssueRefreshToken();

        await refreshStore.StoreAsync(
            refresh.HashHex,
            new CentralRefreshSession(
                CentralUserId: centralUserId,
                ActiveTenantPublicId: m.TenantId,
                FamilyId: Guid.NewGuid(),
                IssuedAt: now,
                IpAddress: null,
                UserAgent: null,
                ReplacedByTokenHashHex: null),
            RefreshTokenTtl, ct);

        return Result.Success(new ConfirmMfaEnrollmentResult(
            RecoveryCodes: recoveryCodes,
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: refresh.Token,
            RefreshTokenExpiresAt: refresh.ExpiresAt,
            ActiveTenantPublicId: m.TenantId,
            ActiveTenantName: m.TenantName));
    }

    private async Task EmitAuditAsync(
        Guid centralUserId, string email, string action, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: action,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: "/api/profile/mfa/confirm",
                HttpMethod: "POST",
                HttpStatusCode: 200,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}", action);
        }
    }
}
