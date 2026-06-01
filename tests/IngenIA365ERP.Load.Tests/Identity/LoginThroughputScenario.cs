using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;

namespace IngenIA365ERP.Load.Tests.Identity;

/// <summary>
/// T124 (Phase 8) — Throughput de <c>POST /api/auth/login</c> con el flujo
/// de identidad central. Aceptación:
/// <list type="bullet">
///   <item>100 logins por segundo durante 5 minutos.</item>
///   <item>p95 &lt; 800 ms.</item>
///   <item>0 errores 5xx.</item>
/// </list>
///
/// <para>
/// Requiere usuarios pre-creados en la BD. La env var
/// <c>LOADTEST_USER_CSV</c> apunta a un archivo
/// <c>email,password</c> por línea (sin cabecera). Si no se indica, se usa
/// un dataset embebido de 50 usuarios sintéticos (los seeders de Phase 8 lo
/// crean automáticamente en dev).
/// </para>
///
/// <para>
/// Ejecución:
/// <code>
/// LOADTEST_BASE_URL=https://localhost:7200 \
/// LOADTEST_USER_CSV=fixtures/users-50.csv \
/// dotnet test tests/IngenIA365ERP.Load.Tests --filter LoginThroughput
/// </code>
/// </para>
/// </summary>
public static class LoginThroughputScenario
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("LOADTEST_BASE_URL") ?? "http://localhost:5000";

    private static readonly string? UserCsvPath =
        Environment.GetEnvironmentVariable("LOADTEST_USER_CSV");

    public static ScenarioProps Build()
    {
        using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        var users = LoadUsers();

        return Scenario.Create("login_throughput", async ctx =>
            {
                // Round-robin sobre los usuarios sintéticos.
                var user = users[(int)(ctx.InvocationNumber % users.Count)];
                var body = new { email = user.Email, password = user.Password };

                var req = Http.CreateRequest("POST", "/api/auth/login")
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(body),
                        System.Text.Encoding.UTF8, "application/json"));

                using var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
                var resp = await Http.Send(client, req);
                return resp;
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromMinutes(5)))
            .WithoutWarmUp();
    }

    private static IReadOnlyList<SyntheticUser> LoadUsers()
    {
        if (!string.IsNullOrEmpty(UserCsvPath) && File.Exists(UserCsvPath))
        {
            return File.ReadAllLines(UserCsvPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l =>
                {
                    var parts = l.Split(',', 2);
                    return new SyntheticUser(parts[0].Trim(), parts[1].Trim());
                })
                .ToList();
        }

        // Fallback embebido — los seeders de dev crean estos usuarios.
        return Enumerable.Range(1, 50)
            .Select(i => new SyntheticUser(
                Email: $"load.user{i:D2}@cooperativa.test",
                Password: "LoadTest-Pwd-2026"))
            .ToList();
    }

    private sealed record SyntheticUser(string Email, string Password);
}
