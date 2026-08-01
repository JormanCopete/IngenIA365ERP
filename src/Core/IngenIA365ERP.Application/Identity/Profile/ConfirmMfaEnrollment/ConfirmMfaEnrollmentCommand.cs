using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.ConfirmMfaEnrollment;

/// <summary>
/// T079b — Confirma el enrollment de MFA con el primer código TOTP que el
/// usuario lee de su app. Si correcto, persiste <c>MfaSecret</c> cifrado +
/// <c>TwoFactorEnabled=true</c> + genera/persiste los recovery codes vía
/// <c>UserManager.GenerateNewTwoFactorRecoveryCodesAsync</c> (almacenados
/// en <c>ADM_CentralUserTokens</c>).
///
/// <para>
/// Si el JWT entrante tenía <c>purpose=mfa-enroll</c>, la respuesta incluye
/// el access token operativo recién emitido (eleva sesión scoped → full sin
/// re-login). Si era <c>purpose=full</c> (enrollment voluntario), solo
/// devuelve los recovery codes.
/// </para>
/// </summary>
public sealed record ConfirmMfaEnrollmentCommand(string Code)
    : IRequest<Result<ConfirmMfaEnrollmentResult>>;

public sealed record ConfirmMfaEnrollmentResult(
    IReadOnlyList<string> RecoveryCodes,
    string? AccessToken = null,
    DateTime? AccessTokenExpiresAt = null,
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresAt = null,
    Guid? ActiveTenantPublicId = null,
    string? ActiveTenantName = null);
