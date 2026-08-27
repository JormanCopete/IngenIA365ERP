using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.BeginMfaEnrollment;

public sealed class BeginMfaEnrollmentCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IMfaDirectory credenciales,
    IMfaPendingStore pendingStore,
    IDateTimeService clock,
    ILogger<BeginMfaEnrollmentCommandHandler> logger)
    : IRequestHandler<BeginMfaEnrollmentCommand, Result<BeginMfaEnrollmentResult>>
{
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Cuántos autenticadores puede tener una persona a la vez. Cinco cubre con
    /// holgura el caso real —teléfono, escritorio, teléfono viejo— sin que el
    /// barrido de verificación se vuelva caro.
    /// </summary>
    private const int MaximoDeAutenticadores = 5;

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

        // Tope de autenticadores por persona.
        //
        // No es estética: el ingreso prueba el código contra TODOS, así que cada
        // credencial es un descifrado más por intento y otro secreto que acepta
        // códigos. El presupuesto de intentos no crece con el número de
        // credenciales, así que con un tope razonable un tiro ciego sigue sin
        // acertar; sin tope, deja de estar claro.
        //
        // Se comprueba aquí y no en la base porque aquí se puede explicar qué pasa
        // y qué hacer, en vez de devolver una violación de índice.
        var yaInscritas = await credenciales.ContarTotpActivasAsync(centralUserId, ct);
        if (yaInscritas >= MaximoDeAutenticadores)
        {
            return Result.Failure<BeginMfaEnrollmentResult>(
                "Profile.Mfa.DemasiadasCredenciales",
                $"Ya tenés {yaInscritas} autenticadores, que es el máximo. " +
                "Retirá alguno que ya no uses antes de agregar otro.");
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
            QrPngDataUri: setup.QrPngDataUri,
            ExpiresInSeconds: (int)PendingTtl.TotalSeconds));
    }
}
