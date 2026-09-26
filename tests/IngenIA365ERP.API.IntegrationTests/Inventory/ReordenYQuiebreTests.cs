using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Security;
using IngenIA365ERP.Application.Inventory.Replenishment;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T949 (quickstart §3.12; US17-2 en su parte de I1; FR-035, FR-022; SC-022), en el motor de <c>DB_PROVIDER</c>, sobre el
/// escenario aislado «reorden» con los usuarios del ensayo: P2 en PV1 (mínimo 10, punto 15, máximo 50) con 12 disponibles y 5 en
/// tránsito hacia PV1 no aparece en <c>reorder-alerts</c>; al bajar a 8 la confirmación avisa
/// <c>Inventory.Stock.BelowReorderPoint</c> y levanta <c>Inventario.Reorden</c> (sugerido 37) e <c>Inventario.Quiebre</c> sin esperar
/// a la tarea; las ve quien tiene el permiso destinatario y alcance sobre PV1 y no <c>bodega.b</c>; se atienden con nota; la
/// revisión diaria disparada a mano (los trabajos de fondo están apagados en la fixture) levanta la de un producto que quedó bajo
/// su punto sin salidas y no duplica las pendientes; y, sin nadie con el permiso destinatario, la alerta llega a
/// <c>CompanyAdmin</c> con <c>withoutRecipient</c>.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ReordenYQuiebreTests(CentralIdentityApiFixture fx)
{
    private const string Alertas = "/api/inventory/alerts";

    [Fact]
    public async Task La_posicion_cuenta_el_transito_y_bajo_el_punto_avisa_y_alerta_sin_esperar_a_la_tarea()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "reorden");
        var u = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        await PoliticaAsync(http, esc, "P2", "PV1");
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV1", [new("P2", 12, 1_000m)]);
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P2", 5, 1_000m)]);
        var traslado = await InventarioE2E.BorradorAsync(http, esc.Admin, "/api/inventory/transfers", new
        {
            documentTypePublicId = esc.Tipos["TRD"], warehousePublicId = esc.Bodega("PRIN"), destinationWarehousePublicId = esc.Bodega("PV1"),
            lines = esc.Lineas(new EscenarioDeInventario.Linea("P2", 5)),
        });
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/transfers/{traslado.GetProperty("publicId").GetGuid()}/dispatch", new { });

        (await FilasDeReordenAsync(http, esc, "P2")).Should().BeEmpty("posición 12 + 5 = 17, sobre el punto 15");
        (await PendientesAsync(http, esc.Admin, "Inventario.Reorden")).Should().BeEmpty();

        var salida = await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJN", "PV1", [new("P2", 4)]);
        salida.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("code").GetString())
            .Should().Contain("Inventory.Stock.BelowReorderPoint", "la confirmación avisa sin esperar a la revisión");

        var fila = (await FilasDeReordenAsync(http, esc, "P2")).Single();
        var tabla = await TablaDeReordenAsync(http, esc);
        tabla.Numero(fila, "Posición").Should().Be(13m);
        tabla.Numero(fila, "Sugerido").Should().Be(37m, "50 − 13");

        var reorden = await PendientesAsync(http, u.BodegaA.Token, "Inventario.Reorden");
        reorden.Should().ContainSingle("bodega.a tiene Inventory.Purchases.Create y alcance sobre PV1");
        (await PendientesAsync(http, u.BodegaB.Token, "Inventario.Reorden")).Should().BeEmpty("bodega.b no tiene alcance sobre PV1");
        var quiebre = (await PendientesAsync(http, u.Jefe.Token, "Inventario.Quiebre")).Single();

        var atendida = await InventarioE2E.ExitoAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Alertas}/{quiebre.GetProperty("publicId").GetGuid()}/attend",
            new { note = "Pedido al proveedor habitual" });
        atendida.GetProperty("attendNote").GetString().Should().Be("Pedido al proveedor habitual");

        // La revisión diaria, disparada a mano, no duplica la pendiente y levanta la de un producto que quedó bajo su punto sin salidas.
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV1", [new("P3", 5, 700m)]);
        await PoliticaAsync(http, esc, "P3", "PV1");
        await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, TareaDeRevisionDeReorden.NombreDeLaTarea);
        (await PendientesAsync(http, u.BodegaA.Token, "Inventario.Reorden")).Should().HaveCount(2, "la de P2 sigue siendo una y aparece la de P3");
    }

    [Fact]
    public async Task Sin_nadie_con_el_permiso_destinatario_la_alerta_llega_a_CompanyAdmin()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "reordensolo");
        using var http = fx.CreateClient();
        await PoliticaAsync(http, esc, "P4", "PV2");
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV2", [new("P4", 12, 500m)]);
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJN", "PV2", [new("P4", 4)]);

        var alerta = (await PendientesAsync(http, esc.Admin, "Inventario.Reorden")).Single();
        alerta.GetProperty("withoutRecipient").GetBoolean().Should().BeTrue("sólo el administrador de la cooperativa puede verla (SC-022)");
        var tipos = await InventarioE2E.GetAsync(http, esc.Admin, "/api/inventory/alert-types");
        tipos.EnumerateArray().Single(t => t.GetProperty("typeCode").GetString() == "Inventario.Reorden")
            .GetProperty("withoutRecipient").GetBoolean().Should().BeTrue("el reporte de completitud lista el tipo cuyo permiso nadie tiene");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static Task<JsonElement> PoliticaAsync(HttpClient http, EscenarioDeInventario esc, string producto, string bodega) =>
        InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Put, "/api/inventory/reorder-policies", new
        {
            productPublicId = esc.P(producto).Id, warehousePublicId = esc.Bodega(bodega), minimum = 10m, maximum = 50m, reorderPoint = 15m,
        });

    private static Task<Accounting.ContabilidadE2E.Tabla> TablaDeReordenAsync(HttpClient http, EscenarioDeInventario esc) =>
        InventarioE2E.InformeAsync(http, esc.Admin, "reorder-alerts", $"warehouse={esc.Bodega("PV1")}");

    private static async Task<List<JsonElement>> FilasDeReordenAsync(HttpClient http, EscenarioDeInventario esc, string producto)
    {
        var tabla = await TablaDeReordenAsync(http, esc);
        var clave = tabla.IndicePorClave("_producto");
        return tabla.Filas.Where(f => Accounting.ContabilidadE2E.Tabla.Texto(f, clave) == esc.P(producto).Id.ToString()).ToList();
    }

    private static async Task<List<JsonElement>> PendientesAsync(HttpClient http, string token, string tipo) =>
        (await InventarioE2E.GetAsync(http, token, $"{Alertas}?typeCode={tipo}&status=Pending&pageSize=50")).GetProperty("items").EnumerateArray().ToList();
}
