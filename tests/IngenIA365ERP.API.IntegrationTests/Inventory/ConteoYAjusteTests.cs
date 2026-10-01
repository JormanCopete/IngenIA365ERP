using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Security;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T388 (quickstart §3.10; US11; FR-040, FR-041), en el motor de <c>DB_PROVIDER</c>, sobre el escenario aislado «conteos»:
/// <c>jefe</c> abre un conteo cíclico de la ubicación por defecto de PV1 y la foto bloquea las salidas del producto; dos
/// contadores capturan a la vez; la diferencia fuera de tolerancia exige reconteo; el cierre numera; el ajuste nace en
/// aprobación y no lo deciden quien abrió ni quien contó, sí <c>aprobador</c>, fechado en la foto y al promedio de esa fecha
/// aunque después haya salidas del mismo producto en PRIN (ámbito cooperativa); la vista <c>count-differences</c> lo muestra; y
/// descartar un conteo abierto no deja hueco en la numeración.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ConteoYAjusteTests(CentralIdentityApiFixture fx)
{
    private const string Conteos = "/api/inventory/counts";

    [Fact]
    public async Task Del_conteo_abierto_al_ajuste_aprobado_en_la_fecha_de_la_foto()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "conteos");
        var u = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV1", [new("P1", 20, 1_000m)]);
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 10, 1_000m), new("P2", 3, 100m), new("P3", 3, 100m)]);
        var ubicacion = await UbicacionPorDefectoAsync(http, esc, "PV1");

        // (1) jefe abre: la foto queda fija, sin número, y bloquea las salidas de P1 en PV1.
        var conteo = await AbrirAsync(http, esc, u, "PV1", new { scope = "Location", locationPublicIds = new[] { ubicacion } },
            [u.BodegaA.PublicId, u.Jefe.PublicId]);
        var abierto = await InventarioE2E.GetAsync(http, esc.Admin, $"{Conteos}/{conteo}");
        abierto.GetProperty("displayNumber").ValueKind.Should().Be(JsonValueKind.Null, "se numera al cerrar");
        var salida = await esc.AjusteAsync(http, esc.Admin, "AJN", "PV1", [new("P1", 1)], causa: "MERMA");
        await InventarioE2E.FallaAsync(await InventarioE2E.PedirConfirmarAsync(http, esc.Admin, "/api/inventory/adjustments", salida), "Inventory.Count.ProductsLocked");

        // (2) Dos contadores capturan a la vez: 5 + 5 + 7 = 17 contra 20.
        var capturas = await Task.WhenAll(
            CapturarAsync(http, u.BodegaA.Token, conteo, 1, esc.P("P1").CodigoDeBarras, 5),
            CapturarAsync(http, u.BodegaA.Token, conteo, 1, esc.P("P1").CodigoDeBarras, 5),
            CapturarAsync(http, u.Jefe.Token, conteo, 1, esc.P("P1").CodigoDeBarras, 7));
        capturas.Should().OnlyContain(c => c.StatusCode == HttpStatusCode.OK);

        // (3) Con tolerancia 0 la diferencia exige reconteo; la segunda ronda vale.
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Conteos}/{conteo}/close", new { }),
            "Inventory.Count.RecountRequired");
        (await CapturarAsync(http, u.BodegaA.Token, conteo, 2, esc.P("P1").CodigoDeBarras, 17)).StatusCode.Should().Be(HttpStatusCode.OK);
        var cerrado = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Conteos}/{conteo}/close", new { });
        var numero = cerrado.GetProperty("number").GetInt64();

        // (4) Salida posterior del mismo producto en PRIN (el conteo sólo bloquea PV1).
        var posterior = await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJN", "PRIN", [new("P1", 2)]);

        // (5) El ajuste nace en aprobación; no lo deciden quien abrió ni quien contó.
        var ajuste = await InventarioE2E.ExitoAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Conteos}/{conteo}/adjustment", new { });
        var documento = ajuste.GetProperty("documents").EnumerateArray().Single();
        documento.GetProperty("class").ToString().Should().BeOneOf("NegativeAdjustment", "11");
        var solicitud = documento.GetProperty("approvalRequestPublicId").GetGuid();
        await InventarioE2E.FallaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, u.Jefe.Token, solicitud, aprobar: true), "Approvals.SelfApprovalForbidden");
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.BodegaA.Token, solicitud, aprobar: true)).StatusCode.Should().NotBe(HttpStatusCode.OK,
            "quien contó no aprueba (sin Inventory.Counts.Approve, además, ni la ve)");
        var aprobada = await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, solicitud, aprobar: true);
        aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());

        var ajustado = await InventarioE2E.GetAsync(http, esc.Admin, $"/api/inventory/adjustments/{documento.GetProperty("documentPublicId").GetGuid()}");
        ajustado.GetProperty("status").GetInt32().Should().Be(2, "Confirmed");
        ajustado.GetProperty("operationDate").GetString().Should().Be(abierto.GetProperty("snapshotDate").GetString(), "Conteo.FechaDelAjuste = Foto");
        ajustado.GetProperty("lines")[0].GetProperty("unitCost").GetDecimal().Should().Be(1_000m, "al promedio de la fecha de la foto");
        (await esc.FisicaAsync(http, esc.Admin, "P1", "PV1")).Should().Be(17m);
        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");
        var kardex = await InventarioE2E.InformeAsync(http, esc.Admin, "kardex", $"product={esc.P("P1").Id}&warehouse={esc.Bodega("PRIN")}&from={hoy}&to={hoy}");
        kardex.Numero(kardex.Filas.Single(f => kardex.Numero(f, "Salida") == 2m), "Costo unitario").Should().Be(1_000m,
            $"el costo de la salida posterior ({posterior.GetProperty("displayNumber")}) no cambia");

        // (6) La vista de diferencias.
        var diferencias = await InventarioE2E.InformeAsync(http, esc.Admin, "count-differences", $"count={conteo}&from={hoy}&to={hoy}");
        diferencias.Filas.Should().NotBeEmpty();

        // (7) Descartar un conteo abierto no consume número.
        var descartado = await AbrirAsync(http, esc, u, "PRIN", new { scope = "Selection", productPublicIds = new[] { esc.P("P2").Id } }, []);
        (await InventarioE2E.MandarAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Conteos}/{descartado}/discard", new { reason = "Se abrió por error" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var siguiente = await AbrirAsync(http, esc, u, "PRIN", new { scope = "Selection", productPublicIds = new[] { esc.P("P3").Id } }, []);
        (await CapturarAsync(http, u.Jefe.Token, siguiente, 1, esc.P("P3").CodigoDeBarras, 3)).StatusCode.Should().Be(HttpStatusCode.OK);
        var otroCierre = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Conteos}/{siguiente}/close", new { });
        otroCierre.GetProperty("number").GetInt64().Should().Be(numero + 1, "el conteo descartado no dejó hueco");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static async Task<Guid> AbrirAsync(HttpClient http, EscenarioDeInventario esc, UsuariosDelEnsayo u, string bodega, object criterio, Guid[] contadores)
    {
        var cuerpo = JsonSerializer.SerializeToNode(criterio)!.AsObject();
        cuerpo["documentTypePublicId"] = esc.Tipos["CON"];
        cuerpo["warehousePublicId"] = esc.Bodega(bodega);
        cuerpo["kind"] = "Cyclic";
        cuerpo["blind"] = false;
        cuerpo["counterUserPublicIds"] = JsonSerializer.SerializeToNode(contadores);
        var borrador = await InventarioE2E.BorradorAsync(http, u.Jefe.Token, Conteos, cuerpo);
        var id = borrador.GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Conteos}/{id}/open", new { });
        return id;
    }

    private static Task<HttpResponseMessage> CapturarAsync(HttpClient http, string token, Guid conteo, int ronda, string codigo, decimal cantidad) =>
        InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"{Conteos}/{conteo}/captures", new
        {
            round = ronda, reads = new[] { new { barcode = codigo, quantity = cantidad, readAt = DateTime.UtcNow } },
        });

    private static async Task<Guid> UbicacionPorDefectoAsync(HttpClient http, EscenarioDeInventario esc, string bodega) =>
        (await InventarioE2E.GetAsync(http, esc.Admin, $"/api/inventory/warehouses/{esc.Bodega(bodega)}/locations")).EnumerateArray()
            .Single(l => l.GetProperty("isDefault").GetBoolean()).GetProperty("publicId").GetGuid();
}
