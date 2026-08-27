using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Auth.Common;
using IngenIA365ERP.Application.Identity.Auth.Login;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.MfaVerify;

/// <summary>
/// T067 — Verifica el segundo factor con un código: TOTP o código de
/// recuperación de un solo uso.
///
/// <para>
/// Lo que pasa DESPUÉS de superarlo —auto-seleccionar cooperativa, pedir que se
/// elija, o la salida del maestro global— vive en
/// <see cref="IEmisorDeSesionTrasSegundoFactor"/>, porque el ingreso con passkey
/// tiene que hacer exactamente lo mismo.
/// </para>
/// </summary>
public sealed class MfaVerifyCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IEmisorDeSesionTrasSegundoFactor emisorDeSesion,
    IAuditAppendOnlyWriter auditWriter,
    ILoginAttemptCounter contadorDeIntentos,
    IDateTimeService clock,
    ILogger<MfaVerifyCommandHandler> logger)
    : IRequestHandler<MfaVerifyCommand, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> Handle(MfaVerifyCommand request, CancellationToken ct)
    {
        // 1) Validar el purpose del challengeToken. Los handlers lo comprueban
        //    ellos mismos: el atributo que el comentario original mencionaba no
        //    existe en este repositorio.
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<LoginResult>(
                "Identity.Unauthenticated", "Falta el token de challenge.");
        }

        if (!string.Equals(currentUser.Purpose, CentralJwtPurposes.MfaVerify, StringComparison.Ordinal))
        {
            return Result.Failure<LoginResult>(
                "Identity.WrongTokenPurpose",
                $"Este endpoint requiere purpose=mfa-verify, recibido '{currentUser.Purpose}'.");
        }

        var centralUserId = currentUser.CentralUserId.Value;
        var now = clock.UtcNow;

        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
        {
            return Result.Failure<LoginResult>(
                "Identity.Unauthenticated", "Usuario no encontrado.");
        }
        if (!user.TwoFactorEnabled)
        {
            return Result.Failure<LoginResult>(
                "Identity.MfaNotEnabled",
                "El usuario no tiene MFA habilitado; reinicia el login.");
        }

        // 1b) Límite de intentos del SEGUNDO FACTOR.
        //
        // No lo tenía. Un TOTP son seis dígitos y aquí se llega con la contraseña
        // ya acertada, así que sin límite el segundo factor no añade nada frente
        // a quien ya robó la credencial: se prueba el millón.
        //
        // El ámbito es propio y no el del login a propósito. Un login correcto
        // llama a ResetAsync, y a este endpoint sólo se llega tras un login
        // correcto: con el contador compartido, el atacante se regalaría un
        // reset volviendo a iniciar sesión cada pocos intentos.
        var correo = user.Email ?? string.Empty;
        var bloqueo = await contadorDeIntentos.CheckAsync(AmbitoDeIntentos.Mfa, correo, ct);
        if (bloqueo.IsLocked)
        {
            await EmitAuditAsync(user.Id, user.Email,
                AuditEventTypes.CentralUserMfaFailed, request, now, ct);
            return Result.Failure<LoginResult>(
                "Identity.Locked.Soft",
                $"Demasiados intentos con el segundo factor. Reintenta en {bloqueo.RetryAfterSeconds} segundos.");
        }

        // 2) Verificar: TOTP o código de recuperación de un solo uso.
        //    El error hacia el cliente es genérico en ambas ramas; la auditoría
        //    sí distingue.
        int? recoveryCodesRemaining = null;
        if (request.UseRecoveryCode)
        {
            var redeemed = await centralIdentity.RedeemRecoveryCodeAsync(centralUserId, request.Code, ct);
            if (!redeemed)
            {
                await EmitAuditAsync(user.Id, user.Email,
                    AuditEventTypes.CentralUserMfaRecoveryCodeFailed, request, now, ct);
                await contadorDeIntentos.RecordFailureAsync(AmbitoDeIntentos.Mfa, correo, ct);
                return Result.Failure<LoginResult>(
                    "Identity.MfaInvalid", "Código MFA inválido.");
            }

            recoveryCodesRemaining = await centralIdentity.CountRecoveryCodesAsync(centralUserId, ct);
            await EmitAuditAsync(user.Id, user.Email,
                AuditEventTypes.CentralUserMfaRecoveryCodeUsed, request, now, ct);
        }
        else
        {
            var ok = await centralIdentity.VerifyMfaCodeAsync(centralUserId, request.Code, ct);
            if (!ok)
            {
                await EmitAuditAsync(user.Id, user.Email,
                    AuditEventTypes.CentralUserMfaFailed, request, now, ct);
                await contadorDeIntentos.RecordFailureAsync(AmbitoDeIntentos.Mfa, correo, ct);
                return Result.Failure<LoginResult>(
                    "Identity.MfaInvalid", "Código MFA inválido.");
            }

            await EmitAuditAsync(user.Id, user.Email,
                AuditEventTypes.CentralUserMfaSuccess, request, now, ct);
        }

        // Segundo factor superado: el contador vuelve a cero. Va aquí y no al
        // final para que valga igual por TOTP que por código de recuperación.
        await contadorDeIntentos.ResetAsync(AmbitoDeIntentos.Mfa, correo, ct);

        // 3) Emitir la sesión.
        //
        // Este bloque tenía cien líneas y un comentario que decía «duplicación
        // controlada hasta que un tercer caller justifique extraer». El ingreso
        // con passkey es ese tercer llamador: verifica una firma en vez de un
        // código, pero a partir de aquí tiene que pasar exactamente lo mismo.
        // Copiarlo habría significado que arreglar un caso límite en un sitio
        // dejara el otro roto — y el caso límite de aquí es la salida del maestro
        // global, que ya se perdió una vez.
        return await emisorDeSesion.EmitirAsync(user, recoveryCodesRemaining, ct);
    }

    private async Task EmitAuditAsync(
        Guid centralUserId,
        string email,
        string action,
        MfaVerifyCommand request,
        DateTime now,
        CancellationToken ct)
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
                IpAddress: request.IpAddress,
                UserAgent: request.UserAgent,
                Endpoint: "/api/auth/mfa/verify",
                HttpMethod: "POST",
                HttpStatusCode: action == AuditEventTypes.CentralUserMfaSuccess ? 200 : 401,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditAppendOnlyWriter falló para acción {Action}", action);
        }
    }
}
