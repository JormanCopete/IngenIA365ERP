using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;

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
public class ConcurrenciaDeExistenciasTests(CentralIdentityApiFixture fx)
{
    private const string Ajustes = "/api/inventory/adjustments";

    private Task<EscenarioDeInventario> EscenarioAsync() => EscenarioDeInventario.PrepararAsync(fx, "concurrencia");

    [Fact]
    public async Task Cincuenta_salidas_simultaneas_sobre_diez_dejan_diez_confirmadas_y_cuarenta_rechazadas()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        await CarreraAsync(http, esc, "P1", disponibles: 10, salidas: 50, cantidad: 1, esperadas: 10);
    }

    [Fact]
    public async Task Dos_salidas_de_cuatro_sobre_cinco_dejan_una()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        await CarreraAsync(http, esc, "P2", disponibles: 5, salidas: 2, cantidad: 4, esperadas: 1);
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

    [FactDeRendimiento]
    public async Task Mil_repeticiones_de_la_carrera_de_SC_001()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        for (var i = 0; i < 1_000; i++)
            await CarreraAsync(http, esc, "P4", disponibles: 2, salidas: 3, cantidad: 1, esperadas: 2);
    }

    /// <summary>
    /// Deja <paramref name="disponibles"/> del producto en PRIN, prepara <paramref name="salidas"/> borradores de salida y los
    /// confirma todos a la vez: exactamente <paramref name="esperadas"/> pasan, los demás dicen cuánto queda de verdad, nunca
    /// queda negativo y los números son consecutivos.
    /// </summary>
    private static async Task CarreraAsync(HttpClient http, EscenarioDeInventario esc, string producto, decimal disponibles, int salidas, decimal cantidad, int esperadas)
    {
        var antes = await esc.FisicaAsync(http, esc.Admin, producto, "PRIN");
        if (antes > 0) await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJN", "PRIN", [new(producto, antes)]);
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new(producto, disponibles, 1_000m)]);

        var borradores = new List<Guid>();
        for (var i = 0; i < salidas; i++)
            borradores.Add(await esc.AjusteAsync(http, esc.Admin, "AJN", "PRIN", [new(producto, cantidad)], causa: "MERMA"));

        var respuestas = await Task.WhenAll(borradores.Select(id => InventarioE2E.PedirConfirmarAsync(http, esc.Admin, Ajustes, id)));

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

        (await esc.FisicaAsync(http, esc.Admin, producto, "PRIN")).Should().Be(disponibles - esperadas * cantidad).And.BeGreaterThanOrEqualTo(0m);
    }
}
