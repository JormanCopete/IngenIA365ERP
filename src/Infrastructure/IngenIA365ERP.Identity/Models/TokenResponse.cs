namespace IngenIA365ERP.Identity.Models;

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    DateTime RefreshTokenExpiry
);
