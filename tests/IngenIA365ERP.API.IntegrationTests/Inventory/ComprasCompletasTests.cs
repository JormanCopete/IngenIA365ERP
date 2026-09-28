using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Security;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T774 (feature 012, I5; quickstart §7; US13-1 a US13-3; FR-046, FR-048, FR-049, FR-050, FR-075; api.md §14.9, §27), en el motor de
/// <c>DB_PROVIDER</c>, sobre el escenario aislado «comprasi5» con <c>Compras.ToleranciaPrecioPorcentaje = 0.01</c> y
/// <c>Compras.ReglaDeTolerancia = AmbasCondiciones</c>: la prueba independiente de US13 de punta a punta por HTTP, cada escritura con su
/// <c>Idempotency-Key</c>. Solicitud → orden de 100 aprobada por la política del tipo → su PDF → recepciones de 60 y 38 contra la orden →
/// factura de 60 y 40 a un precio 2 % mayor, retenida (precio en las dos líneas y, en la segunda, cantidad) y visible en el cruce y en la
/// vista <c>purchase-matches</c> → rechazo, corrección a 38 y segundo intento retenido sólo por precio → las dos aprobaciones desde
/// <c>/api/inventory/approvals/{id}/decide</c> → la factura confirmada con la diferencia de precio partida en existencia y vendido y un
/// <c>AjusteDeCostoReconocido</c> por recepción. Aparte, un flete de $100.000 repartido por valor sobre recepciones de $600.000 y $400.000
/// deja $60.000 y $40.000 en <c>INV_LandedCostAllocations</c> y un ajuste por recepción. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ComprasCompletasTests(CentralIdentityApiFixture fx)
{
    private const string Compras = "/api/inventory/purchases";
    private static int _numero = 5000;

    private async Task<EscenarioDeInventario> EscenarioAsync()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "comprasi5");
        using var http = fx.CreateClient();
        await Cerrojo.WaitAsync();
        try
        {
            if (Parametrizados.Add(esc))
            {
                var desde = esc.Corte.AddDays(1).ToString("yyyy-MM-dd");
                await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/parameters/INV/Compras.ToleranciaPrecioPorcentaje/versions",
                    new { scopeKind = "None", value = "0.01", validFrom = desde, reason = "Tolerancia de precio del ensayo" });
                await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/parameters/INV/Compras.ReglaDeTolerancia/versions",
                    new { scopeKind = "None", value = "AmbasCondiciones", validFrom = desde, reason = "Regla de tolerancia del ensayo" });
            }
        }
        finally
        {
            Cerrojo.Release();
        }
        return esc;
    }

    private static readonly HashSet<EscenarioDeInventario> Parametrizados = [];
    private static readonly SemaphoreSlim Cerrojo = new(1, 1);

    [Fact]
    public async Task Solicitud_orden_recepciones_y_factura_retenida_que_se_aprueba_y_confirma_con_la_diferencia_partida()
    {
        var esc = await EscenarioAsync();
        var usuarios = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        var proveedor = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, t, "ProveedorOrden");
        var p1 = esc.P("P1");
        esc.Tipos.Should().ContainKeys(["SOC", "ORC", "CAD"], "la semilla deja los tipos de I5 cuando la entrega vigente llega a I5");

        // ------------------------------------------------------------------------------------------ solicitud (FR-036) --
        var solicitud = await InventarioE2E.BorradorAsync(http, t, $"{Compras}/requests", new
        {
            documentTypePublicId = esc.Tipos["SOC"], warehousePublicId = esc.Bodega("PRIN"), neededBy = hoy.AddDays(15).ToString("yyyy-MM-dd"),
            lines = new[] { new { productPublicId = p1.Id, unitPublicId = p1.Unidad, quantity = 100m } },
        });
        var idSolicitud = solicitud.GetProperty("publicId").GetGuid();
        (await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/requests", idSolicitud)).GetProperty("status").GetInt32().Should().Be(2, "Confirmed");
        var lineaSolicitada = LineaUnica(await InventarioE2E.GetAsync(http, t, $"{Compras}/requests/{idSolicitud}"));

        // ------------------------------------------------------------------------ orden aprobada por la política (FR-048) --
        await InventarioE2E.PoliticaAsync(http, t, esc.Tipos["ORC"], esc.Corte.AddDays(1), (50_000m, "Inventory.Purchases.Approve"));
        var orden = await InventarioE2E.BorradorAsync(http, t, $"{Compras}/orders", new
        {
            documentTypePublicId = esc.Tipos["ORC"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor,
            expectedDate = hoy.AddDays(10).ToString("yyyy-MM-dd"), paymentTerms = "30 días",
            lines = new[] { new { productPublicId = p1.Id, unitPublicId = p1.Unidad, quantity = 100m, unitPrice = 1_000m, requestLinePublicId = lineaSolicitada } },
        });
        var idOrden = orden.GetProperty("publicId").GetGuid();
        var enviada = await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/orders", idOrden);
        enviada.GetProperty("status").GetInt32().Should().Be(1, "PendingApproval: supera el umbral de la política del tipo ORC");
        (await InventarioE2E.DecidirAsync(http, t, usuarios.Aprobador.Token, enviada.GetProperty("approval").GetProperty("requestPublicId").GetGuid(), aprobar: true))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var ordenConfirmada = await InventarioE2E.GetAsync(http, t, $"{Compras}/orders/{idOrden}");
        Documento(ordenConfirmada).GetProperty("status").GetInt32().Should().Be(2, "Confirmed tras la aprobación");
        var lineaDeOrden = LineaUnica(ordenConfirmada);

        var pdf = await InventarioE2E.MandarAsync(http, t, HttpMethod.Get, $"{Compras}/orders/{idOrden}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync());
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");

        // ----------------------------------------------------------------------------- dos recepciones contra la orden (FR-049) --
        var recepcion1 = await RecepcionContraOrdenAsync(http, esc, proveedor, lineaDeOrden, 60m, "REM-60");
        var recepcion2 = await RecepcionContraOrdenAsync(http, esc, proveedor, lineaDeOrden, 38m, "REM-38");
        var pendiente = await InventarioE2E.GetAsync(http, t, $"{Compras}/orders/{idOrden}");
        pendiente.GetProperty("pendingLines")[0].GetProperty("pendingToReceive").GetDecimal().Should().Be(2m, "100 − 60 − 38, calculado desde los vínculos");

        // Salen 68 antes de facturar: quedan 30 de los 98 recibidos (la diferencia de precio se parte en existencia y vendido, D5).
        await esc.AjusteConfirmadoAsync(http, t, "AJN", "PRIN", [new("P1", 68)]);

        // ------------------------------------------------------------- factura 2 % sobre el precio y 40 sobre 38: retenida --
        var l1 = LineaUnica(await InventarioE2E.GetAsync(http, t, $"{Compras}/receipts/{recepcion1}"));
        var l2 = LineaUnica(await InventarioE2E.GetAsync(http, t, $"{Compras}/receipts/{recepcion2}"));
        var borrador = await InventarioE2E.BorradorAsync(http, t, $"{Compras}/supplier-invoices", new
        {
            documentTypePublicId = esc.Tipos["FCP"], supplierPersonPublicId = proveedor,
            supplier = new { prefix = "FV", number = Interlocked.Increment(ref _numero).ToString(), issueDate = hoy.ToString("yyyy-MM-dd"), paymentForm = "Cash", isElectronic = false },
            lines = new object[]
            {
                new { productPublicId = p1.Id, unitPublicId = p1.Unidad, quantity = 60m, unitPrice = 1_020m, receiptLinePublicId = l1 },
                new { productPublicId = p1.Id, unitPublicId = p1.Unidad, quantity = 40m, unitPrice = 1_020m, receiptLinePublicId = l2 },
            },
        });
        var factura = borrador.GetProperty("publicId").GetGuid();
        var mensajesAntes = await ContarMensajesAsync(esc, "AjusteDeCostoReconocido");

        var retenida = await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/supplier-invoices", factura);
        retenida.GetProperty("status").GetInt32().Should().Be(1, "PendingApproval: las dos líneas exceden la tolerancia");
        retenida.GetProperty("number").ValueKind.Should().Be(JsonValueKind.Null, "en aprobación no consume número");

        var cruce = Lineas(await InventarioE2E.GetAsync(http, t, $"{Compras}/supplier-invoices/{factura}/match"));
        cruce.Should().HaveCount(2).And.OnlyContain(l => l.GetProperty("status").ToString() == "1" && l.GetProperty("exceedsTolerance").GetBoolean());
        Razones(cruce[0]).Should().Equal("Price");
        Razones(cruce[1]).Should().Equal("Quantity", "Price");
        cruce[1].GetProperty("quantityVariance").GetDecimal().Should().Be(2m);
        cruce[0].GetProperty("priceVariance").GetDecimal().Should().Be(1_200m);
        cruce[0].GetProperty("tolerance").GetString().Should().Contain("Compras.ToleranciaPrecioPorcentaje");
        (await InventarioE2E.GetAsync(http, t, $"{Compras}/matches?status=Held&supplierPersonPublicId={proveedor}"))
            .GetProperty("items").GetArrayLength().Should().Be(2);

        var desde = hoy.AddDays(-1).ToString("yyyy-MM-dd");
        var hasta = hoy.ToString("yyyy-MM-dd");
        var vista = await InventarioE2E.InformeAsync(http, t, "purchase-matches", $"from={desde}&to={hasta}&supplier={proveedor}");
        vista.Filas.Should().HaveCount(2);
        vista.Filas.Should().OnlyContain(f => vista.Texto(f, "Estado") == "Retenida" && vista.Texto(f, "Dentro de tolerancia") == "No");
        (await FilasDelCruceAsync(esc, factura, vivas: true)).Should().Be(2);

        // La retenida por cantidad no se aprueba por excepción (T796, T42a): se rechaza, vuelve a borrador y se corrige a 38.
        var porCantidad = cruce[1].GetProperty("approvalRequestPublicId").GetGuid();
        var noAprobable = await InventarioE2E.DecidirAsync(http, t, usuarios.Aprobador.Token, porCantidad, aprobar: true);
        await InventarioE2E.FallaAsync(noAprobable, "Inventory.PurchaseMatch.QuantityNotApprovable");
        (await InventarioE2E.DecidirAsync(http, t, usuarios.Aprobador.Token, porCantidad, aprobar: false, motivo: "Se facturaron 40 y llegaron 38"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var devuelta = Documento(await InventarioE2E.GetAsync(http, t, $"{Compras}/supplier-invoices/{factura}"));
        devuelta.GetProperty("status").GetInt32().Should().Be(0, "rechazada una línea, la factura vuelve a borrador");

        var lineas = devuelta.GetProperty("lines").EnumerateArray().OrderBy(l => l.GetProperty("lineNumber").GetInt32()).ToList();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"{Compras}/supplier-invoices/{factura}", new
        {
            documentTypePublicId = esc.Tipos["FCP"], supplierPersonPublicId = proveedor, rowVersion = devuelta.GetProperty("rowVersion").GetString(),
            supplier = new { prefix = "FV", number = Interlocked.Increment(ref _numero).ToString(), issueDate = hoy.ToString("yyyy-MM-dd"), paymentForm = "Cash", isElectronic = false },
            lines = new object[]
            {
                new { linePublicId = lineas[0].GetProperty("linePublicId").GetGuid(), productPublicId = p1.Id, unitPublicId = p1.Unidad, quantity = 60m, unitPrice = 1_020m, receiptLinePublicId = l1 },
                new { linePublicId = lineas[1].GetProperty("linePublicId").GetGuid(), productPublicId = p1.Id, unitPublicId = p1.Unidad, quantity = 38m, unitPrice = 1_020m, receiptLinePublicId = l2 },
            },
        });
        var segunda = await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/supplier-invoices", factura);
        segunda.GetProperty("status").GetInt32().Should().Be(1, "sigue retenida, ahora sólo por precio");
        var cruce2 = Lineas(await InventarioE2E.GetAsync(http, t, $"{Compras}/supplier-invoices/{factura}/match"));
        cruce2.Should().HaveCount(2).And.OnlyContain(l => Razones(l).SequenceEqual(new[] { "Price" }));
        (await FilasDelCruceAsync(esc, factura, vivas: true)).Should().Be(2, "el segundo intento da de baja lógica las filas del primero");
        (await FilasDelCruceAsync(esc, factura, vivas: false)).Should().Be(2);

        // -------------------------------------------------------- las dos aprobaciones: la última confirma la factura --
        foreach (var linea in cruce2)
        {
            var decision = await InventarioE2E.DecidirAsync(http, t, usuarios.Aprobador.Token, linea.GetProperty("approvalRequestPublicId").GetGuid(),
                aprobar: true, motivo: "Alza del proveedor aceptada");
            decision.StatusCode.Should().Be(HttpStatusCode.OK, await decision.Content.ReadAsStringAsync());
        }
        var confirmada = Documento(await InventarioE2E.GetAsync(http, t, $"{Compras}/supplier-invoices/{factura}"));
        confirmada.GetProperty("status").GetInt32().Should().Be(2, "aprobada la última, se confirma en la transacción del aprobador");
        confirmada.GetProperty("number").ValueKind.Should().Be(JsonValueKind.Number);
        Lineas(await InventarioE2E.GetAsync(http, t, $"{Compras}/supplier-invoices/{factura}/match"))
            .Should().OnlyContain(l => l.GetProperty("status").ToString() == "2", "Approved");

        var kardex = await InventarioE2E.InformeAsync(http, t, "kardex", $"product={p1.Id}&warehouse={esc.Bodega("PRIN")}&from={desde}&to={hasta}");
        kardex.Filas.Should().Contain(f => kardex.Texto(f, "Motivo") == "Ajuste de costo: diferencia de precio");
        kardex.Numero(kardex.Filas[^1], "Saldo (cantidad)").Should().Be(30m);
        kardex.Numero(kardex.Filas[^1], "Saldo (valor)").Should().Be(31_200m,
            "30 × 1.000 más la parte en existencia de la diferencia: 60 × 20 × 30/60 + 38 × 20 × 30/38; lo vendido va al mensaje");
        (await ContarMensajesAsync(esc, "AjusteDeCostoReconocido") - mensajesAntes).Should().Be(2, "uno por recepción afectada");
        (await InventarioE2E.InformeAsync(http, t, "purchase-matches", $"from={desde}&to={hasta}&supplier={proveedor}"))
            .Filas.Should().HaveCount(2);
    }

    [Fact]
    public async Task Un_flete_de_100000_por_valor_reparte_60000_y_40000_y_deja_un_ajuste_por_recepcion()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        var proveedor = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, t, "ProveedorMercancia");
        var transportador = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, t, "Transportador");
        var flete = await ServicioAsync(http, esc);

        var a = await RecepcionAsync(http, esc, proveedor, 600m, "REM-600");
        var b = await RecepcionAsync(http, esc, proveedor, 400m, "REM-400");
        var facturaDelFlete = await InventarioE2E.BorradorAsync(http, t, $"{Compras}/supplier-invoices", new
        {
            documentTypePublicId = esc.Tipos["FCP"], supplierPersonPublicId = transportador,
            supplier = new { prefix = "FL", number = Interlocked.Increment(ref _numero).ToString(), issueDate = hoy.ToString("yyyy-MM-dd"), paymentForm = "Cash", isElectronic = false },
            lines = new[] { new { productPublicId = flete.Id, unitPublicId = flete.Unidad, quantity = 1m, unitPrice = 100_000m } },
        });
        var idFlete = facturaDelFlete.GetProperty("publicId").GetGuid();
        await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/supplier-invoices", idFlete);
        var mensajesAntes = await ContarMensajesAsync(esc, "AjusteDeCostoReconocido");

        var costos = await InventarioE2E.BorradorAsync(http, t, $"{Compras}/landed-costs", new
        {
            documentTypePublicId = esc.Tipos["CAD"], supplierInvoicePublicId = idFlete, receiptPublicIds = new[] { a, b }, amount = 100_000m, method = "Value",
            lines = Array.Empty<object>(),
        });
        var reparto = CostosAdicionales(costos);
        reparto.GetProperty("amount").GetDecimal().Should().Be(100_000m);
        reparto.GetProperty("allocations").EnumerateArray().Select(x => x.GetProperty("allocated").GetDecimal()).Should().Equal(60_000m, 40_000m);
        reparto.GetProperty("roundingResidue").GetDecimal().Should().Be(0m);

        var id = costos.GetProperty("publicId").GetGuid();
        (await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/landed-costs", id)).GetProperty("status").GetInt32().Should().Be(2);
        var confirmado = CostosAdicionales(await InventarioE2E.GetAsync(http, t, $"{Compras}/landed-costs/{id}"));
        confirmado.GetProperty("allocations").EnumerateArray().Select(x => x.GetProperty("toInventory").GetDecimal()).Should().Equal(60_000m, 40_000m);

        (await EnteroAsync(esc,
            $"""SELECT COUNT(*) FROM dbo."INV_LandedCostAllocations" a JOIN dbo."INV_Documents" d ON d."Id" = a."DocumentId" WHERE d."PublicId" = '{id}' AND a."IsDeleted" = false""",
            $"SELECT COUNT(*) FROM [dbo].[INV_LandedCostAllocations] a JOIN [dbo].[INV_Documents] d ON d.[Id] = a.[DocumentId] WHERE d.[PublicId] = '{id}' AND a.[IsDeleted] = 0"))
            .Should().Be(2);
        (await ContarMensajesAsync(esc, "AjusteDeCostoReconocido") - mensajesAntes).Should().Be(2, "uno por recepción afectada");

        var dia = hoy.ToString("yyyy-MM-dd");
        var kardex = await InventarioE2E.InformeAsync(http, t, "kardex", $"product={esc.P("P2").Id}&warehouse={esc.Bodega("PRIN")}&from={dia}&to={dia}");
        kardex.Filas.Count(f => kardex.Texto(f, "Motivo") == "Ajuste de costo: costos adicionales").Should().BeGreaterThanOrEqualTo(2);
        kardex.Numero(kardex.Filas[^1], "Saldo (valor)").Should().Be(1_100_000m, "1.000 × 1.000 más el flete completo, todo sigue en existencia");

        // Anular una recepción con costos adicionales vigentes: primero los costos (FR-075).
        var anular = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Compras}/receipts/{a}/void", new { reason = "Error" });
        await InventarioE2E.FallaAsync(anular, "Inventory.Document.HasDependents");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static JsonElement Documento(JsonElement detalle) => detalle.TryGetProperty("document", out var d) ? d : detalle;

    private static Guid LineaUnica(JsonElement detalle) =>
        Documento(detalle).GetProperty("lines").EnumerateArray().Single().GetProperty("linePublicId").GetGuid();

    private static JsonElement CostosAdicionales(JsonElement cuerpo) =>
        cuerpo.TryGetProperty("landedCost", out var lc) && lc.ValueKind == JsonValueKind.Object ? lc : Documento(cuerpo).GetProperty("landedCost");

    private static List<JsonElement> Lineas(JsonElement cuerpo)
    {
        var arreglo = cuerpo.ValueKind == JsonValueKind.Array ? cuerpo
            : cuerpo.TryGetProperty("lines", out var l) ? l : cuerpo.GetProperty("items");
        return arreglo.EnumerateArray().OrderBy(x => x.GetProperty("lineNumber").GetInt32()).ToList();
    }

    private static IEnumerable<string> Razones(JsonElement linea) => linea.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()!);

    private static async Task<Guid> RecepcionContraOrdenAsync(HttpClient http, EscenarioDeInventario esc, Guid proveedor, Guid lineaDeOrden, decimal cantidad, string remision)
    {
        var p1 = esc.P("P1");
        var recepcion = await InventarioE2E.BorradorAsync(http, esc.Admin, $"{Compras}/receipts", new
        {
            documentTypePublicId = esc.Tipos["REC"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor, externalReference = remision,
            lines = new[] { new { productPublicId = p1.Id, unitPublicId = p1.Unidad, quantity = cantidad, unitPrice = 1_000m, orderLinePublicId = lineaDeOrden } },
        });
        var id = recepcion.GetProperty("publicId").GetGuid();
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, $"{Compras}/receipts", id);
        return id;
    }

    private static async Task<Guid> RecepcionAsync(HttpClient http, EscenarioDeInventario esc, Guid proveedor, decimal cantidad, string remision)
    {
        var p2 = esc.P("P2");
        var recepcion = await InventarioE2E.BorradorAsync(http, esc.Admin, $"{Compras}/receipts", new
        {
            documentTypePublicId = esc.Tipos["REC"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor, externalReference = remision,
            lines = new[] { new { productPublicId = p2.Id, unitPublicId = p2.Unidad, quantity = cantidad, unitPrice = 1_000m } },
        });
        var id = recepcion.GetProperty("publicId").GetGuid();
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, $"{Compras}/receipts", id);
        return id;
    }

    private static readonly SemaphoreSlim CerrojoDelServicio = new(1, 1);

    /// <summary>El servicio FLETE (excluido de IVA) por la plantilla de productos; uno por escenario.</summary>
    private static async Task<EscenarioDeInventario.Producto> ServicioAsync(HttpClient http, EscenarioDeInventario esc)
    {
        await CerrojoDelServicio.WaitAsync();
        try
        {
            var lista = (await InventarioE2E.GetAsync(http, esc.Admin, "/api/inventory/products?pageSize=50")).GetProperty("items").EnumerateArray().ToList();
            if (!lista.Any(p => p.GetProperty("code").GetString() == "FLETE"))
            {
                string[] encabezados = ["codigo", "nombre", "tipo", "categoria", "marca", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva", "conceptoRetencion"];
                await InventarioE2E.RevisarYAplicarAsync(http, esc.Admin, "/api/inventory/products", InventarioE2E.Libro(
                    ("Productos", encabezados, [["FLETE", "FLETE DE MERCANCIA", "Service", "GENERAL", null, "UND", "ABARROTES", "Excluded", null, "COMPRAS"]])));
                lista = (await InventarioE2E.GetAsync(http, esc.Admin, "/api/inventory/products?pageSize=50")).GetProperty("items").EnumerateArray().ToList();
            }
            var id = lista.Single(p => p.GetProperty("code").GetString() == "FLETE").GetProperty("publicId").GetGuid();
            var unidad = (await InventarioE2E.GetAsync(http, esc.Admin, $"/api/inventory/products/{id}")).GetProperty("baseUnit").GetProperty("publicId").GetGuid();
            return new EscenarioDeInventario.Producto(id, "FLETE", unidad, null, string.Empty, null);
        }
        finally
        {
            CerrojoDelServicio.Release();
        }
    }

    private Task<int> ContarMensajesAsync(EscenarioDeInventario esc, string tipo) => EnteroAsync(esc,
        $"""SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" = '{tipo}'""",
        $"SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] = '{tipo}'");

    private Task<int> FilasDelCruceAsync(EscenarioDeInventario esc, Guid factura, bool vivas) => EnteroAsync(esc,
        $"""SELECT COUNT(*) FROM dbo."INV_PurchaseMatchLines" m JOIN dbo."INV_Documents" d ON d."Id" = m."InvoiceDocumentId" WHERE d."PublicId" = '{factura}' AND m."IsDeleted" = {(vivas ? "false" : "true")}""",
        $"SELECT COUNT(*) FROM [dbo].[INV_PurchaseMatchLines] m JOIN [dbo].[INV_Documents] d ON d.[Id] = m.[InvoiceDocumentId] WHERE d.[PublicId] = '{factura}' AND m.[IsDeleted] = {(vivas ? 0 : 1)}");

    private async Task<int> EnteroAsync(EscenarioDeInventario esc, string postgres, string sqlServer) =>
        Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop, postgres, sqlServer));
}
