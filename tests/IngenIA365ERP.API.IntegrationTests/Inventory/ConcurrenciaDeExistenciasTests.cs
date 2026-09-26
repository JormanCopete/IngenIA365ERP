using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using Xunit.Abstractions;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T246 (nombre fijo, decisiones-transversales §2.18; US2-1 sobre ajustes, SC-001, FR-004, FR-038, T15), en el motor de
/// <c>DB_PROVIDER</c> y sobre el escenario aislado «concurrencia»: con el negativo prohibido, cincuenta confirmaciones
/// simultáneas de salidas de 1 sobre 10 disponibles dejan exactamente 10 confirmadas y 40 <c>Inventory.Stock.Insufficient</c>
/// con la cantidad real, sin existencia negativa y con la numeración sin huecos ni repetidos; dos salidas de 4 sobre 5, una;
/// y la primera entrada de un producto crea su fila de proyección con las columnas de auditoría escritas (el
/// <c>INSERT … ON CONFLICT DO NOTHING</c> / <c>WHERE NOT EXISTS … WITH (UPDLOCK, HOLDLOCK)</c> del cerrojo). La variante de
/// SC-001 al pie de la letra (mil repeticiones) sólo corre con <c>RUN_PERF_TESTS=1</c>.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ConcurrenciaDeExistenciasTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const string Ajustes = "/api/inventory/adjustments";

    private Task<EscenarioDeInventario> EscenarioAsync() => EscenarioDeInventario.PrepararAsync(fx, "concurrencia");

    [Fact]
    public async Task Cincuenta_salidas_simultaneas_sobre_diez_dejan_diez_confirmadas_y_cuarenta_rechazadas()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        await CarreraAsync(http, esc, esc.Admin, "P1", disponibles: 10, salidas: 50, cantidad: 1, esperadas: 10);
    }

    [Fact]
    public async Task Dos_salidas_de_cuatro_sobre_cinco_dejan_una()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        await CarreraAsync(http, esc, esc.Admin, "P2", disponibles: 5, salidas: 2, cantidad: 4, esperadas: 1);
    }

    [Fact]
    public async Task La_primera_entrada_crea_la_fila_de_proyeccion_con_su_auditoria()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV1", [new("P3", 2, 800m)]);

        var creadaPor = await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            """SELECT b."CreatedBy" FROM dbo."INV_StockBalances" b JOIN dbo."INV_Products" p ON p."Id" = b."ProductId" JOIN dbo."INV_Warehouses" w ON w."Id" = b."WarehouseId" WHERE p."Code" = 'P3' AND w."Code" = 'PV1' AND b."IsDeleted" = false AND b."PublicId" <> '00000000-0000-0000-0000-000000000000' AND b."CreatedAt" > '2000-01-01'""",
            "SELECT b.[CreatedBy] FROM [dbo].[INV_StockBalances] b JOIN [dbo].[INV_Products] p ON p.[Id] = b.[ProductId] JOIN [dbo].[INV_Warehouses] w ON w.[Id] = b.[WarehouseId] WHERE p.[Code] = 'P3' AND w.[Code] = 'PV1' AND b.[IsDeleted] = 0 AND b.[PublicId] <> '00000000-0000-0000-0000-000000000000' AND b.[CreatedAt] > '2000-01-01'");
        creadaPor.Should().NotBeNull("la fila de la proyección nace con PublicId, CreatedAt, CreatedBy e IsDeleted escritos (T15)");
        creadaPor!.ToString().Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// SC-001 al pie de la letra (T984, T994): mil repeticiones de cincuenta salidas simultáneas de 1 sobre existencia 10. En
    /// I1 no hay ventas todavía (llegan con US5): la salida es un ajuste negativo, que pasa por el mismo cerrojo y el mismo
    /// numerador que la venta. Deja en la salida de la prueba el tiempo total, el p95 de cada repetición completa (reponer,
    /// preparar los 50 borradores y confirmarlos) y el p95 de la ráfaga de confirmaciones sola. Sin <c>RUN_PERF_TESTS=1</c> se
    /// reporta omitida.
    /// </summary>
    [FactDeRendimiento]
    public async Task Mil_repeticiones_de_la_carrera_de_SC_001()
    {
        const int repeticiones = 1_000;
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(5);

        // El access dura 15 minutos y la corrida entera pasa de media hora: se vuelve a entrar como el administrador de la
        // cooperativa cada diez (el alta de InventarioE2E.CooperativaAisladaAsync fija su correo y su contraseña).
        var token = esc.Admin;
        var renovar = DateTime.UtcNow; // la primera entrada se hace antes de la primera repetición: si fallara, falla ya

        var total = Stopwatch.StartNew();
        var porRepeticion = new List<double>(repeticiones);
        var porRafaga = new List<double>(repeticiones);
        for (var i = 0; i < repeticiones; i++)
        {
            // Cada repetición hace ~115 peticiones y el limitador general admite 1.000 por minuto y por IP (la de
            // CF-Connecting-IP): sin esto la prueba mide el 429 y no el cerrojo. Una IP distinta por repetición.
            http.DefaultRequestHeaders.Remove("CF-Connecting-IP");
            http.DefaultRequestHeaders.Add("CF-Connecting-IP", $"10.250.{i / 250}.{i % 250 + 1}");
            if (DateTime.UtcNow >= renovar)
            {
                token = await EntrarComoAdministradorAsync(http);
                renovar = DateTime.UtcNow.AddMinutes(10);
            }
            var reloj = Stopwatch.StartNew();
            porRafaga.Add(await CarreraAsync(http, esc, token, "P4", disponibles: 10, salidas: 50, cantidad: 1, esperadas: 10));
            porRepeticion.Add(reloj.Elapsed.TotalMilliseconds);
        }
        total.Stop();

        salida.WriteLine($"SC-001 [{Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "PostgreSql"}]: {repeticiones:N0} repeticiones de 50 salidas sobre 10 en {total.Elapsed.ToString(@"hh\:mm\:ss")}; " +
            $"p95 por repetición {P95(porRepeticion):N0} ms (mediana {Percentil(porRepeticion, 0.5):N0}, máx. {porRepeticion.Max():N0}); " +
            $"p95 de la ráfaga de confirmaciones {P95(porRafaga):N0} ms (mediana {Percentil(porRafaga, 0.5):N0}, máx. {porRafaga.Max():N0}).");
    }

    private static async Task<string> EntrarComoAdministradorAsync(HttpClient http)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email = "admin.concurrencia@coop.inventario.test", password = "Inv-concurrencia-2026!" });
        var cuerpo = await InventarioE2E.LeerAsync(resp);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, cuerpo.ToString());
        cuerpo.GetProperty("challenge").GetString().Should().Be("None", "el administrador de la cooperativa de prueba entra sin segundo factor");
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static double P95(List<double> tiempos) => Percentil(tiempos, 0.95);

    private static double Percentil(List<double> tiempos, double p)
    {
        var orden = tiempos.OrderBy(t => t).ToList();
        return orden[Math.Max(0, (int)Math.Ceiling(orden.Count * p) - 1)];
    }

    /// <summary>
    /// Deja <paramref name="disponibles"/> del producto en PRIN, prepara <paramref name="salidas"/> borradores de salida y los
    /// confirma todos a la vez: exactamente <paramref name="esperadas"/> pasan, los demás dicen cuánto queda de verdad, nunca
    /// queda negativo y los números son consecutivos. Devuelve lo que tardó la ráfaga de confirmaciones, en milisegundos.
    /// </summary>
    private static async Task<double> CarreraAsync(HttpClient http, EscenarioDeInventario esc, string token, string producto, decimal disponibles, int salidas, decimal cantidad, int esperadas)
    {
        var antes = await esc.FisicaAsync(http, token, producto, "PRIN");
        if (antes > 0) await esc.AjusteConfirmadoAsync(http, token, "AJN", "PRIN", [new(producto, antes)]);
        await esc.AjusteConfirmadoAsync(http, token, "AJP", "PRIN", [new(producto, disponibles, 1_000m)]);

        var borradores = new List<Guid>();
        for (var i = 0; i < salidas; i++)
            borradores.Add(await esc.AjusteAsync(http, token, "AJN", "PRIN", [new(producto, cantidad)], causa: "MERMA"));

        var rafaga = Stopwatch.StartNew();
        var respuestas = await Task.WhenAll(borradores.Select(id => InventarioE2E.PedirConfirmarAsync(http, token, Ajustes, id)));
        rafaga.Stop();

        var confirmadas = respuestas.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        var rechazadas = respuestas.Where(r => r.StatusCode != HttpStatusCode.OK).ToList();
        confirmadas.Should().HaveCount(esperadas);
        rechazadas.Should().HaveCount(salidas - esperadas);
        foreach (var r in rechazadas)
        {
            var sobre = await InventarioE2E.FallaAsync(r, "Inventory.Stock.Insufficient");
            sobre.GetProperty("data").GetProperty("available").GetDecimal().Should().BeLessThan(cantidad, "la respuesta dice lo que realmente queda");
        }

        var numeros = new List<long>();
        foreach (var r in confirmadas) numeros.Add((await InventarioE2E.LeerAsync(r)).GetProperty("number").GetInt64());
        numeros.Should().OnlyHaveUniqueItems();
        (numeros.Max() - numeros.Min() + 1).Should().Be(numeros.Count, "la numeración no deja huecos (FR-038)");

        (await esc.FisicaAsync(http, token, producto, "PRIN")).Should().Be(disponibles - esperadas * cantidad).And.BeGreaterThanOrEqualTo(0m);
        return rafaga.Elapsed.TotalMilliseconds;
    }
}
