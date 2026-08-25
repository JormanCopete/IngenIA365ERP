using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Identity.Configuration;
using IngenIA365ERP.Identity.KeyManagement;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.Identity.Services;

/// <summary>
/// Emisor de JWT RS256 + refresh tokens opacos (FR-012). El access token
/// tiene TTL <see cref="JwtSettings.AccessTokenExpirationMinutes"/> (30 min
/// por defecto); el refresh token vive en <see cref="JwtSettings.RefreshTokenExpirationDays"/>
/// y se almacena hasheado (SHA-256 hex).
/// </summary>
public class AccessTokenIssuer : IAccessTokenIssuer
{
    private readonly JwtSettings _settings;
    private readonly IRsaKeyProvider _keyProvider;

    public AccessTokenIssuer(IOptions<JwtSettings> settings, IRsaKeyProvider keyProvider)
    {
        _settings = settings.Value;
        _keyProvider = keyProvider;
    }

    public AccessTokenIssueResult IssueAccessToken(AccessTokenClaims input)
    {
        var signingCredentials = new SigningCredentials(
            _keyProvider.GetSecurityKey(),
            SecurityAlgorithms.RsaSha256);

        var jti = string.IsNullOrWhiteSpace(input.Jti) ? Guid.NewGuid().ToString("N") : input.Jti;
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, input.UserPublicId.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new("uid", input.UserId.ToString()),
            new("username", input.Username),
            new("tenant_id", input.TenantId),
            new("tenant_public_id", input.TenantPublicId.ToString()),
            new("perm_ver", input.PermissionsVersion.ToString()),
        };

        if (input.BranchPublicId.HasValue)
        {
            claims.Add(new Claim("branch_id", input.BranchPublicId.Value.ToString()));
        }

        foreach (var role in input.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("roles", role));
        }

        foreach (var perm in input.Permissions)
        {
            claims.Add(new Claim("perm", perm));
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenIssueResult(jwt, expiresAt, jti);
    }

    public RefreshTokenIssueResult IssueRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = HashRefreshToken(token);
        var expiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays);
        return new RefreshTokenIssueResult(token, hash, expiresAt);
    }

    public string HashRefreshToken(string token)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token), hash);
        return Convert.ToHexString(hash);
    }
}
