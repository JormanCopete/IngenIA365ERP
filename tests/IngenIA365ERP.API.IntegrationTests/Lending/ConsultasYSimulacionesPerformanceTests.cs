using System.Diagnostics;
using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.Integration;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
/// numerador de Cartera → numerador de Inventario) y se informa la latencia de la confirmación;</item>
/// <item>fase 24 (T1066): 500 simulaciones de plan (<c>POST /api/lending/simulations/schedule</c>) y 500 simulaciones de pago
/// (<c>POST /api/lending/loans/{id}/payment-simulations</c>) sobre las obligaciones que nacieron de esas ventas, con p95 &lt; 2 s (SC-008).</item>
/// </list>
/// Y, en su propia cooperativa, el caso de <b>SC-005 con el despachador real</b>: 200 ventas a crédito confirmadas con
/// <see cref="DespachadorDeMensajes"/> corriendo su ciclo (no pasadas a mano), midiendo de la confirmación a que la obligación existe y su
/// <c>ObligacionCreada</c> quedó emitida; p100 &lt; 5 s. Los resultados se informan en la salida; el runbook
/// <c>docs/operaciones/cartera-ventas-a-credito.md</c> dice cómo leerlos. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ConsultasYSimulacionesPerformanceTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int Consultas = 1_000;
    private const int VentasPorCaja = 25;
    private const int Simulaciones = 500;
    private const int VentasConDespachador = 200;
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(30);

    [FactDeRendimiento]
    public async Task Mil_consultas_de_cupo_y_las_confirmaciones_concurrentes_del_POS_cumplen_sin_interbloqueos()
    {
        var esc = await EscenarioAsync(fx, "ventas_cartera_volumen");
        using var http = fx.CreateClient();
        var deudores = await DeudoresConCupoAsync(http, esc, 4, "Volumen", "7290000");

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

        // Fase 24 (T1066): las ventas nacen como obligaciones (pasadas del despachador a mano) y sobre ellas se simulan planes y pagos.
        await DespacharAsync(fx, esc);
        var (linea, obligaciones) = await EnLaBaseAsync(fx, esc, async db => (
            await db.CreditLines.AsNoTracking().Where(l => l.Code == Linea).Select(l => l.PublicId).SingleAsync(),
            await db.Loans.AsNoTracking().Where(l => l.Origin == LoanOrigin.Commercial).Select(l => l.PublicId).Take(20).ToListAsync()));
        obligaciones.Should().NotBeEmpty("las ventas a crédito confirmadas nacen como obligaciones de Cartera");

        var planes = new List<double>(Simulaciones);
        for (var i = 0; i < Simulaciones; i++)
        {
            var reloj = Stopwatch.StartNew();
            using var r = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, "/api/lending/simulations/schedule", new
            {
                creditLinePublicId = linea, amount = 1_000_000m + i * 1_000m, installments = 12, periodicity = "Monthly",
            });
            reloj.Stop();
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            planes.Add(reloj.Elapsed.TotalMilliseconds);
        }
        var pagos = new List<double>(Simulaciones);
        for (var i = 0; i < Simulaciones; i++)
        {
            var reloj = Stopwatch.StartNew();
            using var r = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post,
                $"{Creditos}/{obligaciones[i % obligaciones.Count]}/payment-simulations", new { modality = "FreeAmount", amount = 10_000m + i, valueDate = esc.Hoy });
            reloj.Stop();
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            pagos.Add(reloj.Elapsed.TotalMilliseconds);
        }
        salida.WriteLine($"{Simulaciones} simulaciones de plan: p50 {Percentil(planes, 0.50):N0} ms, p95 {Percentil(planes, 0.95):N0} ms.");
        salida.WriteLine($"{Simulaciones} simulaciones de pago sobre {obligaciones.Count} obligaciones: p50 {Percentil(pagos, 0.50):N0} ms, " +
                         $"p95 {Percentil(pagos, 0.95):N0} ms.");
        Percentil(planes, 0.95).Should().BeLessThan(2_000, "SC-008: la simulación del plan responde en menos de 2 s (p95)");
        Percentil(pagos, 0.95).Should().BeLessThan(2_000, "SC-008: la simulación de un pago responde en menos de 2 s (p95)");
    }

    /// <summary>
    /// Fase 24 (T1066; SC-005): con el <b>despachador real</b> —el <see cref="DespachadorDeMensajes"/> del host corriendo su ciclo, que despierta con
    /// el aviso de mensajes nuevos o por su intervalo, sobre el destino <c>Lending</c> habilitado de la cooperativa—, cada una de 200 ventas a
    /// crédito confirmadas en Comercial tiene su obligación en Cartera y su <c>ObligacionCreada</c> emitida en menos de 5 s desde la confirmación
    /// (p100), con p50 y p95 en la salida. La fixture tiene el ciclo apagado (<c>Integration:Dispatcher:Enabled = false</c>): esta prueba lo arranca y
    /// lo detiene ella misma, así que sólo aquí corre.
    /// </summary>
    [FactDeRendimiento]
    public async Task Con_el_despachador_real_cada_venta_a_credito_tiene_su_obligacion_en_menos_de_cinco_segundos()
    {
        var esc = await EscenarioAsync(fx, "ventas_cartera_despachador");
        using var http = fx.CreateClient();
        var deudores = await DeudoresConCupoAsync(http, esc, 4, "Despacho", "7291000");
        var despachador = fx.Factory.Services.GetRequiredService<DespachadorDeMensajes>();
        var latencias = new List<double>(VentasConDespachador);

        await despachador.StartAsync(CancellationToken.None);
        try
        {
            for (var i = 0; i < VentasConDespachador; i++)
            {
                var (venta, cobro) = await CobrarACreditoAsync(http, esc.Ventas, esc.Cajero, esc.Sesion, deudores[i % deudores.Count], 1,
                    Credito(esc.Ventas, 100_000m, cuotas: 1, plazo: 30));
                var reloj = Stopwatch.StartNew();
                cobro.StatusCode.Should().Be(HttpStatusCode.OK, await cobro.Content.ReadAsStringAsync());
                var numero = (await PagoACreditoAsync(http, esc.Cajero, venta)).GetProperty("reservedLoanNumber").GetString()!;
                while (true)
                {
                    var listo = await EnLaBaseAsync(fx, esc, async db =>
                        await db.Loans.AsNoTracking().AnyAsync(l => l.Number == numero)
                        && await db.IntegrationMessages.AsNoTracking().AnyAsync(m => m.Type == ObligacionCreadaV1.Type && m.PayloadJson.Contains(numero)));
                    if (listo) break;
                    reloj.Elapsed.Should().BeLessThan(EsperaMaxima, $"la venta {venta} (crédito {numero}) no tuvo su obligación con el despachador corriendo");
                    await Task.Delay(25);
                }
                reloj.Stop();
                latencias.Add(reloj.Elapsed.TotalMilliseconds);
            }
        }
        finally
        {
            await despachador.StopAsync(CancellationToken.None);
        }

        salida.WriteLine($"SC-005 con el despachador real, {latencias.Count} ventas: p50 {Percentil(latencias, 0.50):N0} ms, " +
                         $"p95 {Percentil(latencias, 0.95):N0} ms, máximo {latencias.Max():N0} ms.");
        latencias.Max().Should().BeLessThan(5_000, "SC-005: de la confirmación a la obligación con su ObligacionCreada en menos de 5 s (p100)");
    }

    /// <summary>Asociados con un cupo rotativo de 20.000.000 cada uno, para vender a crédito sobre ellos.</summary>
    private static async Task<List<Guid>> DeudoresConCupoAsync(HttpClient http, Escenario esc, int cuantos, string nombre, string documento)
    {
        var deudores = new List<Guid>();
        for (var i = 0; i < cuantos; i++)
        {
            var persona = await AsociadoAsync(http, esc.Admin, $"{nombre}{i}", $"{documento}{i}");
            var cupo = await OriginacionE2E.OtorgarCupoAsync(http, esc.Admin, persona, 20_000_000m, esc.Hoy, "Revolving");
            cupo.StatusCode.Should().Be(HttpStatusCode.Created, await cupo.Content.ReadAsStringAsync());
            deudores.Add(persona);
        }
        return deudores;
    }

    private static double Percentil(IReadOnlyList<double> valores, double p)
    {
        var ordenados = valores.Order().ToList();
        var indice = (int)Math.Ceiling(p * ordenados.Count) - 1;
        return ordenados[Math.Clamp(indice, 0, ordenados.Count - 1)];
    }
}
