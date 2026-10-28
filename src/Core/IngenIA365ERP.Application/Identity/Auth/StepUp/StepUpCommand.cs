using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Auth.StepUp;

/// <summary>
/// Feature 017 (US7, T076, FR-037): antes de cambiar el correo, la persona con sesión vuelve a demostrar
/// quién es —clave más segundo factor, con los mismos verificadores y contadores del ingreso— y recibe un token
/// <c>step-up</c> de cinco minutos que sólo sirve para pedir el cambio. Con segundo factor, el código es el TOTP
/// o un código de respaldo (quien sólo tiene passkeys usa uno de respaldo: la ceremonia WebAuthn vive en el
/// desafío del ingreso). Sin segundo factor inscrito —su cooperativa no lo exige— basta la clave.
/// </summary>
public sealed record StepUpCommand(string? Password, string? Code, bool UseRecoveryCode = false)
    : IRequest<Result<StepUpResult>>;

public sealed record StepUpResult(string StepUpToken, int ExpiresInSeconds);

public sealed class StepUpCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ILoginAttemptCounter intentos,
    ICentralJwtIssuer jwtIssuer)
    : IRequestHandler<StepUpCommand, Result<StepUpResult>>
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);

    public async Task<Result<StepUpResult>> Handle(StepUpCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is not { } id || currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure<StepUpResult>("Identity.Unauthenticated", "Inicie sesión de nuevo.");

        var persona = await centralIdentity.FindByIdAsync(id, ct);
        if (persona is null)
            return Result.Failure<StepUpResult>("Identity.Unauthenticated", "Inicie sesión de nuevo.");

        var clave = ClaveDeIntentos.DeCuenta(id);
        if ((await intentos.CheckAsync(AmbitoDeIntentos.Password, clave, ct)).IsLocked)
            return Result.Failure<StepUpResult>("Identity.Locked.Soft", "Demasiados intentos. Espere unos minutos.");
        if (string.IsNullOrEmpty(request.Password) || !await centralIdentity.ValidatePasswordAsync(id, request.Password, ct))
        {
            await intentos.RecordFailureAsync(AmbitoDeIntentos.Password, clave, ct);
            return Result.Failure<StepUpResult>("Identity.InvalidCredentials", "La contraseña no es correcta.");
        }

        if (persona.TwoFactorEnabled)
        {
            var correo = persona.NormalizedEmail;
            if ((await intentos.CheckAsync(AmbitoDeIntentos.Mfa, correo, ct)).IsLocked)
                return Result.Failure<StepUpResult>("Identity.Locked.Soft", "Demasiados intentos. Espere unos minutos.");
            var codigo = request.Code?.Trim() ?? string.Empty;
            var valido = codigo.Length > 0 && (request.UseRecoveryCode
                ? await centralIdentity.RedeemRecoveryCodeAsync(id, codigo, ct)
                : await centralIdentity.VerifyMfaCodeAsync(id, codigo, ct));
            if (!valido)
            {
                await intentos.RecordFailureAsync(AmbitoDeIntentos.Mfa, correo, ct);
                return Result.Failure<StepUpResult>("Identity.MfaInvalid", "El código del segundo factor no es correcto.");
            }
            await intentos.ResetAsync(AmbitoDeIntentos.Mfa, correo, ct);
        }
        await intentos.ResetAsync(AmbitoDeIntentos.Password, clave, ct);

        var token = jwtIssuer.IssueChallengeToken(
            centralUserId: id,
            email: persona.Email,
            isGlobalMasterAdmin: persona.IsGlobalMasterAdmin,
            purpose: CentralJwtPurposes.StepUp,
            metodoMfa: currentUser.MetodoMfa,
            lifetime: Vigencia);
        return Result.Success(new StepUpResult(token.Jwt, (int)Vigencia.TotalSeconds));
    }
}
