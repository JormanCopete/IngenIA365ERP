using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Emite los tokens JWT del flujo de identidad central (Feature 002).
/// Abstracción en Application para que los handlers no dependan del
/// proveedor RSA concreto en Infrastructure.
///
/// <para>Tres tipos de token:</para>
/// <list type="bullet">
///   <item><b>Access</b> (<see cref="IssueAccessToken"/>): purpose=full,
///         lleva <c>active_tenant_id</c> + <c>tenant_admin</c>. Vida: del setting JWT.</item>
///   <item><b>Challenge</b> (<see cref="IssueChallengeToken"/>): purpose acotado
///         (mfa-verify / mfa-enroll / tenant-select / password-reset). Sin tenant.
///         Vida default 5 min.</item>
///   <item><b>Refresh</b> (<see cref="IssueRefreshToken"/>): opaco, 64 bytes random,
///         hash SHA-256. Vida: min(setting, 12h).</item>
/// </list>
/// </summary>
public interface ICentralJwtIssuer
{
    /// <param name="metodoMfa">
    /// Con qué método demostró la persona su identidad en ESTA sesión.
    /// <c>Ninguno</c> significa «no consta»: no tiene segundo factor, o entró con
    /// un código de recuperación.
    ///
    /// <para>
    /// <b>Obligatorio a propósito, sin valor por defecto.</b> Hay ocho sitios que
    /// emiten tokens; con valor por defecto, el que se olvidara de pasarlo sellaría
    /// «no consta» y esa sesión quedaría fuera de toda cooperativa con máscara
    /// restrictiva — sin que fallara ninguna compilación ni ninguna prueba de los
    /// otros siete. Poniéndolo obligatorio, el compilador los enumera de una vez.
    /// </para>
    /// </param>
    CentralAccessTokenResult IssueAccessToken(
        Guid centralUserId,
        string email,
        bool isGlobalMasterAdmin,
        Guid? activeTenantId,
        bool? tenantAdmin,
        bool mfaVerified,
        MetodosMfa metodoMfa);

    /// <param name="metodoMfa">
    /// Sólo tiene sentido en el desafío de <c>tenant-select</c>, que es el único
    /// que se emite DESPUÉS de superar el segundo factor y por tanto el único que
    /// tiene algo que sellar. En los demás propósitos va <c>Ninguno</c>, porque
    /// todavía no se demostró nada.
    /// </param>
    CentralAccessTokenResult IssueChallengeToken(
        Guid centralUserId,
        string email,
        bool isGlobalMasterAdmin,
        string purpose,
        MetodosMfa metodoMfa,
        TimeSpan? lifetime = null);

    CentralRefreshTokenResult IssueRefreshToken();
}

public sealed record CentralAccessTokenResult(
    string Jwt,
    DateTime ExpiresAt,
    string Jti,
    string Purpose);

public sealed record CentralRefreshTokenResult(
    string Token,
    string HashHex,
    DateTime ExpiresAt);

/// <summary>Constantes de claim <c>purpose</c> aceptadas en el sistema.</summary>
public static class CentralJwtPurposes
{
    public const string Full = "full";
    public const string MfaVerify = "mfa-verify";
    public const string MfaEnroll = "mfa-enroll";
    public const string TenantSelect = "tenant-select";
    public const string PasswordReset = "password-reset";

    public static bool IsValid(string purpose) =>
        purpose is Full or MfaVerify or MfaEnroll or TenantSelect or PasswordReset;
}
