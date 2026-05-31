using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Identity.Configuration;
using IngenIA365ERP.Identity.KeyManagement;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.Identity.CentralIdentity;

/// <summary>
/// Emite JWT RS256 para el flujo de identidad central (T037, research D-03).
/// Claims según spec: <c>sub</c>, <c>email</c>, <c>is_global_master_admin</c>,
/// <c>active_tenant_id</c>, <c>tenant_admin</c>, <c>mfa_verified</c>, <c>purpose</c>,
/// <c>iat</c>, <c>exp</c>, <c>nbf</c>, <c>jti</c>.
///
/// <para>
/// Reutiliza <see cref="IRsaKeyProvider"/> y <see cref="JwtSettings"/> existentes de
/// Fase 0 (mismas claves, mismo issuer/audience). El TTL para los challenge tokens
/// (purposes mfa-verify / mfa-enroll / tenant-select / password-reset) se especifica
/// vía <see cref="IssueChallengeToken"/> (default 5 min).
/// </para>
///
/// <para>
/// <b>Coexistencia con Fase 0</b>: este issuer es independiente del
/// <c>AccessTokenIssuer</c> existente. Mientras el login central no esté wired
/// (Chunk D/E), Fase 0 sigue emitiendo sus JWT por-tenant. Después del cutover,
/// <c>AccessTokenIssuer</c> queda deprecated.
/// </para>
/// </summary>
public class CentralJwtIssuer : ICentralJwtIssuer
{
    private readonly JwtSettings _settings;
    private readonly IRsaKeyProvider _keyProvider;

    public CentralJwtIssuer(IOptions<JwtSettings> settings, IRsaKeyProvider keyProvider)
    {
        _settings = settings.Value;
        _keyProvider = keyProvider;
    }

    /// <summary>Emite un access token operativo (purpose=full) con tenant resuelto.</summary>
    public CentralAccessTokenResult IssueAccessToken(
        Guid centralUserId,
        string email,
        bool isGlobalMasterAdmin,
        Guid? activeTenantId,
        bool? tenantAdmin,
        bool mfaVerified)
    {
        return Issue(
            centralUserId,
            email,
            isGlobalMasterAdmin,
            activeTenantId,
            tenantAdmin,
            mfaVerified,
            purpose: CentralJwtPurposes.Full,
            lifetime: TimeSpan.FromMinutes(_settings.AccessTokenExpirationMinutes));
    }

    /// <summary>Emite un challenge token con purpose acotado (MFA verify / enroll /
    /// tenant-select / password-reset). TTL default 5 min.</summary>
    public CentralAccessTokenResult IssueChallengeToken(
        Guid centralUserId,
        string email,
        bool isGlobalMasterAdmin,
        string purpose,
        TimeSpan? lifetime = null)
    {
        if (!CentralJwtPurposes.IsValid(purpose))
            throw new ArgumentException($"Purpose '{purpose}' no es válido.", nameof(purpose));
        if (purpose == CentralJwtPurposes.Full)
            throw new ArgumentException("Para purpose=full usa IssueAccessToken.", nameof(purpose));

        return Issue(
            centralUserId,
            email,
            isGlobalMasterAdmin,
            activeTenantId: null,
            tenantAdmin: null,
            mfaVerified: false,
            purpose,
            lifetime ?? TimeSpan.FromMinutes(5));
    }

    /// <summary>Genera un refresh token opaco (64 bytes → Base64Url) + su hash SHA-256.</summary>
    public CentralRefreshTokenResult IssueRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token), hash);
        var hashHex = Convert.ToHexString(hash);

        var expiresAt = DateTime.UtcNow.AddHours(_settings.RefreshTokenExpirationDays * 24.0);
        // Spec dice "12h" para central refresh; el setting de Fase 0 puede ser distinto.
        // Si el setting es muy alto, usar el menor: 12h.
        var central12h = DateTime.UtcNow.AddHours(12);
        if (expiresAt > central12h) expiresAt = central12h;

        return new CentralRefreshTokenResult(token, hashHex, expiresAt);
    }

    private CentralAccessTokenResult Issue(
        Guid centralUserId,
        string email,
        bool isGlobalMasterAdmin,
        Guid? activeTenantId,
        bool? tenantAdmin,
        bool mfaVerified,
        string purpose,
        TimeSpan lifetime)
    {
        var signingCredentials = new SigningCredentials(
            new RsaSecurityKey(_keyProvider.GetKey()),
            SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        var expiresAt = now.Add(lifetime);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, centralUserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Email, email),
            new("is_global_master_admin", isGlobalMasterAdmin ? "true" : "false", ClaimValueTypes.Boolean),
            new("mfa_verified", mfaVerified ? "true" : "false", ClaimValueTypes.Boolean),
            new("purpose", purpose),
        };

        if (activeTenantId.HasValue)
            claims.Add(new Claim("active_tenant_id", activeTenantId.Value.ToString()));

        if (tenantAdmin.HasValue)
            claims.Add(new Claim("tenant_admin", tenantAdmin.Value ? "true" : "false", ClaimValueTypes.Boolean));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new CentralAccessTokenResult(jwt, expiresAt, jti, purpose);
    }
}

// CentralJwtPurposes, CentralAccessTokenResult y CentralRefreshTokenResult
// se promovieron a Application (ICentralJwtIssuer.cs) en US1.2 para que los
// handlers de invitaciones/login puedan consumir tipos sin referenciar
// Infrastructure. Quedan accesibles vía
// IngenIA365ERP.Application.Common.Interfaces.Identity.*.
