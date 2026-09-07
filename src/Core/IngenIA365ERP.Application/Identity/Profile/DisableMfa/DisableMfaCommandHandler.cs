using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Profile.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.DisableMfa;

public sealed class DisableMfaCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<DisableMfaCommandHandler> logger)
    : IRequestHandler<DisableMfaCommand, Result>
{
    public async Task<Result> Handle(DisableMfaCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure("Identity.WrongTokenPurpose",
                "Este endpoint requiere purpose=full.");

        var centralUserId = currentUser.CentralUserId.Value;

        // 1) Re-validar password actual.
        var ok = await centralIdentity.ValidatePasswordAsync(
            centralUserId, request.CurrentPassword, ct);
        if (!ok)
        {
            return Result.Failure(
                "Identity.InvalidCredentials", "La contraseña actual no es correcta.");
        }

        // 2) ¿Puede quedarse sin segundo factor?
        //
        // La regla vive en GuardiaDeSegundoFactor porque ahora hay dos caminos que
        // llevan aquí: desactivar el MFA entero y revocar la última credencial.
        // Duplicarla habría dejado abierta la puerta de al lado, que además es la
        // que la gente usa a diario.
        var puede = await GuardiaDeSegundoFactor.PuedeQuedarseSinSegundoFactorAsync(
            centralUserId, currentUser.IsGlobalMasterAdmin, memberships, ct);
        if (puede.IsFailure) return puede;

        // 3) Desactivar.
        await centralIdentity.DisableMfaAsync(centralUserId, ct);
        await memberships.InvalidateLocalCacheAsync(centralUserId, ct);

        await EmitAuditAsync(
            centralUserId,
            currentUser.Email ?? string.Empty,
            AuditEventTypes.ProfileMfaDisabled,
            clock.UtcNow, ct);

        return Result.Success();
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
                Endpoint: "/api/profile/mfa/disable",
                HttpMethod: "POST",
                HttpStatusCode: 204,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}", action);
        }
    }
}
