using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Identity.Models;

namespace IngenIA365ERP.Identity.Services;

/// <summary>
/// Adapter that bridges the Application layer's ITokenService to the Identity layer's IJwtService.
/// Maintains backward compatibility with existing code that depends on ITokenService.
/// </summary>
public class JwtTokenService(IJwtService jwtService) : ITokenService
{
    public async Task<TokenResult> GenerateTokenAsync(string userId, string username,
        IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            Id = int.TryParse(userId, out var id) ? id : 0,
            UserName = username,
            Email = username,
            FullName = username,
            TenantId = "default"
        };

        var response = await jwtService.GenerateTokensAsync(user, roles.ToList(), []);
        return new TokenResult(response.AccessToken, response.RefreshToken,
            response.AccessTokenExpiry, response.RefreshTokenExpiry);
    }

    public async Task<TokenResult> RefreshTokenAsync(string accessToken, string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var response = await jwtService.RefreshTokenAsync(accessToken, refreshToken);
        return new TokenResult(response.AccessToken, response.RefreshToken,
            response.AccessTokenExpiry, response.RefreshTokenExpiry);
    }

    public Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
