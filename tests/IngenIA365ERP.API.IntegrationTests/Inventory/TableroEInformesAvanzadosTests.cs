using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T951 (feature 012, I6, US17; quickstart §7 I6; contracts/api.md §27, §28; FR-086, FR-087, FR-088, FR-021), en el motor de <c>DB_PROVIDER</c>,
/// en la cooperativa aislada «tableroi6» (la de ventas de I3) con cifras calculadas a mano: dos P7 vendidos a 100.000 con costo 60.000
/// (margen 80.000, 40 %), P8 quieto en PRIN desde el día siguiente al corte (8 a 3.000), el lote LX de LOTE8 que vence en 10 días (4 a 5.000,
/// comprado a «ProveedorTablero»), LOTE8 en PV1 con mínimo 10, reorden 15 y máximo 50 (sugerido 46) y una merma de 2 P1 a 1.000. Las siete
/// vistas de I6 y el motivo «sin movimiento» de <c>impairment</c> en <c>json</c> y <c>xlsx</c>; <c>margin?by=customer</c> exportado sin
/// <c>Inventory.Reports.ExportPersonalData</c> → 404 (US17-3); el tablero con y sin <c>Inventory.Costs.Read</c> y con alcance parcial, y sin
/// <c>Inventory.Dashboard.View</c> → 404. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class TableroEInformesAvanzadosTests(CentralIdentityApiFixture fx)
{
    private const string Informes = "/api/reports/inventory";
    private const string Tablero = "/api/inventory/dashboard";

    [Fact]
    public async Task Las_vistas_de_I6_y_el_tablero_coinciden_con_las_cifras_a_mano_y_respetan_costos_alcance_y_datos_personales()
    {
        var esc = await EscenarioDeVentas.PrepararAsync(fx, "tableroi6");
        using var http = fx.CreateClient();
        var inv = esc.Inv;
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        var desde = inv.Corte.AddDays(1);
        var und = inv.P("P1").Unidad;
        var rango = $"from={hoy:yyyy-MM-dd}&to={hoy:yyyy-MM-dd}";

        // Datos: P8 quieto en PRIN, LOTE8 por lote, la política de reorden, la venta de P7, la merma de P1 y 20 días para «sin movimiento».
        await ContabilidadDeInventarioE2E.ParametroAsync(http, t, "Informes.DiasSinMovimiento", "20", desde);
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/products", InventarioE2E.Libro(("Productos",
            ["codigo", "nombre", "tipo", "categoria", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva", "conceptoRetencion", "controlaLote", "controlaVencimiento"],
            [
                ["P8", "PRODUCTO QUIETO", "Inventoriable", "GENERAL", "UND", "ABARROTES", "Excluded", null, "COMPRAS", "no", "no"],
                ["LOTE8", "YOGUR", "Inventoriable", "GENERAL", "UND", "ABARROTES", "Excluded", null, "COMPRAS", "sí", "sí"],
            ])));
        var ids = (await InventarioE2E.GetAsync(http, t, "/api/inventory/products?pageSize=100")).GetProperty("items").EnumerateArray()
            .ToDictionary(p => p.GetProperty("code").GetString()!, p => p.GetProperty("publicId").GetGuid());
        var quieto = await InventarioE2E.BorradorAsync(http, t, "/api/inventory/adjustments", new
        {
            documentTypePublicId = inv.Tipos["AJP"], warehousePublicId = inv.Bodega("PRIN"), reason = "Existencia quieta", operationDate = desde.ToString("yyyy-MM-dd"),
            lines = new[] { new { productPublicId = ids["P8"], unitPublicId = und, quantity = 8m, unitCost = 3_000m } },
        });
        await InventarioE2E.ConfirmarAsync(http, t, "/api/inventory/adjustments", quieto.GetProperty("publicId").GetGuid());

        var proveedor = await ContabilidadE2E.CrearPersonaAsync(http, t, "ProveedorTablero");
        var recepcion = await InventarioE2E.BorradorAsync(http, t, "/api/inventory/purchases/receipts", new
        {
            documentTypePublicId = inv.Tipos["REC"], warehousePublicId = inv.Bodega("PV1"), supplierPersonPublicId = proveedor,
            lines = new[] { new { productPublicId = ids["LOTE8"], unitPublicId = und, quantity = 4m, unitPrice = 5_000m, lotCode = "LX", expiryDate = hoy.AddDays(10).ToString("yyyy-MM-dd") } },
        });
        await InventarioE2E.ConfirmarAsync(http, t, "/api/inventory/purchases/receipts", EscenarioDeFacturacionElectronica.Id(recepcion));
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, "/api/inventory/reorder-policies", new
        {
            productPublicId = ids["LOTE8"], warehousePublicId = inv.Bodega("PV1"), minimum = 10m, maximum = 50m, reorderPoint = 15m,
        });

        var comprador = await EscenarioDeFacturacionElectronica.PersonaAsync(http, t, "Tablero", 7);
        var sup = esc.Supervisor.Token;
        await EscenarioDeVentas.SesionAsync(http, sup, esc.Caja1);
        object Venta(object[] pagos) => new
        {
            documentTypePublicId = esc.TipoRv, warehousePublicId = inv.Bodega("PV1"), counterpartyPersonPublicId = comprador, payments = pagos,
            lines = new[] { new { productPublicId = inv.P("P7").Id, unitPublicId = inv.P("P7").Unidad, quantity = 2m } },
        };
        var venta = await InventarioE2E.BorradorAsync(http, sup, "/api/inventory/sales/invoices", Venta([]));
        var idVenta = EscenarioDeFacturacionElectronica.Id(venta);
        await InventarioE2E.ExitoAsync(http, sup, HttpMethod.Put, $"/api/inventory/sales/invoices/{idVenta}", Venta([esc.Efectivo(200_000m)]));
        await InventarioE2E.ExitoAsync(http, sup, HttpMethod.Post, $"/api/inventory/sales/invoices/{idVenta}/confirm", new { expectedAmountDue = 200_000m });
        await inv.AjusteConfirmadoAsync(http, t, "AJN", "PV1", [new("P1", 2)], causa: "MERMA");

        // (1) margin por producto: 200.000 − 120.000 = 80.000 (40 %).
        var margen = await InventarioE2E.InformeAsync(http, t, "margin", $"{rango}&by=product");
        var p7 = margen.Filas.Single();
        (margen.Numero(p7, "Ventas netas"), margen.Numero(p7, "Costo de venta"), margen.Numero(p7, "Margen")).Should().Be((200_000m, 120_000m, 80_000m));
        margen.Numero(p7, "Margen (%)").Should().BeOneOf(40m, 0.4m);
        var porCliente = await InventarioE2E.InformeAsync(http, t, "margin", $"{rango}&by=customer");
        porCliente.Filas.Should().ContainSingle().Which.ToString().Should().Contain("Tablero");

        // (2) turnover por producto: el costo de venta del período y la rotación = costo ÷ inventario promedio.
        var rotacion = await InventarioE2E.InformeAsync(http, t, "turnover", $"{rango}&by=product");
        var filaP7 = rotacion.Filas.Single(f => rotacion.Numero(f, "Costo de venta del período") == 120_000m);
        var promedio = rotacion.Numero(filaP7, "Inventario promedio");
        promedio.Should().BePositive();
        rotacion.Numero(filaP7, "Rotación").Should().Be(Math.Round(120_000m / promedio, 2, MidpointRounding.AwayFromZero));

        // (3) abc por ventas: P7 es todo lo vendido, clase A.
        var abc = await InventarioE2E.InformeAsync(http, t, "abc", $"{rango}&basis=sales");
        var filaAbc = abc.Filas.Single();
        (abc.Numero(filaAbc, "Valor"), abc.Texto(filaAbc, "Clase")).Should().Be((200_000m, "A"));
        abc.Numero(filaAbc, "Participación (%)").Should().BeOneOf(100m, 1m);

        // (4) no-movement con el parámetro (20 días): P8 en PRIN, quieto desde el día siguiente al corte.
        var quietos = await InventarioE2E.InformeAsync(http, t, "no-movement", $"asOf={hoy:yyyy-MM-dd}");
        var p8 = quietos.Filas.Single(f => quietos.Texto(f, "Producto").Contains("P8"));
        (quietos.Numero(p8, "Días sin movimiento"), quietos.Numero(p8, "Cantidad"), quietos.Numero(p8, "Valor"))
            .Should().Be((hoy.DayNumber - desde.DayNumber, 8m, 24_000m));
        quietos.Filas.Should().NotContain(f => quietos.Texto(f, "Producto").Contains("P7"), "lo que se movió hoy no está quieto");

        // El mismo motivo en impairment.
        var deterioro = await InventarioE2E.InformeAsync(http, t, "impairment", $"asOf={hoy:yyyy-MM-dd}");
        deterioro.Filas.Should().Contain(f => deterioro.Texto(f, "Producto").Contains("P8") && deterioro.Texto(f, "Motivo").StartsWith("Sin movimiento"));

        // (5) expiring: el lote LX vence en 10 días (4 a 5.000).
        var porVencer = await InventarioE2E.InformeAsync(http, t, "expiring", $"asOf={hoy:yyyy-MM-dd}&days=30");
        var lx = porVencer.Filas.Single(f => porVencer.Texto(f, "Lote") == "LX");
        (porVencer.Numero(lx, "Días"), porVencer.Numero(lx, "Cantidad"), porVencer.Numero(lx, "Valor")).Should().Be((10m, 4m, 20_000m));

        // (6) purchase-suggestion: LOTE8 en PV1, posición 4, sugerido 46, último costo 5.000 y el proveedor de la recepción.
        var sugerido = await InventarioE2E.InformeAsync(http, t, "purchase-suggestion", $"warehouse={inv.Bodega("PV1")}");
        var lote8 = sugerido.Filas.Single(f => sugerido.Texto(f, "Producto").Contains("LOTE8"));
        (sugerido.Numero(lote8, "Posición"), sugerido.Numero(lote8, "Sugerido"), sugerido.Numero(lote8, "Último costo")).Should().Be((4m, 46m, 5_000m));
        sugerido.Texto(lote8, "Proveedor habitual").Should().Contain("ProveedorTablero");

        // (7) shrinkage-cap con el parámetro en 0: lo dice y no calcula tope; base = compras del año (20.000), mermas 2.000.
        var tope = await InventarioE2E.InformeAsync(http, t, "shrinkage-cap", $"year={hoy.Year}");
        var fila = tope.Filas.Single();
        (tope.Numero(fila, "Compras del año"), tope.Numero(fila, "Faltantes y mermas")).Should().Be((20_000m, 2_000m));
        tope.Notas.Should().Contain(n => n.Contains("no está parametrizado"));

        // (8) Las ocho en xlsx; la del margen trae la cifra.
        foreach (var consulta in new[]
                 {
                     $"margin?{rango}&by=product", $"turnover?{rango}&by=product", $"abc?{rango}&basis=sales", $"no-movement?asOf={hoy:yyyy-MM-dd}",
                     $"expiring?asOf={hoy:yyyy-MM-dd}&days=30", $"purchase-suggestion?warehouse={inv.Bodega("PV1")}", $"shrinkage-cap?year={hoy.Year}",
                     $"impairment?asOf={hoy:yyyy-MM-dd}",
                 })
        {
            var archivo = await ArchivoAsync(http, t, $"{Informes}/{consulta}&format=xlsx");
            archivo.StatusCode.Should().Be(HttpStatusCode.OK, $"{consulta}: {await archivo.Content.ReadAsStringAsync()}");
            archivo.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheetml", consulta);
            if (consulta.StartsWith("margin"))
            {
                using var libro = new XLWorkbook(new MemoryStream(await archivo.Content.ReadAsByteArrayAsync()));
                libro.Worksheets.First().CellsUsed().Should().Contain(c => c.Value.IsNumber && c.Value.GetNumber() == 80_000d, "el margen de P7 en el archivo");
            }
        }

        // (9) Datos personales (US17-3): margin?by=customer se consulta, pero exportarlo exige Inventory.Reports.ExportPersonalData.
        await InventarioE2E.RolAsync(http, inv.Coop, "ANALISTAI6", null,
            extra: ["Inventory.Reports.View", "Inventory.Reports.Export", "Inventory.Costs.Read", "Inventory.Dashboard.View", "Inventory.Stock.View"]);
        var (analista, idAnalista) = await UsuarioSoloConAsync(http, inv, "ANALISTAI6", $"analista.{inv.Coop.TenantPublicId:N}@coop.tablero.test");
        await InventarioE2E.AlcanceAsync(http, t, idAnalista, inv.Bodega("PV1"), inv.Bodega("PRIN"));
        (await ArchivoAsync(http, analista, $"{Informes}/margin?{rango}&by=product&format=xlsx")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await InventarioE2E.MandarAsync(http, analista, HttpMethod.Get, $"{Informes}/margin?{rango}&by=customer&format=json")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ArchivoAsync(http, analista, $"{Informes}/margin?{rango}&by=customer&format=xlsx")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "exportar datos de clientes sin ExportPersonalData es indistinguible de inexistente");
        (await ArchivoAsync(http, t, $"{Informes}/margin?{rango}&by=customer&format=xlsx")).StatusCode.Should().Be(HttpStatusCode.OK);

        // (10) El tablero: con costos, las fichas de valor; sin Costs.Read no salen; el alcance recorta; sin Dashboard.View, 404.
        var completo = await InventarioE2E.GetAsync(http, t, Tablero);
        var fichas = Fichas(completo);
        fichas.Keys.Should().Contain(["inventoryValue", "turnover", "inventoryDays", "grossMargin", "salesToday", "salesMonth", "belowReorder", "expiringSoon"]);
        fichas["salesToday"].GetProperty("value").GetDecimal().Should().Be(200_000m);
        fichas["belowReorder"].GetProperty("value").GetDecimal().Should().BeGreaterThanOrEqualTo(1m, "LOTE8 está bajo su punto de reorden");
        fichas["expiringSoon"].GetProperty("value").GetDecimal().Should().BeGreaterThanOrEqualTo(1m, "el lote LX");
        fichas.Values.Should().OnlyContain(f => f.GetProperty("link").ValueKind == JsonValueKind.Object);

        await InventarioE2E.RolAsync(http, inv.Coop, "TABLEROI6", null, extra: ["Inventory.Dashboard.View", "Inventory.Stock.View"]);
        var (sinCostos, idSinCostos) = await UsuarioSoloConAsync(http, inv, "TABLEROI6", $"tablero.{inv.Coop.TenantPublicId:N}@coop.tablero.test");
        await InventarioE2E.AlcanceAsync(http, t, idSinCostos, inv.Bodega("PRIN"));
        var recortado = Fichas(await InventarioE2E.GetAsync(http, sinCostos, Tablero));
        recortado.Keys.Should().NotContain(["inventoryValue", "turnover", "inventoryDays", "grossMargin"], "sin Inventory.Costs.Read no hay fichas de valor");
        if (recortado.TryGetValue("salesToday", out var ventasHoy))
            ventasHoy.GetProperty("value").GetDecimal().Should().Be(0m, "la venta fue en PV1, fuera de su alcance");
        if (recortado.TryGetValue("expiringSoon", out var vencen))
            vencen.GetProperty("value").GetDecimal().Should().Be(0m, "el lote está en PV1");

        await InventarioE2E.RolAsync(http, inv.Coop, "SINTABLERO", null, extra: ["Inventory.Stock.View"]);
        var (sinTablero, _) = await UsuarioSoloConAsync(http, inv, "SINTABLERO", $"sintablero.{inv.Coop.TenantPublicId:N}@coop.tablero.test");
        (await InventarioE2E.MandarAsync(http, sinTablero, HttpMethod.Get, Tablero)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static Dictionary<string, JsonElement> Fichas(JsonElement tablero) =>
        tablero.GetProperty("tiles").EnumerateArray().ToDictionary(f => f.GetProperty("key").GetString()!, f => f);

    private static Task<HttpResponseMessage> ArchivoAsync(HttpClient http, string token, string url)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Get, url);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(peticion);
    }

    /// <summary>Un usuario con sólo el rol propio: la invitación le da un rol integrado que ve todo <c>*.View</c>, y aquí se le quita.</summary>
    private async Task<(string Token, Guid Id)> UsuarioSoloConAsync(HttpClient http, EscenarioDeInventario inv, string rol, string correo)
    {
        var (token, usuario) = await InventarioE2E.UsuarioAsync(fx, http, inv.Coop, correo, rol);
        var detalle = await InventarioE2E.GetAsync(http, inv.Admin, $"/api/admin/users/{usuario}");
        foreach (var r in detalle.GetProperty("roles").EnumerateArray().Where(r => r.GetProperty("code").GetString() != rol))
        {
            var quitar = await InventarioE2E.EnviarAsync(http, inv.Admin, HttpMethod.Delete, $"/api/admin/users/{usuario}/roles/{r.GetProperty("publicId").GetGuid()}", null);
            quitar.IsSuccessStatusCode.Should().BeTrue(await quitar.Content.ReadAsStringAsync());
        }
        return (token, usuario);
    }
}
