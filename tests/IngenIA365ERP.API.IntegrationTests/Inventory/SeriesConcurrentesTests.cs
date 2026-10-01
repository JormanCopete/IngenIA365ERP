using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T912 (feature 012, I6, US15; FR-026, T15), en el motor de <c>DB_PROVIDER</c>, en la cooperativa aislada «seriesi6» (la de ventas de I3): el
/// cerrojo incluye <c>INV_Serials</c> (exclusivo, después de los detalles). Dos recepciones simultáneas con la misma serie → una confirma y la
/// otra recibe <c>Inventory.Serial.AlreadyInStock</c>; dos ventas simultáneas de la misma serie → una sola sale. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class SeriesConcurrentesTests(CentralIdentityApiFixture fx)
{
    private const string Recepciones = "/api/inventory/purchases/receipts";
    private const string Facturas = "/api/inventory/sales/invoices";

    [Fact]
    public async Task La_misma_serie_no_entra_dos_veces_ni_sale_dos_veces_aunque_lleguen_a_la_vez()
    {
        var esc = await EscenarioDeVentas.PrepararAsync(fx, "seriesi6");
        using var http = fx.CreateClient();
        var inv = esc.Inv;
        var t = esc.Admin;
        var und = inv.P("P1").Unidad;

        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/products", InventarioE2E.Libro(("Productos",
            ["codigo", "nombre", "tipo", "categoria", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva", "conceptoRetencion", "controlaSerie"],
            [["SERIE2", "NEVERA", "Inventoriable", "GENERAL", "UND", "ABARROTES", "Taxed", "IVA19", "COMPRAS", "sí"]])));
        var producto = (await InventarioE2E.GetAsync(http, t, "/api/inventory/products?pageSize=100")).GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("code").GetString() == "SERIE2").GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/price-lists/{esc.ListaGeneral}/items", new
        {
            items = new[] { new { productPublicId = producto, unitPublicId = und, price = 1_190_000m } }, reason = "Precio de la nevera",
        });
        var proveedor = await ContabilidadE2E.CrearPersonaAsync(http, t, "ProveedorSeries");

        // (1) Dos recepciones de la misma serie, confirmadas a la vez: una entra y la otra ve la serie ya en existencia.
        object Recepcion() => new
        {
            documentTypePublicId = inv.Tipos["REC"], warehousePublicId = inv.Bodega("PV1"), supplierPersonPublicId = proveedor,
            lines = new[] { new { productPublicId = producto, unitPublicId = und, quantity = 1m, unitPrice = 900_000m, serialNumber = "SN-C1" } },
        };
        var a = EscenarioDeFacturacionElectronica.Id(await InventarioE2E.BorradorAsync(http, t, Recepciones, Recepcion()));
        var b = EscenarioDeFacturacionElectronica.Id(await InventarioE2E.BorradorAsync(http, t, Recepciones, Recepcion()));
        var entradas = await Task.WhenAll(InventarioE2E.PedirConfirmarAsync(http, t, Recepciones, a), InventarioE2E.PedirConfirmarAsync(http, t, Recepciones, b));
        var textos = await Task.WhenAll(entradas.Select(r => r.Content.ReadAsStringAsync()));
        entradas.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1, string.Join(" | ", textos));
        await InventarioE2E.FallaAsync(entradas.Single(r => r.StatusCode != HttpStatusCode.OK), "Inventory.Serial.AlreadyInStock");
        (await FisicaAsync(http, inv, producto)).Should().Be(1m);

        // (2) Dos ventas de esa serie, confirmadas a la vez: sale una sola.
        var sup = esc.Supervisor.Token;
        await EscenarioDeVentas.SesionAsync(http, sup, esc.Caja1);
        var v1 = await BorradorDeVentaAsync(http, esc, sup, producto);
        var v2 = await BorradorDeVentaAsync(http, esc, sup, producto);
        var salidas = await Task.WhenAll(
            EscenarioDeFacturacionElectronica.PedirConfirmarAsync(http, sup, Facturas, v1.Id, v1.Total),
            EscenarioDeFacturacionElectronica.PedirConfirmarAsync(http, sup, Facturas, v2.Id, v2.Total));
        var textosDeVenta = await Task.WhenAll(salidas.Select(r => r.Content.ReadAsStringAsync()));
        salidas.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1, string.Join(" | ", textosDeVenta));
        salidas.Single(r => r.StatusCode != HttpStatusCode.OK).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        textosDeVenta.Single(x => !x.Contains("\"status\":2")).Should().ContainAny("Inventory.Serial.NotInStock", "Inventory.Stock.Insufficient");
        (await FisicaAsync(http, inv, producto)).Should().Be(0m);
        (await InventarioE2E.GetAsync(http, t, $"/api/inventory/serials?productPublicId={producto}&inStock=true")).GetArrayLength().Should().Be(0);
    }

    private static async Task<(Guid Id, decimal Total)> BorradorDeVentaAsync(HttpClient http, EscenarioDeVentas esc, string token, Guid producto)
    {
        object Cuerpo(object[] pagos) => new
        {
            documentTypePublicId = esc.TipoRv, warehousePublicId = esc.Inv.Bodega("PV1"), payments = pagos,
            lines = new[] { new { productPublicId = producto, unitPublicId = esc.Inv.P("P1").Unidad, quantity = 1m, serialNumber = "SN-C1" } },
        };
        var borrador = await InventarioE2E.BorradorAsync(http, token, Facturas, Cuerpo([]));
        var id = EscenarioDeFacturacionElectronica.Id(borrador);
        var total = EscenarioDeFacturacionElectronica.AmountDue(borrador);
        await InventarioE2E.ExitoAsync(http, token, HttpMethod.Put, $"{Facturas}/{id}", Cuerpo([esc.Efectivo(total)]));
        return (id, total);
    }

    private static async Task<decimal> FisicaAsync(HttpClient http, EscenarioDeInventario inv, Guid producto)
    {
        var e = await InventarioE2E.GetAsync(http, inv.Admin, $"/api/inventory/stock/{producto}");
        var fila = e.GetProperty("byWarehouse").EnumerateArray().FirstOrDefault(w => w.GetProperty("warehouse").GetProperty("publicId").GetGuid() == inv.Bodega("PV1"));
        return fila.ValueKind == System.Text.Json.JsonValueKind.Undefined ? 0m : fila.GetProperty("physical").GetDecimal();
    }
}
