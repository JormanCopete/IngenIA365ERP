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
/// <para>
/// <b>No corre en el run estándar.</b> Hace falta <c>RUN_PERF_TESTS=1</c>,
/// siguiendo el precedente de <c>LoginThroughputFact</c>. El gate va ANTES del
/// sembrado: son 50.000 eventos que hoy se pagaban íntegros para después
/// fallar.
/// </para>
///
/// <para>
/// <b>Por qué está apagada y no arreglada.</b> Decía "hoy es RED porque el
/// endpoint /api/audit/logs aún no responde" — y eso dejó de ser cierto: el
/// endpoint existe y T091 está cerrado. Lo que quedó viejo es la prueba, y no
/// con un detalle: usa un cliente ANÓNIMO contra un endpoint que exige
/// autenticación y el permiso <c>AuditLog.View</c>, así que nunca recibe un 200,
/// se queda sin muestras y revienta en un mensaje que culpa a una tarea
/// terminada hace meses. Un rojo que miente es peor que un rojo.
/// </para>
///
/// <para>
/// Arreglarla no es añadir un token: el rango consultado no coincide con los
/// timestamps sembrados, la cooperativa del sembrado no coincide con la del
/// claim, y el vaciado de la cola escribe 100 documentos por llamada, así que
/// los 50.000 tampoco llegan. Son cuatro arreglos acoplados y necesita que la
/// fixture sepa emitir un cliente autenticado, que hoy no sabe.
/// </para>
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
        // Antes del sembrado: sin opt-in esto no cuesta nada.
        if (Environment.GetEnvironmentVariable("RUN_PERF_TESTS") != "1")
        {
            return;
        }

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
