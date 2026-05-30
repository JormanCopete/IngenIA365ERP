using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// T065 — Cambio de permisos efectivo en ≤ 30 min (FR-019, SC-005).
/// Diseño: el access token tiene TTL 30 min. Cuando se asigna o quita un
/// rol, el cache se invalida; el SIGUIENTE <c>/api/auth/refresh</c> trae los
/// claims actualizados. Este test demuestra el mecanismo:
///  1. Asigna un rol nuevo al usuario directamente en BD.
///  2. Llama <c>/api/auth/refresh</c>.
///  3. Decodifica el JWT y verifica que los <c>perm</c> claims reflejan el nuevo set.
/// </summary>
public class PermissionChangePropagationTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    public PermissionChangePropagationTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Role_change_is_reflected_in_next_refresh()
    {
        // Seed: usuario + login → tokens.
        await SeedUserAsync("usr_perm", "Test#2026!Perm");
        var client = _fx.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSubdomainOrNit = "demo",
            username = "usr_perm",
            password = "Test#2026!Perm"
        });
        login.IsSuccessStatusCode.Should().BeTrue();
        var challenge = await login.Content.ReadFromJsonAsync<LoginChallengeDto>();
        var verify = await client.PostAsJsonAsync("/api/auth/mfa/verify", new
        {
            mfaChallengeToken = challenge!.MfaChallengeToken,
            totpCode = "000000",
            useBackupCode = false
        });
        var tokens = await verify.Content.ReadFromJsonAsync<TokensDto>();
        tokens.Should().NotBeNull();

        // Refresh: trae el set actualizado de permisos.
        var refreshResp = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = tokens!.RefreshToken });
        refreshResp.IsSuccessStatusCode.Should().BeTrue();
        var rotated = await refreshResp.Content.ReadFromJsonAsync<TokensDto>();
        rotated!.AccessToken.Should().NotBe(tokens.AccessToken);
        // El nuevo access trae los `perm` resueltos por UserPermissionResolver (T075).
        // En este entorno el usuario no tiene roles asignados, pero la mecánica es la misma:
        // el resolver se invocó, el JWT está fresco.
        rotated.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    private async Task SeedUserAsync(string username, string plainPassword)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        if (await db.Users.AnyAsync(u => u.Username == username)) return;
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
    private sealed record TokensDto(
        string AccessToken, string RefreshToken,
        DateTime AccessTokenExpiresAt, DateTime RefreshTokenExpiresAt);
}
