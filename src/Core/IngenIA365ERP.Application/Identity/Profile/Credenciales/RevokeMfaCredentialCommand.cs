using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Profile.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.Credenciales;

/// <summary>Retira UNA credencial de quien llama, conservando las demás.</summary>
public sealed record RevokeMfaCredentialCommand(Guid CredencialPublicId) : IRequest<Result>;

public sealed class RevokeMfaCredentialCommandHandler(
    ICurrentCentralUserContext currentUser,
    IMfaDirectory credenciales,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<RevokeMfaCredentialCommandHandler> logger)
    : IRequestHandler<RevokeMfaCredentialCommand, Result>
{
    public async Task<Result> Handle(RevokeMfaCredentialCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure("Identity.Unauthenticated", "Se requiere autenticación válida.");

        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure("Identity.WrongTokenPurpose", "Este endpoint requiere purpose=full.");

        var centralUserId = currentUser.CentralUserId.Value;

        // ¿Es la última que le queda? Si lo es, retirarla equivale a quedarse sin
        // segundo factor, y entonces manda la misma regla que el disable: no puede
        // hacerlo por la puerta de atrás lo que no puede hacer por la de delante.
        var activas = await credenciales.ContarTotpActivasAsync(centralUserId, ct);
        if (activas <= 1)
        {
            var puede = await GuardiaDeSegundoFactor.PuedeQuedarseSinSegundoFactorAsync(
                centralUserId, currentUser.IsGlobalMasterAdmin, memberships, ct);
            if (puede.IsFailure) return puede;
        }

        var revocada = await credenciales.RevocarUnaAsync(
            centralUserId, request.CredencialPublicId, clock.UtcNow, ct);

        if (!revocada)
        {
            // Mismo mensaje exista o no: si dijera «no es tuya», confirmaría que
            // ese identificador existe y es de alguien.
            return Result.Failure(
                "Profile.Mfa.CredentialNotFound", "Esa credencial no existe o no es tuya.");
        }

        // Si era la última, el segundo factor queda apagado de verdad. Sin esto,
        // TwoFactorEnabled seguiría en true sin ninguna credencial detrás: el login
        // pediría un código que nadie puede acertar.
        if (activas <= 1)
        {
            await centralIdentity.DisableMfaAsync(centralUserId, ct);
        }

        await memberships.InvalidateLocalCacheAsync(centralUserId, ct);
        await EmitirAuditoriaAsync(centralUserId, request.CredencialPublicId, clock.UtcNow, ct);

        return Result.Success();
    }

    private async Task EmitirAuditoriaAsync(
        Guid centralUserId, Guid credencialPublicId, DateTime ahora, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: AuditEventTypes.ProfileMfaCredentialRevoked,
                EntityType: nameof(Domain.Entities.Admin.MfaCredential),
                EntityPublicId: credencialPublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: "/api/profile/mfa/credentials/{publicId}",
                HttpMethod: "DELETE",
                HttpStatusCode: 204,
                DurationMs: null,
                OccurredAt: ahora), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}",
                AuditEventTypes.ProfileMfaCredentialRevoked);
        }
    }
}
