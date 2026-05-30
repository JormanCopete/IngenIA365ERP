using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Auth;

/// <summary>
/// T045 — Rotación de refresh: reutilizar un refresh ya rotado invalida la
/// familia completa y se traduce a <c>Auth.RefreshTokenReuseDetected</c>.
/// </summary>
public class RefreshTokenRotationTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    public RefreshTokenRotationTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Reusing_rotated_refresh_invalidates_family()
    {
        await SeedUserAsync("usr_rotation", "Test#2026!Rotate");
        var client = _fx.CreateClient();

        // login → mfa/verify → tokens
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSubdomainOrNit = "demo",
            username = "usr_rotation",
            password = "Test#2026!Rotate"
        });
        var loginBody = await login.Content.ReadFromJsonAsync<LoginChallengeDto>();
        var verify = await client.PostAsJsonAsync("/api/auth/mfa/verify", new
        {
            mfaChallengeToken = loginBody!.MfaChallengeToken,
            totpCode = "000000",
            useBackupCode = false
        });
        var tokens = await verify.Content.ReadFromJsonAsync<TokensDto>();

        // refresh original → rota correctamente
        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens!.RefreshToken });
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedTokens = await rotated.Content.ReadFromJsonAsync<TokensDto>();

        // segundo intento con el ORIGINAL ya rotado → detección de reuso
        var reused = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        var bodyText = await reused.Content.ReadAsStringAsync();
        reused.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.UnprocessableContent);
        bodyText.Should().Contain("Auth.RefreshTokenReuseDetected");

        // El refresh rotado tampoco debe servir luego de la invalidación de familia.
        var afterRevoke = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotatedTokens!.RefreshToken });
        afterRevoke.IsSuccessStatusCode.Should().BeFalse();

        // Persistencia: ambos tokens están con RevocationReason != null.
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var family = await db.RefreshTokens
            .Where(t => t.User != null && t.User.Username == "usr_rotation")
            .ToListAsync(default);
        family.Should().NotBeEmpty();
        family.All(t => t.RevokedAt != null).Should().BeTrue();
    }

    private async Task SeedUserAsync(string username, string plainPassword)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        if (db.Users.Any(u => u.Username == username)) return;
        db.Users.Add(new User
        {
            Username = username,
            Email = $"{username}@demo.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 11),
            IsActive = true,
            IsMfaEnabled = false,
            LastPasswordChangeAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(default);
    }

    private sealed record LoginChallengeDto(string MfaChallengeToken, bool MustChangePassword);
    private sealed record TokensDto(string AccessToken, string RefreshToken,
        DateTime AccessTokenExpiresAt, DateTime RefreshTokenExpiresAt);
}
