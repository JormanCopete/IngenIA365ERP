using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.RegenerateRecoveryCodes;

public sealed class RegenerateRecoveryCodesCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<RegenerateRecoveryCodesCommandHandler> logger)
    : IRequestHandler<RegenerateRecoveryCodesCommand, Result<RegenerateRecoveryCodesResult>>
{
    public async Task<Result<RegenerateRecoveryCodesResult>> Handle(
        RegenerateRecoveryCodesCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure<RegenerateRecoveryCodesResult>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure<RegenerateRecoveryCodesResult>(
                "Identity.WrongTokenPurpose", "Este endpoint requiere purpose=full.");

        var centralUserId = currentUser.CentralUserId.Value;

        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
            return Result.Failure<RegenerateRecoveryCodesResult>(
                "Identity.Unauthenticated", "Usuario no encontrado.");
        if (!user.TwoFactorEnabled)
            return Result.Failure<RegenerateRecoveryCodesResult>(
                "Profile.Mfa.NotEnabled",
                "MFA no está activo: no hay códigos de recuperación que regenerar.");

        // Confirmación de identidad (FR-111): contraseña actual XOR TOTP vigente.
        var confirmed = !string.IsNullOrWhiteSpace(request.CurrentPassword)
            ? await centralIdentity.ValidatePasswordAsync(centralUserId, request.CurrentPassword!, ct)
            : await centralIdentity.VerifyMfaCodeAsync(centralUserId, request.TotpCode!, ct);
        if (!confirmed)
            return Result.Failure<RegenerateRecoveryCodesResult>(
                "Identity.InvalidCredentials", "La confirmación de identidad no es válida.");

        var codes = await centralIdentity.RegenerateRecoveryCodesAsync(centralUserId, ct);
        if (codes.Count == 0)
            return Result.Failure<RegenerateRecoveryCodesResult>(
                "Profile.Mfa.RegenerateFailed", "No se pudieron regenerar los códigos.");

        await EmitAuditAsync(centralUserId, currentUser.Email ?? string.Empty, clock.UtcNow, ct);

        return Result.Success(new RegenerateRecoveryCodesResult(codes));
    }

    private async Task EmitAuditAsync(
        Guid centralUserId, string email, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: AuditEventTypes.ProfileRecoveryCodesRegenerated,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: "/api/profile/mfa/recovery-codes/regenerate",
                HttpMethod: "POST",
                HttpStatusCode: 200,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}",
                AuditEventTypes.ProfileRecoveryCodesRegenerated);
        }
    }
}
