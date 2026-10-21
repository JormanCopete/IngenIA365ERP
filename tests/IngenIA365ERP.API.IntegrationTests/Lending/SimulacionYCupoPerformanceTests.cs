using System.Diagnostics;
using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using Xunit.Abstractions;
using static IngenIA365ERP.API.IntegrationTests.Lending.OriginacionE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0386 (feature 013, US2, SC-008; quickstart §3.5 paso 11), en el motor de <c>DB_PROVIDER</c> y sólo con <c>RUN_PERF_TESTS=1</c> (sin ella se
/// reporta omitida con su motivo, nunca con un <c>return</c>): sobre una cooperativa aislada con 5.000 cupos otorgados por HTTP, 200 simulaciones
/// de 60 cuotas y 200 consultas de disponible, con p95 de simulación &lt; 2 s y de disponible &lt; 1 s, informados en la salida. La siembra
/// también va por la API (asociado, autorización con su evidencia y cupo asignado por <c>jefe</c>), con 16 a la vez. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class SimulacionYCupoPerformanceTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int Cupos = 5_000;
    private const int Mediciones = 200;

    [FactDeRendimiento]
    public async Task Doscientas_simulaciones_y_doscientos_disponibles_sobre_cinco_mil_cupos_cumplen_sus_p95()
    {
        var esc = await EscenarioAsync(fx, "originacion_volumen", activada: false);
        var admin = esc.Coop.TokenAdmin;
        var jefe = esc.Usuarios.Jefe.Token;
        var personas = new System.Collections.Concurrent.ConcurrentBag<Guid>();

        var siembra = Stopwatch.StartNew();
        await Parallel.ForEachAsync(Enumerable.Range(0, Cupos), new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (i, _) =>
        {
            using var http = fx.CreateClient();
            var persona = await DeudorAsync(http, admin, $"Volumen{i}", conCentrales: false);
            var cupo = await OtorgarCupoAsync(http, jefe, persona, 1_000_000m + i, esc.Hoy);
            cupo.StatusCode.Should().Be(HttpStatusCode.Created, await cupo.Content.ReadAsStringAsync());
            personas.Add(persona);
        });
        salida.WriteLine($"Siembra de {Cupos} cupos: {siembra.Elapsed.TotalSeconds:N0} s.");

        using var cliente = fx.CreateClient();
        var asesor = esc.Usuarios.AsesorO1.Token;
        var simulaciones = new List<double>();
        for (var i = 0; i < Mediciones; i++)
        {
            var reloj = Stopwatch.StartNew();
            var r = await CarteraE2E.ConClaveAsync(cliente, asesor, HttpMethod.Post, $"{Simulaciones}/schedule", new
            {
                creditLinePublicId = esc.Origen, amount = 20_000_000m + i * 1000m, installments = 60, periodicity = "Monthly",
            });
            reloj.Stop();
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            simulaciones.Add(reloj.Elapsed.TotalMilliseconds);
        }

        var muestra = personas.Take(Mediciones).ToList();
        var disponibles = new List<double>();
        foreach (var persona in muestra)
        {
            var reloj = Stopwatch.StartNew();
            var r = await CarteraE2E.MandarGetAsync(cliente, jefe, $"{OriginacionE2E.Cupos}/availability?personPublicId={persona}");
            reloj.Stop();
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            disponibles.Add(reloj.Elapsed.TotalMilliseconds);
        }

        var p95Simulacion = P95(simulaciones);
        var p95Disponible = P95(disponibles);
        salida.WriteLine($"Simulación de 60 cuotas: p95 {p95Simulacion:N0} ms (n = {simulaciones.Count}).");
        salida.WriteLine($"Disponible sobre {Cupos} cupos: p95 {p95Disponible:N0} ms (n = {disponibles.Count}).");
        p95Simulacion.Should().BeLessThan(2_000, "SC-008: la simulación responde en menos de 2 s");
        p95Disponible.Should().BeLessThan(1_000, "SC-008: el disponible responde en menos de 1 s");
    }

    private static double P95(List<double> tiempos)
    {
        var ordenados = tiempos.Order().ToList();
        return ordenados[(int)Math.Ceiling(ordenados.Count * 0.95) - 1];
    }
}
