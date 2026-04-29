namespace IngenIA365ERP.Application.Common.Interfaces;

public interface IAuthenticationService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshAsync(string accessToken, string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public record AuthResult(
    bool Succeeded,
    string? AccessToken = null,
    string? RefreshToken = null,
    DateTime? AccessTokenExpiresAt = null,
    DateTime? RefreshTokenExpiresAt = null,
    string? Error = null
);
