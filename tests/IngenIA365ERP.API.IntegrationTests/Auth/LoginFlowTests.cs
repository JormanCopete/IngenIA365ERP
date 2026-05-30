using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Auth;

/// <summary>
/// T044 — Camino feliz del flujo de autenticación: login → mfa/verify →
/// refresh → logout. Usa <see cref="ApiTestFixture"/> con SQL Server,
/// MongoDB y Redis efímeros (Testcontainers). Requiere Docker en el host.
/// </summary>
public class LoginFlowTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;

    public LoginFlowTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Full_login_flow_returns_access_and_refresh_tokens()
    {
        // === arrange: usuario seed con BCrypt hash conocido ===
        await SeedUserAsync("usr_demo", "Test#2026!Pass", mfaEnabled: false);

        var client = _fx.CreateClient();

        // === act 1: login ===
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSubdomainOrNit = "demo",
            username = "usr_demo",
            password = "Test#2026!Pass"
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginChallengeDto>();
        loginBody!.MfaChallengeToken.Should().NotBeNullOrEmpty();

        // === act 2: mfa/verify (sin MFA inscrito) ===
        var verifyResp = await client.PostAsJsonAsync("/api/auth/mfa/verify", new
        {
            mfaChallengeToken = loginBody.MfaChallengeToken,
            totpCode = "000000",
            useBackupCode = false
        });
        verifyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await verifyResp.Content.ReadFromJsonAsync<TokensDto>();
        tokens!.AccessToken.Should().NotBeNullOrEmpty();
        tokens.RefreshToken.Should().NotBeNullOrEmpty();

        // === act 3: refresh ===
        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = tokens.RefreshToken
        });
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await refreshResp.Content.ReadFromJsonAsync<TokensDto>();
        rotated!.RefreshToken.Should().NotBe(tokens.RefreshToken);

        // === act 4: logout ===
        var logoutClient = _fx.CreateClient();
        logoutClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rotated.AccessToken);
        var logoutResp = await logoutClient.PostAsJsonAsync("/api/auth/logout", new
        {
            refreshToken = rotated.RefreshToken
        });
        logoutResp.IsSuccessStatusCode.Should().BeTrue();
    }

    private async Task SeedUserAsync(string username, string plainPassword, bool mfaEnabled)
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
            IsMfaEnabled = mfaEnabled,
            LastPasswordChangeAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(default);
    }

    private sealed record LoginChallengeDto(string MfaChallengeToken, bool MustChangePassword);
    private sealed record TokensDto(string AccessToken, string RefreshToken,
        DateTime AccessTokenExpiresAt, DateTime RefreshTokenExpiresAt);
}
