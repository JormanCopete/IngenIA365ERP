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
    Common.IElevadorDeSesionTrasInscripcion elevador,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<ConfirmMfaEnrollmentCommandHandler> logger)
    : IRequestHandler<ConfirmMfaEnrollmentCommand, Result<ConfirmMfaEnrollmentResult>>
{
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
            centralUserId, pending.SecretBase32, request.Code, request.Label, ct);
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

        // La emisión vive aparte desde que hay dos formas de inscribirse: con
        // código y con passkey. Copiarla habría dejado a quien sólo puede usar una
        // llave sin manera de cumplir la exigencia de su cooperativa.
        var elevada = await elevador.ElevarAsync(
            centralUserId, Domain.Entities.Admin.MetodosMfa.Totp, now, ct);
        if (elevada is null)
        {
            return Result.Success(new ConfirmMfaEnrollmentResult(RecoveryCodes: recoveryCodes));
        }

        return Result.Success(new ConfirmMfaEnrollmentResult(
            RecoveryCodes: recoveryCodes,
            AccessToken: elevada.AccessToken,
            AccessTokenExpiresAt: elevada.AccessTokenExpiresAt,
            RefreshToken: elevada.RefreshToken,
            RefreshTokenExpiresAt: elevada.RefreshTokenExpiresAt,
            ActiveTenantPublicId: elevada.ActiveTenantPublicId,
            ActiveTenantName: elevada.ActiveTenantName));
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
