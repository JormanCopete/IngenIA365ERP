using System.Diagnostics;
using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Audit;

/// <summary>
/// T085 — Performance del query del audit log con dataset grande (SC-004):
/// con 50.000 eventos poblados, una consulta de 1 mes debe responder p95
/// &lt; 5 s.
///
/// El test es <b>costoso</b>: pre-puebla la BD Mongo a través del propio
/// <c>IAuditService</c> y luego mide. Por eso se marca con un trait
/// <c>perf</c> y debería excluirse del run estándar (correr en CI nightly).
///
/// Hoy es RED porque:
///  1) El endpoint <c>/api/audit/logs</c> aún no responde con el envelope
///     CQRS y puede no estar indexado por <c>tenant + timestamp</c>.
///  2) El index bootstrap (T026/T093) puede no haberse ejecutado contra la
///     colección efímera de Testcontainers.
/// Cuando T087 y los índices aterricen, el p95 debería estar holgado.
/// </summary>
[Trait("category", "perf")]
public class AuditPerformanceTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    private const int EventCount = 50_000;
    private static readonly TimeSpan P95Budget = TimeSpan.FromSeconds(5);

    public AuditPerformanceTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Querying_one_month_over_50k_events_responds_under_5s_p95()
    {
        await SeedAuditEventsAsync(EventCount);

        var client = _fx.CreateClient();
        var samples = new List<TimeSpan>(20);

        // 20 muestras para calcular p95 — la primera puede ser cold.
        for (var i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();
            var resp = await client.GetAsync(
                "/api/audit/logs?from=2026-04-01&to=2026-04-30&pageSize=50");
            sw.Stop();

            // Si todavía no existe el endpoint correcto, ignoramos la muestra
            // — el test es RED por status code, no por latencia.
            if (resp.StatusCode != HttpStatusCode.OK) continue;

            samples.Add(sw.Elapsed);
        }

        samples.Should().NotBeEmpty(
            "el endpoint /api/audit/logs aún no responde 200 — RED esperado hasta T091");

        var sorted = samples.OrderBy(t => t).ToList();
        var p95Index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
        var p95 = sorted[Math.Max(0, p95Index)];

        p95.Should().BeLessThan(P95Budget,
            $"SC-004 exige p95 < 5s — observado {p95.TotalMilliseconds:N0} ms");
    }

    private async Task SeedAuditEventsAsync(int count)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var baseDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
        {
            await audit.LogAsync(new AuditLogCommand
            {
                Action = i % 3 == 0 ? "Created" : i % 3 == 1 ? "Updated" : "Viewed",
                EntityType = "PerformanceProbe",
                EntityId = $"probe-{i}",
                Module = "Audit.PerfTest",
                Endpoint = "/api/seed",
                HttpMethod = "POST",
                HttpStatusCode = 200,
                DurationMs = 1
                // Timestamp lo pone el servicio (DateTime.UtcNow) — el dataset
                // se concentra en el momento del seed, lo que satisface el
                // filtro "abril 2026" si el test corre en ese mes. Para hacer
                // el seed independiente del wall-clock, T087 debería aceptar
                // Timestamp en el AuditLogCommand.
            });
        }
        await audit.FlushAsync();
    }
}
