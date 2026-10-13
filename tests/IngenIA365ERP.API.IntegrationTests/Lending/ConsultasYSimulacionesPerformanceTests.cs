using System.Diagnostics;
using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using Xunit.Abstractions;
using static IngenIA365ERP.API.IntegrationTests.Lending.VentasConCarteraE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0580 (feature 013, US6; quickstart §1.8; C24, G6, SC-008), en el motor de <c>DB_PROVIDER</c> y sólo con <c>RUN_PERF_TESTS=1</c> (sin ella se
/// reporta omitida con su motivo): la <b>prueba de volumen del POS</b> sobre la cooperativa de ventas con Cartera (<see cref="VentasConCarteraE2E"/>):
/// <list type="bullet">
/// <item>1.000 consultas de cupo y estado (<c>POST /api/inventory/sales/credit-evaluations</c>) con p95 &lt; 1 s;</item>
/// <item>confirmaciones concurrentes del POS —las dos cajas a la vez— con la reserva del número dentro de su transacción sobre cuatro deudores
/// compartidos: todas confirman (ningún interbloqueo con el orden filas de Inventario → <c>LND_Setup</c> compartido → <c>LND_DebtorControls</c> →
/// numerador de Cartera → numerador de Inventario) y se informa la latencia de la confirmación.</item>
/// </list>
/// Los resultados se informan en la salida; el runbook <c>docs/operaciones/cartera-ventas-a-credito.md</c> dice cómo leerlos. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ConsultasYSimulacionesPerformanceTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int Consultas = 1_000;
    private const int VentasPorCaja = 25;

    [FactDeRendimiento]
    public async Task Mil_consultas_de_cupo_y_las_confirmaciones_concurrentes_del_POS_cumplen_sin_interbloqueos()
    {
        var esc = await EscenarioAsync(fx, "ventas_cartera_volumen");
        using var http = fx.CreateClient();
        var deudores = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var persona = await AsociadoAsync(http, esc.Admin, $"Volumen{i}", $"7290000{i}");
            var cupo = await OriginacionE2E.OtorgarCupoAsync(http, esc.Admin, persona, 20_000_000m, esc.Hoy, "Revolving");
            cupo.StatusCode.Should().Be(HttpStatusCode.Created, await cupo.Content.ReadAsStringAsync());
            deudores.Add(persona);
        }

        // SC-008: mil consultas de cupo y estado.
        var consultas = new List<double>(Consultas);
        for (var i = 0; i < Consultas; i++)
        {
            var reloj = Stopwatch.StartNew();
            var r = await InventarioE2E.MandarAsync(http, esc.Cajero, HttpMethod.Post, Evaluaciones,
                new { personPublicId = deudores[i % deudores.Count], paymentMeansPublicId = esc.Ventas.Medio("CREDASOC"), amount = 100_000m });
            reloj.Stop();
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            consultas.Add(reloj.Elapsed.TotalMilliseconds);
        }
        var p95Consulta = Percentil(consultas, 0.95);
        salida.WriteLine($"{Consultas} consultas de cupo y estado: p50 {Percentil(consultas, 0.50):N0} ms, p95 {p95Consulta:N0} ms, máximo {consultas.Max():N0} ms.");

        // C24, G6: las dos cajas confirman a la vez sobre los mismos deudores.
        var sesion2 = await EscenarioDeVentas.SesionAsync(http, esc.Ventas.Cajero2.Token, esc.Ventas.Caja2);
        var confirmaciones = new System.Collections.Concurrent.ConcurrentBag<(double Ms, HttpStatusCode Estado, string Cuerpo)>();
        async Task CajaAsync(string cajero, Guid sesion, int desfase)
        {
            using var cliente = fx.CreateClient();
            for (var i = 0; i < VentasPorCaja; i++)
            {
                var reloj = Stopwatch.StartNew();
                var (_, cobro) = await CobrarACreditoAsync(cliente, esc.Ventas, cajero, sesion, deudores[(i + desfase) % deudores.Count], 1,
                    Credito(esc.Ventas, 100_000m, cuotas: 1, plazo: 30));
                reloj.Stop();
                confirmaciones.Add((reloj.Elapsed.TotalMilliseconds, cobro.StatusCode, await cobro.Content.ReadAsStringAsync()));
            }
        }
        var total = Stopwatch.StartNew();
        await Task.WhenAll(CajaAsync(esc.Cajero, esc.Sesion, 0), CajaAsync(esc.Ventas.Cajero2.Token, sesion2, 1));
        total.Stop();
        var tiempos = confirmaciones.Select(c => c.Ms).ToList();
        salida.WriteLine($"{confirmaciones.Count} ventas a crédito confirmadas por dos cajas a la vez en {total.Elapsed.TotalSeconds:N1} s: " +
            $"p50 {Percentil(tiempos, 0.50):N0} ms, p95 {Percentil(tiempos, 0.95):N0} ms, máximo {tiempos.Max():N0} ms (abrir, leer y cobrar).");

        confirmaciones.Should().OnlyContain(c => c.Estado == HttpStatusCode.OK, "ninguna confirmación cae por interbloqueo: " +
            string.Join(" | ", confirmaciones.Where(c => c.Estado != HttpStatusCode.OK).Select(c => $"{(int)c.Estado} {c.Cuerpo}")));
        p95Consulta.Should().BeLessThan(1_000, "SC-008: la consulta de cupo y estado responde en menos de un segundo (p95)");
    }

    private static double Percentil(IReadOnlyList<double> valores, double p)
    {
        var ordenados = valores.Order().ToList();
        var indice = (int)Math.Ceiling(p * ordenados.Count) - 1;
        return ordenados[Math.Clamp(indice, 0, ordenados.Count - 1)];
    }
}
