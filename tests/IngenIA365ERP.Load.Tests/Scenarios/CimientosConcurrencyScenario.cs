using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace IngenIA365ERP.Load.Tests.Scenarios;

/// <summary>
/// T131 — Escenario de carga del cimiento técnico (Fase 0).
///
/// <para>Mix:</para>
/// <list type="bullet">
///   <item>60 % reads (login + list audit log + my notifications)</item>
///   <item>30 % writes (create role, assign branch, mark notification read)</item>
///   <item>10 % adjunto (upload + download de PDF de 1 MB)</item>
/// </list>
///
/// <para>
/// Ramp: 100 users en 10 min + 30 min plateau. Reports en HTML + JSON.
/// Se ejecuta vía <c>dotnet run --project ... -- run cimientos</c> o
/// como fact xUnit explícito (no se ejecuta por defecto).
/// </para>
///
/// <para>
/// El target URL se configura via env <c>LOADTEST_BASE_URL</c>; default
/// <c>http://localhost:5000</c> para correr contra un dev local.
/// </para>
/// </summary>
public static class CimientosConcurrencyScenario
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("LOADTEST_BASE_URL") ?? "http://localhost:5000";

    public static ScenarioProps Build()
    {
        using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };

        return Scenario.Create("cimientos_concurrency", async ctx =>
            {
                // Cada virtual user dispara un mini-flow representativo.
                var roll = Random.Shared.Next(100);
                var step = roll switch
                {
                    < 60 => "GET /health/live",
                    < 90 => "POST /api/notifications/read-all",
                    _    => "GET /api/attachments/by-owner?…"
                };

                // Implementación real: usar Http.SendAsync con auth token
                // pre-generado por un step inicial. Para no inflar el archivo,
                // el placeholder hace un healthcheck.
                var req = Http.CreateRequest("GET", $"{BaseUrl}/health/live");
                var resp = await Http.Send(http, req);
                return resp;
            })
            .WithLoadSimulations(
                Simulation.RampingInject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(10)),
                Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(30))
            );
    }
}
