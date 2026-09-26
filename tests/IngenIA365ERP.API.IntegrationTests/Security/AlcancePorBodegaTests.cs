using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// T419 (quickstart §3.13 pasos 1 y 2; US12-1, US2-5; SC-014, T35), en el motor de <c>DB_PROVIDER</c>, sobre el escenario
/// aislado «alcance» con los usuarios del ensayo: <c>bodega.b</c> (sólo PV2) no ve los documentos de PRIN en las listas —y no
/// cuentan en <c>totalCount</c>—, y el detalle, el kardex de PRIN y crear un ajuste en PRIN le responden el mismo 404 que lo
/// inexistente; la existencia de una bodega de tránsito sólo la ve quien tiene alcance total o asignación explícita;
/// <c>lectura</c> confirmando un ajuste recibe 404 <c>Generic.NotFound</c>; y una alerta de PRIN no llega a la bandeja de
/// <c>bodega.b</c>, aunque tenga el permiso destinatario.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class AlcancePorBodegaTests(CentralIdentityApiFixture fx)
{
    private async Task<(EscenarioDeInventario Esc, UsuariosDelEnsayo U)> PrepararAsync()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "alcance");
        return (esc, await UsuariosDelEnsayo.CrearAsync(fx, esc));
    }

    [Fact]
    public async Task Bodega_b_no_ve_PRIN_y_lo_de_PRIN_es_el_mismo_404_que_lo_inexistente()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        var dePrin = (await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 5, 100m)])).GetProperty("publicId").GetGuid();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV2", [new("P1", 3, 100m)]);
        var b = u.BodegaB.Token;

        var lista = await InventarioE2E.GetAsync(http, b, "/api/inventory/documents?pageSize=100");
        var filas = lista.GetProperty("items").EnumerateArray().ToList();
        filas.Should().NotBeEmpty();
        filas.Should().OnlyContain(d => d.GetProperty("warehouse").GetProperty("code").GetString() == "PV2");
        lista.GetProperty("totalCount").GetInt32().Should().Be(filas.Count, "lo de PRIN no cuenta en el total");

        foreach (var ruta in new[]
                 {
                     $"/api/inventory/documents/{dePrin}", $"/api/inventory/adjustments/{dePrin}",
                     $"/api/inventory/stock?warehousePublicId={esc.Bodega("PRIN")}",
                     $"/api/reports/inventory/kardex?format=json&product={esc.P("P1").Id}&warehouse={esc.Bodega("PRIN")}",
                 })
            (await InventarioE2E.MandarAsync(http, b, HttpMethod.Get, ruta)).StatusCode.Should().Be(HttpStatusCode.NotFound, ruta);
        (await InventarioE2E.MandarAsync(http, b, HttpMethod.Get, $"/api/inventory/documents/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var alta = await InventarioE2E.MandarAsync(http, b, HttpMethod.Post, "/api/inventory/adjustments", new
        {
            documentTypePublicId = esc.Tipos["AJP"], warehousePublicId = esc.Bodega("PRIN"), reason = "Fuera de mi alcance",
            lines = esc.Lineas(new EscenarioDeInventario.Linea("P1", 1)),
        });
        alta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task La_existencia_del_transito_solo_con_alcance_total_o_asignacion_explicita()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        var ruta = $"/api/inventory/stock?warehousePublicId={esc.Bodega("TR2")}";

        (await InventarioE2E.MandarAsync(http, u.BodegaB.Token, HttpMethod.Get, ruta)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "PV2 no le da el tránsito de su sucursal");
        (await InventarioE2E.MandarAsync(http, u.Jefe.Token, HttpMethod.Get, ruta)).StatusCode.Should().Be(HttpStatusCode.OK, "alcance total");

        await InventarioE2E.AlcanceAsync(http, esc.Admin, u.Aprobador.PublicId, esc.Bodega("PRIN"), esc.Bodega("PV1"), esc.Bodega("PV2"), esc.Bodega("TR2"));
        (await InventarioE2E.MandarAsync(http, u.Aprobador.Token, HttpMethod.Get, ruta)).StatusCode.Should().Be(HttpStatusCode.OK, "asignación explícita");
    }

    [Fact]
    public async Task Lectura_confirmando_un_ajuste_recibe_el_404_generico()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        var id = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P2", 1, 100m)]);

        var resp = await InventarioE2E.PedirConfirmarAsync(http, u.Lectura.Token, "/api/inventory/adjustments", id);
        await InventarioE2E.FallaAsync(resp, "Generic.NotFound", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Una_alerta_de_PRIN_no_llega_a_la_bandeja_de_bodega_b()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Put, "/api/inventory/reorder-policies", new
        {
            productPublicId = esc.P("P3").Id, warehousePublicId = esc.Bodega("PRIN"), minimum = 10m, maximum = 50m, reorderPoint = 15m,
        });
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P3", 20, 100m)]);
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJN", "PRIN", [new("P3", 12)]);

        const string ruta = "/api/inventory/alerts?typeCode=Inventario.Reorden&status=Pending&pageSize=50";
        (await InventarioE2E.GetAsync(http, u.BodegaA.Token, ruta)).GetProperty("items").GetArrayLength().Should().BeGreaterThan(0,
            "bodega.a tiene el permiso destinatario (Inventory.Purchases.Create) y alcance sobre PRIN");
        (await InventarioE2E.GetAsync(http, u.BodegaB.Token, ruta)).GetProperty("items").GetArrayLength().Should().Be(0,
            "bodega.b tiene el permiso pero no el alcance");
    }
}
