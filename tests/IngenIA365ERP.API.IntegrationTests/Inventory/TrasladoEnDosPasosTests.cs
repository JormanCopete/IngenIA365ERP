using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Security;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T364 (quickstart §3.9; US10, US3-2; FR-039), en el motor de <c>DB_PROVIDER</c>, sobre el escenario aislado «traslados»:
/// <c>bodega.a</c> despacha de PRIN a PV2 y <c>bodega.b</c> recibe. El despacho no cambia el valorizado total (el tránsito lo
/// lleva); el faltante se resuelve de las tres maneras —baja por reclamación al transportador, devolución al origen y
/// recepción tardía— y el sobrante con su ajuste, siempre con la aprobación de otra persona (<c>jefe</c>); un despacho no
/// recibido se anula y vuelve al origen; el movimiento entre ubicaciones es de un paso; un producto bloqueado con unidades en
/// tránsito sí se recibe; y <c>bodega.b</c> no ve lo de PRIN pero sí el traslado que le llega.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class TrasladoEnDosPasosTests(CentralIdentityApiFixture fx)
{
    private const string Traslados = "/api/inventory/transfers";

    private async Task<(EscenarioDeInventario Esc, UsuariosDelEnsayo U)> PrepararAsync()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "traslados");
        return (esc, await UsuariosDelEnsayo.CrearAsync(fx, esc));
    }

    [Fact]
    public async Task Despachar_no_cambia_el_valorizado_y_el_faltante_se_da_de_baja_con_aprobacion()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 100, 1_000m)]);
        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");
        var antes = await TotalValorizadoAsync(http, esc, hoy);

        var traslado = await DespacharAsync(http, esc, u, "P1", 10);
        (await TotalValorizadoAsync(http, esc, hoy)).Should().Be(antes, "el tránsito lleva el valor del despacho");
        (await esc.FisicaAsync(http, esc.Admin, "P1", "PRIN")).Should().Be(90m);
        (await esc.FisicaAsync(http, esc.Admin, "P1", "TR1")).Should().Be(10m, "entra al tránsito de la sucursal de origen");

        var recibido = await RecibirAsync(http, esc, u, traslado, 9);
        var faltante = recibido.GetProperty("shortages").EnumerateArray().Single();
        faltante.GetProperty("quantityBase").GetDecimal().Should().Be(1m);
        (await esc.FisicaAsync(http, esc.Admin, "P1", "PV2")).Should().Be(9m);

        await ResolverYAprobarAsync(http, esc, u, faltante.GetProperty("discrepancyPublicId").GetGuid(),
            new { resolution = "WriteOffFromTransit", adjustmentCausePublicId = esc.Causas["RECLTRANSP"], reason = "La transportadora perdió una unidad" });
        (await esc.FisicaAsync(http, esc.Admin, "P1", "TR1")).Should().Be(0m);
    }

    [Fact]
    public async Task El_faltante_vuelve_al_origen_o_llega_tarde_por_partes()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P2", 20, 800m)]);

        var traslado = await DespacharAsync(http, esc, u, "P2", 10);
        var faltante = (await RecibirAsync(http, esc, u, traslado, 8)).GetProperty("shortages").EnumerateArray().Single()
            .GetProperty("discrepancyPublicId").GetGuid();

        await ResolverYAprobarAsync(http, esc, u, faltante, new { resolution = "ReturnToOrigin", quantity = 1m, reason = "Una unidad no salió del camión" });
        (await esc.FisicaAsync(http, esc.Admin, "P2", "PRIN")).Should().Be(11m);
        await ResolverYAprobarAsync(http, esc, u, faltante, new { resolution = "LateReceipt", quantity = 1m, reason = "Llegó al día siguiente" });
        (await esc.FisicaAsync(http, esc.Admin, "P2", "PV2")).Should().Be(9m);
        (await esc.FisicaAsync(http, esc.Admin, "P2", "TR1")).Should().Be(0m);
    }

    [Fact]
    public async Task El_sobrante_queda_fuera_de_la_existencia_hasta_aprobar_su_ajuste()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P3", 20, 500m)]);

        var traslado = await DespacharAsync(http, esc, u, "P3", 10);
        var sobrante = (await RecibirAsync(http, esc, u, traslado, 10, sobrante: 1)).GetProperty("surpluses").EnumerateArray().Single();
        (await esc.FisicaAsync(http, esc.Admin, "P3", "PV2")).Should().Be(10m, "el sobrante no entra hasta aprobar su ajuste");

        await ResolverYAprobarAsync(http, esc, u, sobrante.GetProperty("discrepancyPublicId").GetGuid(),
            new { resolution = "SurplusAdjustment", adjustmentCausePublicId = esc.Causas["DIFCONTEO"], reason = "Vino una de más" });
        (await esc.FisicaAsync(http, esc.Admin, "P3", "PV2")).Should().Be(11m);
    }

    [Fact]
    public async Task Un_despacho_no_recibido_se_anula_y_uno_recibido_no()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P4", 10, 900m)]);

        var sinRecibir = await DespacharAsync(http, esc, u, "P4", 5);
        (await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Traslados}/{sinRecibir}/void", new { reason = "Se canceló el envío" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await esc.FisicaAsync(http, esc.Admin, "P4", "PRIN")).Should().Be(10m, "la mercancía vuelve del tránsito al origen");

        var recibido = await DespacharAsync(http, esc, u, "P4", 2);
        await RecibirAsync(http, esc, u, recibido, 2);
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Traslados}/{recibido}/void", new { reason = "Tarde" }),
            "Inventory.Document.HasDependents");
    }

    [Fact]
    public async Task Mover_entre_ubicaciones_es_un_paso_y_un_bloqueado_en_transito_se_recibe()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P5", 6, 700m), new("P6", 5, 300m)]);

        // Movimiento entre ubicaciones (LocationMove): un solo documento, sin cambio de costo.
        var nueva = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/warehouses/{esc.Bodega("PRIN")}/locations",
            new { code = "EST1", name = "Estante 1" });
        var destino = nueva.GetProperty("publicId").GetGuid();
        var ubicaciones = await InventarioE2E.GetAsync(http, esc.Admin, $"/api/inventory/warehouses/{esc.Bodega("PRIN")}/locations");
        var origen = ubicaciones.EnumerateArray().Single(l => l.GetProperty("isDefault").GetBoolean()).GetProperty("publicId").GetGuid();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "MUB", "PRIN", [new("P5", 4, Ubicacion: origen, UbicacionDestino: destino)]);
        var porUbicacion = (await esc.ExistenciaAsync(http, esc.Admin, "P5")).GetProperty("byLocation").EnumerateArray().ToList();
        porUbicacion.Single(l => l.GetProperty("location").GetProperty("publicId").GetGuid() == destino).GetProperty("quantity").GetDecimal().Should().Be(4m);
        (await esc.FisicaAsync(http, esc.Admin, "P5", "PRIN")).Should().Be(6m);

        // Bloqueado con unidades en tránsito: la recepción sí se admite.
        var traslado = await DespacharAsync(http, esc, u, "P6", 3);
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/products/{esc.P("P6").Id}/status",
            new { status = "Blocked", reason = "Revisión de calidad" });
        await RecibirAsync(http, esc, u, traslado, 3);
        (await esc.FisicaAsync(http, esc.Admin, "P6", "PV2")).Should().Be(3m);
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/products/{esc.P("P6").Id}/status",
            new { status = "Active", reason = "Revisión terminada" });
    }

    [Fact]
    public async Task Bodega_b_no_ve_lo_de_PRIN_pero_si_el_traslado_que_le_llega()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P7", 5, 400m)]);
        var traslado = await DespacharAsync(http, esc, u, "P7", 1);

        var lista = await InventarioE2E.GetAsync(http, u.BodegaB.Token, $"{Traslados}?pageSize=100");
        lista.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("dispatchPublicId").GetGuid()).Should().Contain(traslado);
        var ajustes = await InventarioE2E.GetAsync(http, u.BodegaB.Token, $"/api/inventory/adjustments?pageSize=100");
        ajustes.GetProperty("items").EnumerateArray()
            .Should().NotContain(d => d.GetProperty("warehouse").GetProperty("code").GetString() == "PRIN", "el alcance filtra los documentos de PRIN");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static async Task<Guid> DespacharAsync(HttpClient http, EscenarioDeInventario esc, UsuariosDelEnsayo u, string producto, decimal cantidad)
    {
        var borrador = await InventarioE2E.BorradorAsync(http, u.BodegaA.Token, Traslados, new
        {
            documentTypePublicId = esc.Tipos["TRD"], warehousePublicId = esc.Bodega("PRIN"), destinationWarehousePublicId = esc.Bodega("PV2"),
            lines = esc.Lineas(new EscenarioDeInventario.Linea(producto, cantidad)),
        });
        var id = borrador.GetProperty("publicId").GetGuid();
        var resp = await InventarioE2E.MandarAsync(http, u.BodegaA.Token, HttpMethod.Post, $"{Traslados}/{id}/dispatch", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return id;
    }

    private static async Task<JsonElement> RecibirAsync(HttpClient http, EscenarioDeInventario esc, UsuariosDelEnsayo u, Guid traslado, decimal recibido, decimal? sobrante = null)
    {
        var detalle = await InventarioE2E.GetAsync(http, u.BodegaB.Token, $"{Traslados}/{traslado}");
        var linea = detalle.GetProperty("lines")[0].GetProperty("dispatchLinePublicId").GetGuid();
        var resp = await InventarioE2E.MandarAsync(http, u.BodegaB.Token, HttpMethod.Post, $"{Traslados}/{traslado}/receive", new
        {
            lines = new[] { new { dispatchLinePublicId = linea, receivedQuantity = recibido, surplusQuantity = sobrante } },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return await InventarioE2E.LeerAsync(resp);
    }

    /// <summary><c>bodega.b</c> propone la resolución y <c>jefe</c> (que no despachó, no recibió ni propuso) la aprueba.</summary>
    private static async Task ResolverYAprobarAsync(HttpClient http, EscenarioDeInventario esc, UsuariosDelEnsayo u, Guid diferencia, object cuerpo)
    {
        var propuesta = await InventarioE2E.MandarAsync(http, u.BodegaB.Token, HttpMethod.Post, $"{Traslados}/discrepancies/{diferencia}/resolve", cuerpo);
        propuesta.StatusCode.Should().Be(HttpStatusCode.Created, await propuesta.Content.ReadAsStringAsync());
        var solicitud = (await InventarioE2E.LeerAsync(propuesta)).GetProperty("approvalRequestPublicId").GetGuid();

        var propia = await InventarioE2E.DecidirAsync(http, esc.Admin, u.BodegaB.Token, solicitud, aprobar: true);
        propia.StatusCode.Should().NotBe(HttpStatusCode.OK, "quien propone no aprueba");
        var aprobada = await InventarioE2E.DecidirAsync(http, esc.Admin, u.Jefe.Token, solicitud, aprobar: true);
        aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
    }

    private static async Task<decimal> TotalValorizadoAsync(HttpClient http, EscenarioDeInventario esc, string hoy)
    {
        var tabla = await InventarioE2E.InformeAsync(http, esc.Admin, "valuation", $"asOf={hoy}&includeTransit=true");
        return tabla.Numero(tabla.Totales!.Value, "Valor") + tabla.Numero(tabla.Totales!.Value, "En tránsito (valor)");
    }
}
