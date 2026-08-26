using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.BeginMfaEnrollment;

public sealed class BeginMfaEnrollmentCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IMfaPendingStore pendingStore,
    IDateTimeService clock,
    ILogger<BeginMfaEnrollmentCommandHandler> logger)
    : IRequestHandler<BeginMfaEnrollmentCommand, Result<BeginMfaEnrollmentResult>>
{
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(10);

    public async Task<Result<BeginMfaEnrollmentResult>> Handle(
        BeginMfaEnrollmentCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<BeginMfaEnrollmentResult>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");
        }

        // Aceptamos purpose=full (enrollment voluntario desde perfil) o
        // purpose=mfa-enroll (forced enrollment tras login MfaEnrollmentRequired).
        if (currentUser.Purpose != CentralJwtPurposes.Full
            && currentUser.Purpose != CentralJwtPurposes.MfaEnroll)
        {
            return Result.Failure<BeginMfaEnrollmentResult>(
                "Identity.WrongTokenPurpose",
                $"Este endpoint requiere purpose=full o mfa-enroll, recibido '{currentUser.Purpose}'.");
        }

        var centralUserId = currentUser.CentralUserId.Value;
        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
        {
            return Result.Failure<BeginMfaEnrollmentResult>(
                "Identity.Unauthenticated", "Usuario no encontrado.");
        }

        var setup = await centralIdentity.BeginMfaEnrollmentAsync(centralUserId, ct);

        await pendingStore.StoreAsync(
            centralUserId,
            new MfaPendingEnrollment(
                SecretBase32: setup.SecretBase32,
                CreatedAt: clock.UtcNow),
            PendingTtl,
            ct);

        logger.LogInformation("MFA enrollment iniciado para CentralUser {UserId}.", centralUserId);

        return Result.Success(new BeginMfaEnrollmentResult(
            SecretBase32: setup.SecretBase32,
            OtpAuthUri: setup.OtpAuthUri,
            ExpiresInSeconds: (int)PendingTtl.TotalSeconds));
    }
}
