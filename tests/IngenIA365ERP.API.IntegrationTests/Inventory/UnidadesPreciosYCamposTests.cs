using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// 2026-11-15 (pedido del dueño tras el 07/11 de COOFLOPAL), por HTTP en una cooperativa aislada: el producto sin unidad toma la de la
/// empresa; sin «Productos.VariasUnidades = Permitir» no hay alternas; el precio acompaña a la unidad base cuando cambia y deja su historia;
/// la pantalla de precios busca desde tres caracteres; y los campos adicionales de venta se definen y no admiten un código repetido.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class UnidadesPreciosYCamposTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_precio_acompana_a_la_unidad_y_queda_en_la_historia()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "Unidades y precios");
        using var http = fx.CreateClient();
        var t = coop.TokenAdmin;

        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/accounting-groups",
            InventarioE2E.Libro(("Datos", ["codigo", "nombre"], [["LUBRIC", "Lubricantes"]])));
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/units",
            InventarioE2E.Libro(("Datos", ["codigo", "nombre", "decimales", "codigoDian"], [["CTO", "Cuarto", "0", "QT"]])));
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/product-categories",
            InventarioE2E.Libro(("Datos", ["codigo", "nombre", "padre"], [["ACEITES", "Aceites", null]])));
        // Unidad base vacía: la de la empresa (UND).
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/products", InventarioE2E.Libro(
            ("Productos", ["codigo", "nombre", "tipo", "categoria", "marca", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva", "conceptoRetencion"],
                [["AC01", "ACEITE HELIX 20W50", "Inventoriable", "ACEITES", null, null, "LUBRIC", "Excluded", null, "COMPRAS"]])));

        var lista = (await InventarioE2E.GetAsync(http, t, "/api/inventory/products?pageSize=10")).GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("code").GetString() == "AC01");
        var producto = lista.GetProperty("publicId").GetGuid();
        var detalle = await InventarioE2E.GetAsync(http, t, $"/api/inventory/products/{producto}");
        detalle.GetProperty("baseUnit").GetProperty("code").GetString().Should().Be("UND", "sin unidad, la de Productos.UnidadBasePorDefecto");

        // Sin permitir varias unidades, no hay alternas.
        var unidades = (await InventarioE2E.GetAsync(http, t, "/api/inventory/units")).EnumerateArray().ToList();
        Guid Unidad(string codigo) => unidades.Single(u => u.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();
        var alterna = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/inventory/products/{producto}/units",
            new { unitPublicId = Unidad("CTO"), factor = 4m, usage = "Both" });
        alterna.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await InventarioE2E.CodigoDeErrorAsync(alterna)).Should().Be("Inventory.ProductUnit.NotAllowed");

        // Precio en UND en la lista general.
        var listaGeneral = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/price-lists",
            new { code = "GENERAL", name = "Lista general", includesTaxes = true, validFrom = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"), isActive = true, reason = "Lista del ensayo" });
        var listaId = listaGeneral.GetProperty("priceListPublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/price-lists/{listaId}/items",
            new { items = new[] { new { productPublicId = producto, unitPublicId = Unidad("UND"), price = 25100m } }, reason = "Precio inicial" });

        // La unidad base pasa a CTO: el precio va con ella.
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/products/{producto}", new
        {
            name = "ACEITE HELIX 20W50", categoryPublicId = detalle.GetProperty("category").GetProperty("publicId").GetGuid(),
            baseUnitPublicId = Unidad("CTO"), accountingGroupPublicId = detalle.GetProperty("accountingGroup").GetProperty("publicId").GetGuid(),
            vatSaleTreatment = "Excluded", withholdingConceptPublicId = detalle.GetProperty("withholdingConcept").GetProperty("publicId").GetGuid(),
            tracksLot = false, tracksSerial = false, tracksExpiry = false,
        });

        var ventana = await InventarioE2E.GetAsync(http, t, $"/api/inventory/price-lists/{listaId}/products/{producto}");
        var precios = ventana.GetProperty("units").EnumerateArray().ToList();
        precios.Should().ContainSingle("la unidad vieja no queda huérfana");
        precios[0].GetProperty("unitCode").GetString().Should().Be("CTO");
        precios[0].GetProperty("price").GetDecimal().Should().Be(25100m);
        ventana.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("reason").GetString())
            .Should().Equal("Cambio de unidad base del producto", "Precio inicial");

        // El buscador desde tres caracteres.
        var busqueda = await InventarioE2E.GetAsync(http, t, $"/api/inventory/price-lists/{listaId}/products?q=helix");
        busqueda.GetProperty("items").EnumerateArray().Single().GetProperty("basePrice").GetDecimal().Should().Be(25100m);
    }

    [Fact]
    public async Task Los_campos_adicionales_se_definen_con_codigo_unico()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "Campos de venta");
        using var http = fx.CreateClient();
        var t = coop.TokenAdmin;
        object Placa(string nombre) => new
        {
            code = "PLACA", name = nombre, dataType = "Text", maxLength = 7, isRequired = true, showInSales = true, showInQuotes = true,
            printOnInvoice = true, sortOrder = 1, isActive = true,
        };

        var creado = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/document-fields", Placa("Placa"));
        var repetido = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/inventory/document-fields", Placa("Otra"));
        var lista = await InventarioE2E.GetAsync(http, t, "/api/inventory/document-fields?activeOnly=true");

        creado.GetProperty("code").GetString().Should().Be("PLACA");
        repetido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        lista.EnumerateArray().Should().ContainSingle(c => c.GetProperty("name").GetString() == "Placa");
    }
}
