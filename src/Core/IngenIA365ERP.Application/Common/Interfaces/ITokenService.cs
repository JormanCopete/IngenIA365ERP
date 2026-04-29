namespace IngenIA365ERP.Application.Common.Interfaces;

public interface ITokenService
{
    Task<TokenResult> GenerateTokenAsync(string userId, string username, IEnumerable<string> roles,
        CancellationToken cancellationToken = default);
    Task<TokenResult> RefreshTokenAsync(string accessToken, string refreshToken,
        CancellationToken cancellationToken = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public record TokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt
);
