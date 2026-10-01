using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T911 (feature 012, I6, US15; quickstart §7 I6; FR-023, FR-026, FR-036 fila «Ensamble», FR-040), en el motor de <c>DB_PROVIDER</c>, en la
/// cooperativa aislada «catalogoi6» (la de ventas de I3): el Independent Test de US15 por HTTP. Atributos talla (S, M) × color (azul, rojo)
/// → 4 variantes con existencia propia; un combo A + B vendido que baja A y B con costo de venta = suma; el ensamble de 5 kits que consume
/// componentes, entra el kit al costo consumido y emite <c>AjusteInventarioAprobado</c> (<c>Ensamble</c>); la recepción de dos lotes (vencen
/// a 10 y 60 días) y la venta que usa el sugerido (el de 10), con la vista <c>kardex?lot=</c>; el lote vencido bloqueado
/// (<c>Ventas.LoteVencido = Bloquear</c>, el defecto); la serie que ya está en existencia rechazada; y el conteo por clase A. Cada escritura
/// lleva <c>Idempotency-Key</c>. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CatalogoAvanzadoTests(CentralIdentityApiFixture fx)
{
    private const string Productos = "/api/inventory/products";
    private const string Recepciones = "/api/inventory/purchases/receipts";
    private const string Facturas = "/api/inventory/sales/invoices";
    private const string Ajustes = "/api/inventory/adjustments";

    [Fact]
    public async Task Variantes_combo_ensamble_lotes_series_y_conteo_por_clase_ABC()
    {
        var esc = await EscenarioDeVentas.PrepararAsync(fx, "catalogoi6");
        using var http = fx.CreateClient();
        var inv = esc.Inv;
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;

        // Catálogo de I6 por la plantilla de productos: la plantilla CAMISA, el combo, el kit, el producto por lote y vencimiento y el
        // de serie.
        string[] encabezados = ["codigo", "nombre", "tipo", "categoria", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva",
            "conceptoRetencion", "controlaLote", "controlaSerie", "controlaVencimiento"];
        await InventarioE2E.RevisarYAplicarAsync(http, t, Productos, InventarioE2E.Libro(("Productos", encabezados,
        [
            ["CAMISA", "CAMISA BASICA", "Template", "GENERAL", "UND", "ASEO", "Taxed", "IVA19", "COMPRAS", "no", "no", "no"],
            ["COMBO1", "COMBO JABON Y P2", "Combo", "GENERAL", "UND", "ASEO", "Taxed", "IVA19", "COMPRAS", "no", "no", "no"],
            ["KIT1", "KIT DE ASEO", "Kit", "GENERAL", "UND", "ASEO", "Taxed", "IVA19", "COMPRAS", "no", "no", "no"],
            ["LOTE1", "LECHE EN POLVO", "Inventoriable", "GENERAL", "UND", "ABARROTES", "Taxed", "IVA19", "COMPRAS", "sí", "no", "sí"],
            ["SERIE1", "LICUADORA", "Inventoriable", "GENERAL", "UND", "ABARROTES", "Taxed", "IVA19", "COMPRAS", "no", "sí", "no"],
        ])));
        var ids = (await InventarioE2E.GetAsync(http, t, $"{Productos}?pageSize=100")).GetProperty("items").EnumerateArray()
            .ToDictionary(p => p.GetProperty("code").GetString()!, p => p.GetProperty("publicId").GetGuid());
        var und = inv.P("P1").Unidad;

        // (1) Atributos y variantes: 2 tallas × 2 colores → 4 variantes; generar otra vez no repite ninguna.
        var talla = await AtributoAsync(http, t, "TALLA", "Talla", ("S", "Pequeña"), ("M", "Mediana"));
        var color = await AtributoAsync(http, t, "COLOR", "Color", ("AZUL", "Azul"), ("ROJO", "Rojo"));
        object Generar() => new
        {
            attributes = new[]
            {
                new { attributePublicId = talla.Id, valuePublicIds = talla.Valores },
                new { attributePublicId = color.Id, valuePublicIds = color.Valores },
            },
        };
        var generadas = await InventarioE2E.BorradorAsync(http, t, $"{Productos}/{ids["CAMISA"]}/variants", Generar());
        var variantes = generadas.GetProperty("created").EnumerateArray().ToList();
        variantes.Should().HaveCount(4);
        variantes.Select(v => v.GetProperty("variantKey").GetString()).Should().Contain("COLOR=AZUL;TALLA=M");
        var otraVez = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Productos}/{ids["CAMISA"]}/variants", Generar());
        (await InventarioE2E.FallaAsync(otraVez, "Inventory.Variant.CombinationExists")).GetProperty("data").GetProperty("variantKeys")
            .GetArrayLength().Should().Be(4, "una combinación existente no se repite");
        (await InventarioE2E.GetAsync(http, t, $"{Productos}/{ids["CAMISA"]}/variants")).GetArrayLength().Should().Be(4);

        // Cada variante tiene su existencia.
        var azulM = variantes.Single(v => v.GetProperty("variantKey").GetString() == "COLOR=AZUL;TALLA=M").GetProperty("publicId").GetGuid();
        var rojoS = variantes.Single(v => v.GetProperty("variantKey").GetString() == "COLOR=ROJO;TALLA=S").GetProperty("publicId").GetGuid();
        var entradaDeVariante = await InventarioE2E.BorradorAsync(http, t, Ajustes, new
        {
            documentTypePublicId = inv.Tipos["AJP"], warehousePublicId = inv.Bodega("PV1"), reason = "Variante del ensayo",
            lines = new[] { new { productPublicId = azulM, unitPublicId = und, quantity = 10m, unitCost = 20_000m } },
        });
        await InventarioE2E.ConfirmarAsync(http, t, Ajustes, entradaDeVariante.GetProperty("publicId").GetGuid());
        (await FisicaAsync(http, inv, azulM, "PV1")).Should().Be(10m);
        (await FisicaAsync(http, inv, rojoS, "PV1")).Should().Be(0m, "cada variante lleva su propia existencia");

        // (2) Combo P1 + P2 vendido como una línea: baja P1 y P2, con costo de venta = 1.000 + 2.500.
        await Componentes(http, t, ids["COMBO1"], (inv.P("P1").Id, 1m), (inv.P("P2").Id, 1m));
        await Componentes(http, t, ids["KIT1"], (inv.P("P3").Id, 2m), (inv.P("P4").Id, 1m));
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/price-lists/{esc.ListaGeneral}/items", new
        {
            items = new[]
            {
                new { productPublicId = ids["COMBO1"], unitPublicId = und, price = 8_330m },
                new { productPublicId = ids["LOTE1"], unitPublicId = und, price = 11_900m },
            },
            reason = "Precios de I6",
        });
        var (p1Antes, p2Antes) = (await FisicaAsync(http, inv, inv.P("P1").Id, "PV1"), await FisicaAsync(http, inv, inv.P("P2").Id, "PV1"));
        var sup = esc.Supervisor.Token;
        await EscenarioDeVentas.SesionAsync(http, sup, esc.Caja1);
        var combo = await VentaAsync(http, esc, sup, ids["COMBO1"], 1m);
        (await FisicaAsync(http, inv, inv.P("P1").Id, "PV1")).Should().Be(p1Antes - 1m);
        (await FisicaAsync(http, inv, inv.P("P2").Id, "PV1")).Should().Be(p2Antes - 1m);
        (await CostoDelKardexAsync(inv, combo)).Should().Be(-3_500m, "el costo de venta del combo es la suma de sus componentes");

        // (3) Ensamble de 5 kits: salen 10 P3 y 5 P4 y entra el kit al costo consumido (5 × (2 × 5.000 + 1.150) = 55.750).
        var (p3Antes, p4Antes) = (await FisicaAsync(http, inv, inv.P("P3").Id, "PV1"), await FisicaAsync(http, inv, inv.P("P4").Id, "PV1"));
        var ensamble = await InventarioE2E.BorradorAsync(http, t, Ajustes, new
        {
            documentTypePublicId = inv.Tipos["ENS"], warehousePublicId = inv.Bodega("PV1"), reason = "Ensamble del ensayo",
            assembly = new { kitProductPublicId = ids["KIT1"], quantity = 5m }, lines = Array.Empty<object>(),
        });
        var idEnsamble = ensamble.GetProperty("publicId").GetGuid();
        await InventarioE2E.ConfirmarAsync(http, t, Ajustes, idEnsamble);
        (await FisicaAsync(http, inv, inv.P("P3").Id, "PV1")).Should().Be(p3Antes - 10m);
        (await FisicaAsync(http, inv, inv.P("P4").Id, "PV1")).Should().Be(p4Antes - 5m);
        var kit = await ExistenciaAsync(http, inv, ids["KIT1"], "PV1");
        kit.GetProperty("physical").GetDecimal().Should().Be(5m);
        kit.GetProperty("value").GetDecimal().Should().Be(55_750m, "el kit entra al costo de lo consumido");
        (await MensajesAsync(inv, "AjusteInventarioAprobado", idEnsamble, "Ensamble")).Should().Be(1);

        // (4) Recepción de dos lotes (vencen a 10 y 60 días) y uno ya vencido; la consulta sugiere el que vence primero.
        var proveedor = await ContabilidadE2E.CrearPersonaAsync(http, t, "ProveedorLotes");
        var recepcion = await InventarioE2E.BorradorAsync(http, t, Recepciones, new
        {
            documentTypePublicId = inv.Tipos["REC"], warehousePublicId = inv.Bodega("PV1"), supplierPersonPublicId = proveedor,
            lines = new object[]
            {
                new { productPublicId = ids["LOTE1"], unitPublicId = und, quantity = 5m, unitPrice = 6_000m, lotCode = "L60", expiryDate = hoy.AddDays(60).ToString("yyyy-MM-dd") },
                new { productPublicId = ids["LOTE1"], unitPublicId = und, quantity = 5m, unitPrice = 6_000m, lotCode = "L10", expiryDate = hoy.AddDays(10).ToString("yyyy-MM-dd") },
                new { productPublicId = ids["LOTE1"], unitPublicId = und, quantity = 2m, unitPrice = 6_000m, lotCode = "LVEN", expiryDate = hoy.AddDays(-1).ToString("yyyy-MM-dd") },
            },
        });
        await InventarioE2E.ConfirmarAsync(http, t, Recepciones, EscenarioDeFacturacionElectronicaId(recepcion));
        var lotes = (await InventarioE2E.GetAsync(http, t, $"/api/inventory/lots?productPublicId={ids["LOTE1"]}&warehousePublicId={inv.Bodega("PV1")}"))
            .EnumerateArray().ToList();
        lotes.Select(l => l.GetProperty("code").GetString()).Should().Equal(["L10", "L60"], "orden FEFO, sin el vencido");
        lotes.Single(l => l.GetProperty("suggested").GetBoolean()).GetProperty("code").GetString().Should().Be("L10");
        var conVencidos = (await InventarioE2E.GetAsync(http, t,
            $"/api/inventory/lots?productPublicId={ids["LOTE1"]}&warehousePublicId={inv.Bodega("PV1")}&includeExpired=true")).EnumerateArray().ToList();
        conVencidos.Single(l => l.GetProperty("code").GetString() == "LVEN").GetProperty("state").ToString().Should().BeOneOf("Expired", "3");

        // La venta con el sugerido sale del lote L10, y el kardex por lote la muestra.
        var conLote = await VentaAsync(http, esc, sup, ids["LOTE1"], 2m, "L10");
        var kardex = await InventarioE2E.InformeAsync(http, t, "kardex",
            $"product={ids["LOTE1"]}&warehouse={inv.Bodega("PV1")}&from={hoy:yyyy-MM-dd}&to={hoy:yyyy-MM-dd}&lot=L10");
        var movimientos = kardex.Filas.Skip(1).ToList();
        ContabilidadE2E.Tabla.Texto(kardex.Filas[0], 2).Should().Be("Saldo inicial");
        movimientos.Should().HaveCount(2, "la entrada y la venta del lote L10");
        movimientos.Should().OnlyContain(f => kardex.Texto(f, "Lote/serie") == "L10");
        kardex.Numero(movimientos[^1], "Salida").Should().Be(2m);
        kardex.Numero(movimientos[^1], "Saldo (cantidad)").Should().Be(3m, "el saldo que se muestra es el del lote");
        (await CostoDelKardexAsync(inv, conLote)).Should().Be(-12_000m);

        // (5) El lote vencido no se vende (Ventas.LoteVencido = Bloquear, el defecto).
        var vencida = await BorradorDeVentaAsync(http, esc, sup, ids["LOTE1"], 1m, "LVEN");
        if (vencida.StatusCode == HttpStatusCode.Created)
        {
            var borrador = await InventarioE2E.LeerAsync(vencida);
            var id = EscenarioDeFacturacionElectronicaId(borrador);
            var total = ElectronicInvoicing.EscenarioDeFacturacionElectronica.AmountDue(borrador);
            vencida = await InventarioE2E.MandarAsync(http, sup, HttpMethod.Put, $"{Facturas}/{id}", CuerpoDeVenta(esc, ids["LOTE1"], 1m, "LVEN", [esc.Efectivo(total)]));
            if (vencida.IsSuccessStatusCode)
                vencida = await ElectronicInvoicing.EscenarioDeFacturacionElectronica.PedirConfirmarAsync(http, sup, Facturas, id, total);
        }
        await InventarioE2E.FallaAsync(vencida, "Inventory.Lot.Expired");

        // (6) Una serie que ya está en existencia no vuelve a entrar.
        object Serie(string numero) => new
        {
            documentTypePublicId = inv.Tipos["REC"], warehousePublicId = inv.Bodega("PV1"), supplierPersonPublicId = proveedor,
            lines = new[] { new { productPublicId = ids["SERIE1"], unitPublicId = und, quantity = 1m, unitPrice = 150_000m, serialNumber = numero } },
        };
        var primera = await InventarioE2E.BorradorAsync(http, t, Recepciones, Serie("SN-0001"));
        await InventarioE2E.ConfirmarAsync(http, t, Recepciones, EscenarioDeFacturacionElectronicaId(primera));
        var repetida = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, Recepciones, Serie("SN-0001"));
        if (repetida.StatusCode == HttpStatusCode.Created)
            repetida = await InventarioE2E.PedirConfirmarAsync(http, t, Recepciones, EscenarioDeFacturacionElectronicaId(await InventarioE2E.LeerAsync(repetida)));
        await InventarioE2E.FallaAsync(repetida, "Inventory.Serial.AlreadyInStock");
        var series = (await InventarioE2E.GetAsync(http, t, $"/api/inventory/serials?productPublicId={ids["SERIE1"]}&inStock=true")).EnumerateArray().ToList();
        series.Should().ContainSingle().Which.GetProperty("serialNumber").GetString().Should().Be("SN-0001");

        // (7) El conteo por clase A: la foto trae los productos de mayor salida al costo en la bodega (P3 por el ensamble), no los menores.
        var conteo = await InventarioE2E.BorradorAsync(http, t, "/api/inventory/counts", new
        {
            documentTypePublicId = inv.Tipos["CON"], warehousePublicId = inv.Bodega("PV1"), kind = "Cyclic", scope = "AbcClass", abcClass = "A",
            blind = false,
        });
        var idConteo = conteo.GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/inventory/counts/{idConteo}/open", new { });
        var abierto = await InventarioE2E.GetAsync(http, t, $"/api/inventory/counts/{idConteo}");
        var codigos = abierto.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("product").GetProperty("code").GetString()).Distinct().ToList();
        codigos.Should().Contain("P3", "la mayor salida al costo de PV1 es clase A");
        codigos.Should().NotContain("P1", "la menor no");
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/inventory/counts/{idConteo}/discard", new { reason = "Fin del ensayo" });
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static Guid EscenarioDeFacturacionElectronicaId(JsonElement documento) => ElectronicInvoicing.EscenarioDeFacturacionElectronica.Id(documento);

    private static async Task<(Guid Id, Guid[] Valores)> AtributoAsync(HttpClient http, string t, string codigo, string nombre, params (string Codigo, string Nombre)[] valores)
    {
        var creado = await InventarioE2E.BorradorAsync(http, t, "/api/inventory/variant-attributes", new
        {
            code = codigo, name = nombre, isActive = true, values = valores.Select((v, i) => new { code = v.Codigo, name = v.Nombre, sortOrder = i + 1 }).ToList(),
        });
        return (creado.GetProperty("publicId").GetGuid(), creado.GetProperty("values").EnumerateArray().Select(v => v.GetProperty("publicId").GetGuid()).ToArray());
    }

    private static Task<JsonElement> Componentes(HttpClient http, string t, Guid producto, params (Guid Componente, decimal Cantidad)[] componentes) =>
        InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"{Productos}/{producto}/components", new
        {
            components = componentes.Select(c => new { componentProductPublicId = c.Componente, quantity = c.Cantidad }).ToList(),
        });

    private static object CuerpoDeVenta(EscenarioDeVentas esc, Guid producto, decimal cantidad, string? lote, object[] pagos) => new
    {
        documentTypePublicId = esc.TipoRv, warehousePublicId = esc.Inv.Bodega("PV1"),
        lines = new[] { new { productPublicId = producto, unitPublicId = esc.Inv.P("P1").Unidad, quantity = cantidad, lotCode = lote } },
        payments = pagos,
    };

    private static Task<HttpResponseMessage> BorradorDeVentaAsync(HttpClient http, EscenarioDeVentas esc, string token, Guid producto, decimal cantidad,
        string? lote = null) =>
        InventarioE2E.MandarAsync(http, token, HttpMethod.Post, Facturas, CuerpoDeVenta(esc, producto, cantidad, lote, []));

    /// <summary>Una venta de oficina (comprobante RV) de un producto, cobrada en efectivo; devuelve el documento confirmado.</summary>
    private static async Task<Guid> VentaAsync(HttpClient http, EscenarioDeVentas esc, string token, Guid producto, decimal cantidad, string? lote = null)
    {
        var resp = await BorradorDeVentaAsync(http, esc, token, producto, cantidad, lote);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var borrador = await InventarioE2E.LeerAsync(resp);
        var id = EscenarioDeFacturacionElectronicaId(borrador);
        var total = ElectronicInvoicing.EscenarioDeFacturacionElectronica.AmountDue(borrador);
        await InventarioE2E.ExitoAsync(http, token, HttpMethod.Put, $"{Facturas}/{id}", CuerpoDeVenta(esc, producto, cantidad, lote, [esc.Efectivo(total)]));
        var confirmada = await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"{Facturas}/{id}/confirm", new { expectedAmountDue = total });
        confirmada.GetProperty("status").GetInt32().Should().Be(2);
        return id;
    }

    private static async Task<JsonElement> ExistenciaAsync(HttpClient http, EscenarioDeInventario inv, Guid producto, string bodega)
    {
        var e = await InventarioE2E.GetAsync(http, inv.Admin, $"/api/inventory/stock/{producto}");
        return e.GetProperty("byWarehouse").EnumerateArray().FirstOrDefault(w => w.GetProperty("warehouse").GetProperty("publicId").GetGuid() == inv.Bodega(bodega));
    }

    private static async Task<decimal> FisicaAsync(HttpClient http, EscenarioDeInventario inv, Guid producto, string bodega)
    {
        var fila = await ExistenciaAsync(http, inv, producto, bodega);
        return fila.ValueKind == JsonValueKind.Undefined ? 0m : fila.GetProperty("physical").GetDecimal();
    }

    private Task<decimal> CostoDelKardexAsync(EscenarioDeInventario inv, Guid documento) =>
        InventarioE2E.EscalarEnLaCooperativaAsync(fx, inv.Coop,
            $"""SELECT COALESCE(SUM(k."TotalCost"), 0) FROM dbo."INV_KardexEntries" k JOIN dbo."INV_Documents" d ON d."Id" = k."DocumentId" WHERE d."PublicId" = '{documento}'""",
            $"SELECT COALESCE(SUM(k.[TotalCost]), 0) FROM [dbo].[INV_KardexEntries] k JOIN [dbo].[INV_Documents] d ON d.[Id] = k.[DocumentId] WHERE d.[PublicId] = '{documento}'")
        .ContinueWith(v => Convert.ToDecimal(v.Result));

    private async Task<int> MensajesAsync(EscenarioDeInventario inv, string tipo, Guid origen, string texto) =>
        Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, inv.Coop,
            $"""SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" = '{tipo}' AND "OriginPublicId" = '{origen}' AND "PayloadJson" LIKE '%{texto}%'""",
            $"SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] = '{tipo}' AND [OriginPublicId] = '{origen}' AND [PayloadJson] LIKE '%{texto}%'"));
}
