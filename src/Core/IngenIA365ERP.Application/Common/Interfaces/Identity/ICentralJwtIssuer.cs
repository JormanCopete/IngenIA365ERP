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
    CentralAccessTokenResult IssueAccessToken(
        Guid centralUserId,
        string email,
        bool isGlobalMasterAdmin,
        Guid? activeTenantId,
        bool? tenantAdmin,
        bool mfaVerified);

    CentralAccessTokenResult IssueChallengeToken(
        Guid centralUserId,
        string email,
        bool isGlobalMasterAdmin,
        string purpose,
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
