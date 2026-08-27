using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.BeginMfaEnrollment;

/// <summary>
/// T079a — Inicia el enrollment de MFA. NO recibe input: usa el
/// <c>central_user_id</c> del JWT autenticado. Devuelve el secret en
/// Base32 + la URI <c>otpauth://</c>; el usuario configura su app TOTP y
/// confirma con <c>ConfirmMfaEnrollmentCommand</c>.
///
/// <para>
/// Los códigos de recuperación llegan en el <b>confirm</b>, no aquí. Antes
/// también salían por este paso, y eran otros: se generaban con un formato
/// distinto, se guardaban en Redis y se descartaban al confirmar. Quien los
/// anotara y cerrara la pantalla se quedaba con diez códigos inservibles.
/// </para>
///
/// <para>
/// El secret pendiente se persiste en Redis (TTL 10 min) — si el usuario
/// no confirma a tiempo, debe reiniciar el flow. NO se guarda en BD hasta
/// la confirmación exitosa.
/// </para>
/// </summary>
public sealed record BeginMfaEnrollmentCommand() : IRequest<Result<BeginMfaEnrollmentResult>>;

public sealed record BeginMfaEnrollmentResult(
    string SecretBase32,
    string OtpAuthUri,
    string QrPngDataUri,
    int ExpiresInSeconds);
