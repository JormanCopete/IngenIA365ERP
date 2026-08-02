using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Progresión de lockout del login central (research D-11, T040/T066):
/// 5 fallos consecutivos sobre el mismo email disparan un bloqueo suave de
/// 60 segundos (<c>Identity.Locked.Soft</c>). Verificaciones:
/// <list type="number">
///   <item>Fallos 1-4 → <c>Identity.InvalidCredentials</c> (antienumeración FR-041).</item>
///   <item>El 5º fallo cruza el umbral y ARMA el lock (la respuesta del 5º
///         intento sigue siendo InvalidCredentials: el LockoutCheck corre
///         ANTES de registrar el fallo).</item>
///   <item>Intento 6 → <c>Identity.Locked.Soft</c> con "Reintenta en N segundos"
///         y N ≤ 60 (ventana del primer nivel de escalado).</item>
///   <item>Un login con password CORRECTA durante el bloqueo también es
///         rechazado con <c>Identity.Locked.Soft</c>: el lock es por email,
///         no por credencial.</item>
/// </list>
/// Nota HTTP: el <c>ErrorEnvelopeFilter</c> no tiene mapeo específico para
/// <c>Identity.Locked.Soft</c> ni para <c>Identity.InvalidCredentials</c>,
/// por lo que ambos caen al default 422 UnprocessableEntity con la envolvente
/// <c>{ code, message, traceId }</c> (FR-048). No esperar 60s reales: en lugar
/// de dejar expirar la ventana, validamos el estado bloqueado en caliente.
/// Usa un usuario PROPIO para no contaminar el contador Redis de otros tests.
/// </summary>
public sealed class EndToEnd_LockoutProgression(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private const string UserEmail = "lockout.progresion@integration.test";
    private const string UserPassword = "Lockout-Correcta-2026!";
    private const string WrongPassword = "Password-Incorrecta-2026!";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Cinco_fallos_bloquean_y_el_login_correcto_durante_el_bloqueo_tambien_es_rechazado()
    {
        await SeedUserAsync();
        using var http = fx.CreateClient();

        // 1) Fallos 1-4: siempre InvalidCredentials, nunca lock todavía.
        for (var intento = 1; intento <= 4; intento++)
        {
            var (status, body) = await LoginAsync(http, UserEmail, WrongPassword);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
            Assert.Equal("Identity.InvalidCredentials",
                body.GetProperty("code").GetString());
        }

        // 2) 5º fallo: cruza el umbral (5, 60s) y arma el lock en Redis.
        //    La respuesta del propio 5º intento sigue siendo InvalidCredentials
        //    porque el LockoutCheck se evalúa ANTES de registrar este fallo.
        var (quintoStatus, quintoBody) = await LoginAsync(http, UserEmail, WrongPassword);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, quintoStatus);
        Assert.Equal("Identity.InvalidCredentials",
            quintoBody.GetProperty("code").GetString());

        // 3) Intento 6 (aún con password mala): ya bloqueado → Locked.Soft,
        //    mensaje "Reintenta en N segundos" con N dentro de la ventana de 60s.
        var (sextoStatus, sextoBody) = await LoginAsync(http, UserEmail, WrongPassword);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sextoStatus);
        Assert.Equal("Identity.Locked.Soft", sextoBody.GetProperty("code").GetString());
        var retryAfter = ExtraerSegundos(sextoBody.GetProperty("message").GetString());
        Assert.InRange(retryAfter, 1, 60);

        // 4) Login con la password CORRECTA durante el bloqueo → también
        //    rechazado con Locked.Soft (el lock es por email, pre-credencial).
        var (correctoStatus, correctoBody) = await LoginAsync(http, UserEmail, UserPassword);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, correctoStatus);
        Assert.Equal("Identity.Locked.Soft", correctoBody.GetProperty("code").GetString());
        Assert.False(correctoBody.TryGetProperty("accessToken", out _),
            "Un login durante el bloqueo jamás debe emitir tokens.");

        // 5) La envolvente de error trae traceId (FR-048).
        Assert.False(string.IsNullOrWhiteSpace(
            correctoBody.GetProperty("traceId").GetString()));
    }

    // -------------------- Helpers --------------------

    /// <summary>
    /// Siembra el usuario dedicado de este test (idempotente) vía
    /// <c>UserManager</c> real — ejercita el BcryptPasswordHasher igual que
    /// el seed del master en la fixture. Sin membresías: no las necesita,
    /// el lockout se decide antes de resolver tenants.
    /// </summary>
    private async Task SeedUserAsync()
    {
        using var scope = fx.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<CentralUserIdentity>>();

        if (await userManager.FindByEmailAsync(UserEmail) is not null) return;

        var user = new CentralUserIdentity
        {
            Id = Guid.NewGuid(),
            UserName = UserEmail,
            Email = UserEmail,
            EmailConfirmed = true,
        };
        var created = await userManager.CreateAsync(user, UserPassword);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo sembrar el usuario de lockout: " +
                string.Join("; ", created.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> LoginAsync(
        HttpClient http, string email, string password)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email, password });
        var raw = await resp.Content.ReadAsStringAsync();
        return (resp.StatusCode, JsonSerializer.Deserialize<JsonElement>(raw, Json));
    }

    /// <summary>Extrae los segundos de "Reintenta en N segundos".</summary>
    private static int ExtraerSegundos(string? message)
    {
        Assert.False(string.IsNullOrWhiteSpace(message));
        var match = Regex.Match(message!, @"(\d+)\s*segundos");
        Assert.True(match.Success,
            $"El mensaje de lockout no contiene los segundos de reintento: '{message}'");
        return int.Parse(match.Groups[1].Value);
    }
}
