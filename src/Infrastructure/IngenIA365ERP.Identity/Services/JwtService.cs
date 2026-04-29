using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using IngenIA365ERP.Identity.Configuration;
using IngenIA365ERP.Identity.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.Identity.Services;

public interface IJwtService
{
    Task<TokenResponse> GenerateTokensAsync(ApplicationUser user, IList<string> roles, IList<string> permissions);
    Task<TokenResponse> RefreshTokenAsync(string accessToken, string refreshToken);
    Task RevokeRefreshTokenAsync(int userId);
    ClaimsPrincipal? ValidateExpiredToken(string token);
}

public class JwtService : IJwtService
{
    private readonly JwtSettings _settings;
    private readonly RSA _rsa;

    public JwtService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
        _rsa = RSA.Create();

        if (File.Exists(_settings.PrivateKeyPath))
        {
            var keyPem = File.ReadAllText(_settings.PrivateKeyPath);
            _rsa.ImportFromPem(keyPem);
        }
        else
        {
            _rsa = RSA.Create(2048);
        }
    }

    public Task<TokenResponse> GenerateTokensAsync(ApplicationUser user, IList<string> roles, IList<string> permissions)
    {
        var signingCredentials = new SigningCredentials(
            new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("publicId", user.PublicId.ToString()),
            new("tenantId", user.TenantId),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("fullName", user.FullName),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var permission in permissions)
            claims.Add(new Claim("permission", permission));

        var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: accessTokenExpiry,
            signingCredentials: signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();

        return Task.FromResult(new TokenResponse(
            accessToken, refreshToken, accessTokenExpiry, refreshTokenExpiry));
    }

    public Task<TokenResponse> RefreshTokenAsync(string accessToken, string refreshToken)
    {
        var principal = ValidateExpiredToken(accessToken)
            ?? throw new SecurityTokenException("Invalid access token.");

        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new SecurityTokenException("Missing subject claim.");
        var publicId = principal.FindFirstValue("publicId") ?? string.Empty;
        var tenantId = principal.FindFirstValue("tenantId") ?? string.Empty;
        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty;
        var fullName = principal.FindFirstValue("fullName") ?? string.Empty;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var permissions = principal.FindAll("permission").Select(c => c.Value).ToList();

        var user = new ApplicationUser
        {
            Id = int.Parse(userId),
            PublicId = Guid.TryParse(publicId, out var pid) ? pid : Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            FullName = fullName
        };

        return GenerateTokensAsync(user, roles, permissions);
    }

    public Task RevokeRefreshTokenAsync(int userId)
    {
        // Revocation is handled by clearing RefreshToken in the database via AuthenticationService
        return Task.CompletedTask;
    }

    public ClaimsPrincipal? ValidateExpiredToken(string token)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new RsaSecurityKey(_rsa),
            ValidateLifetime = false
        };

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, validationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.RsaSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
