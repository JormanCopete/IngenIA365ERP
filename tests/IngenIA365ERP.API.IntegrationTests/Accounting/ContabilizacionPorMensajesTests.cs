using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadDeInventarioE2E;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// T474 (feature 012, I2; quickstart §4.3 a §4.10; US7-1 a US7-8; SC-002, SC-005, SC-011, SC-021), en el motor de
/// <c>DB_PROVIDER</c>, en una cooperativa aislada con la contabilidad iniciada y la matriz mínima
/// (<see cref="ContabilidadDeInventarioE2E"/>). Reemplaza a <c>E2E/Inventory/ContabilizacionDeInventarioTests</c> (T129 de la
/// 009). Las compras van en línea (el defecto), los ajustes positivos por lotes resumidos y los traslados sin paso; el
/// despachador lo conduce la prueba (<see cref="Integration.ConductorDelDespachador"/>). Un solo recorrido porque cada paso
/// depende del libro que dejó el anterior: en línea → lote resumido → anulación de un ajuste del resumido → traslado sin paso
/// → reenvío → validación previa → cierre con pendientes, rechazo, reapertura y reproceso → conciliación, huérfanos y cuadre.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ContabilizacionPorMensajesTests(CentralIdentityApiFixture fx)
{
    private const string Ajustes = "/api/inventory/adjustments";
    private const string Compras = "/api/inventory/purchases";

    [Fact]
    public async Task Cada_operacion_de_Inventario_llega_al_libro_por_mensajes_segun_su_modo()
    {
        var esc = await PrepararAsync(fx, "contabmsg");
        var despachador = fx.Despachador;
        var coop = esc.Coop.TenantPublicId;
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        var ayer = hoy.AddDays(-1).Year == hoy.Year ? hoy.AddDays(-1) : hoy;

        // Modos, antes de todo movimiento: ajustes positivos por lotes resumidos, traslados sin paso.
        var desde = new DateOnly(hoy.Year, 1, 1);
        await ParametroAsync(http, t, "Contabilidad.ModoDePaso", "PorLotes", desde, tipo: esc.Tipos["AJP"]);
        await ParametroAsync(http, t, "Contabilidad.Granularidad", "Resumido", desde, tipo: esc.Tipos["AJP"]);
        await ParametroAsync(http, t, "Contabilidad.ModoDePaso", "NoPasa", desde, cadena: "Transfers");

        // --------------------------------------------------------------- (1) en línea: la compra → EI, la factura → CP (US7-1)
        var proveedor = await ContabilidadE2E.CrearPersonaAsync(http, t, "ProveedorMsg");
        var compra = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Compras}/direct", new
        {
            receipt = new
            {
                documentTypePublicId = esc.Tipos["REC"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor,
                lines = new[] { new { productPublicId = esc.P("P1").Id, unitPublicId = esc.P("P1").Unidad, quantity = 10m, unitPrice = 1_000m } },
            },
            invoice = new
            {
                documentTypePublicId = esc.Tipos["FCP"],
                supplier = new { prefix = "FM", number = "7001", issueDate = hoy.ToString("yyyy-MM-dd"), paymentForm = "Cash", isElectronic = false },
            },
        });
        var recepcion = compra.GetProperty("receipt").GetProperty("publicId").GetGuid();
        var factura = compra.GetProperty("supplierInvoice").GetProperty("publicId").GetGuid();
        var compraRecibida = await MensajeAsync(http, t, recepcion, "CompraRecibida");
        Estado(compraRecibida).Should().Be(0, "con el despachador apagado lo en línea queda pendiente, visible (SC-010)");
        compraRecibida.GetProperty("prevalidationOutcome").GetInt32().Should().Be(1, "Postable: Contabilidad dijo que es contabilizable");

        var reloj = System.Diagnostics.Stopwatch.StartNew();
        await despachador.PasadasAsync(coop, 2);
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(1), "SC-011");
        compraRecibida = await MensajeAsync(http, t, recepcion, "CompraRecibida");
        Estado(compraRecibida).Should().Be(2, $"la compra en línea queda procesada: {compraRecibida}");
        TipoDeComprobante(compraRecibida).Should().Be("EI");
        var facturaRegistrada = await MensajeAsync(http, t, factura, "FacturaProveedorRegistrada");
        Estado(facturaRegistrada).Should().Be(2, $"la factura del proveedor: {facturaRegistrada}");
        TipoDeComprobante(facturaRegistrada).Should().Be("CP");

        // La devolución al proveedor llega detrás de su recepción, con su propio comprobante (quickstart §4.3, 3).
        var lineaRecibida = (await InventarioE2E.GetAsync(http, t, $"{Compras}/receipts/{recepcion}")).GetProperty("document").GetProperty("lines")[0]
            .GetProperty("linePublicId").GetGuid();
        var devolucion = await InventarioE2E.BorradorAsync(http, t, $"{Compras}/returns", new
        {
            documentTypePublicId = esc.Tipos["DVP"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor, reason = "Averiada",
            lines = new[] { new { productPublicId = esc.P("P1").Id, unitPublicId = esc.P("P1").Unidad, quantity = 2m, receiptLinePublicId = lineaRecibida } },
        });
        var idDevolucion = devolucion.GetProperty("publicId").GetGuid();
        await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/returns", idDevolucion);
        await despachador.PasadasAsync(coop, 2);
        var devuelta = await MensajeAsync(http, t, idDevolucion, "DevolucionRegistrada");
        Estado(devuelta).Should().Be(2, $"la devolución: {devuelta}");
        TipoDeComprobante(devuelta).Should().Be("SI");

        // -------------------------------------------- (2) lote resumido: ajustes de dos fechas y dos sucursales (US7-3)
        var a1 = await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P2", 5, 2_000m)], fecha: ayer);
        var a2 = await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P3", 4, 1_500m)]);
        var a3 = await esc.AjusteConfirmadoAsync(http, t, "AJP", "PV2", [new("P4", 3, 3_000m)]);
        var a4 = await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P6", 2, 500m)]);
        var ajustes = new[] { a1, a2, a3, a4 }.Select(a => a.GetProperty("publicId").GetGuid()).ToList();
        foreach (var a in ajustes) Estado(await MensajeAsync(http, t, a, "AjusteInventarioAprobado")).Should().Be(1, "InBatch hasta su lote");

        var previa = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/accounting/inventory/batches/preview", new
        {
            from = ayer.ToString("yyyy-MM-dd"), to = hoy.ToString("yyyy-MM-dd"), documentTypeCodes = new[] { "AJP" },
        });
        var corteDelLote = previa.GetProperty("cutoffMessagePublicId").GetGuid();
        var orden = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/accounting/inventory/batches", new
        {
            cutoffMessagePublicId = corteDelLote, from = ayer.ToString("yyyy-MM-dd"), to = hoy.ToString("yyyy-MM-dd"),
            documentTypeCodes = new[] { "AJP" }, reason = "Lote de los ajustes del ensayo",
        });
        orden.StatusCode.Should().Be(HttpStatusCode.Accepted, await orden.Content.ReadAsStringAsync());
        var lote = (await InventarioE2E.LeerAsync(orden)).GetProperty("batchPublicId").GetGuid();
        await despachador.PasadasAsync(coop, 3);

        var detalle = await InventarioE2E.GetAsync(http, t, $"/api/accounting/inventory/batches/{lote}");
        detalle.GetProperty("batch").GetProperty("status").GetInt32().Should().Be(2, $"el lote termina completo: {detalle}");
        var comprobantes = detalle.GetProperty("vouchers").EnumerateArray().ToList();
        var esperados = ayer == hoy ? 2 : 3;
        comprobantes.Should().HaveCount(esperados, "un comprobante por fecha, tipo y sucursal");
        comprobantes.Should().OnlyContain(c => c.GetProperty("voucherTypeCode").GetString() == "EI");
        comprobantes.Select(c => DateOnly.Parse(c.GetProperty("operationDate").GetString()!)).Should().Contain(hoy)
            .And.OnlyContain(d => d == hoy || d == ayer, "fechado en la fecha de los ajustes, no en la del lote");
        detalle.GetProperty("documents").GetArrayLength().Should().Be(4, "el lote lista sus documentos");
        foreach (var a in ajustes) Estado(await MensajeAsync(http, t, a, "AjusteInventarioAprobado")).Should().Be(2);
        var resumidoDeP6 = Comprobante(await MensajeAsync(http, t, ajustes[3], "AjusteInventarioAprobado"));
        resumidoDeP6.Should().NotBeNull();

        // ---------------------------- (3) anular un ajuste del resumido: comprobante nuevo, el resumido intacto (US7-8)
        var anular = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Ajustes}/{ajustes[3]}/void", new { reason = "Error de digitación" });
        anular.StatusCode.Should().Be(HttpStatusCode.Created, await anular.Content.ReadAsStringAsync());
        var anulacion = (await InventarioE2E.LeerAsync(anular)).GetProperty("voidingDocumentPublicId").GetGuid();
        var mensajeDeAnulacion = await MensajeAsync(http, t, anulacion, "DocumentoAnulado");
        Estado(mensajeDeAnulacion).Should().Be(1, "la anulación sigue el modo sellado del original (por lotes)");
        // La anulación es un documento de otro tipo: el lote se ordena por el rango, sin filtrar por el tipo del original.
        var previa2 = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/accounting/inventory/batches/preview", new
        {
            from = hoy.ToString("yyyy-MM-dd"), to = hoy.ToString("yyyy-MM-dd"),
        });
        var orden2 = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/accounting/inventory/batches", new
        {
            cutoffMessagePublicId = previa2.GetProperty("cutoffMessagePublicId").GetGuid(), from = hoy.ToString("yyyy-MM-dd"),
            to = hoy.ToString("yyyy-MM-dd"), reason = "Lote de la anulación",
        });
        orden2.StatusCode.Should().Be(HttpStatusCode.Accepted, await orden2.Content.ReadAsStringAsync());
        await despachador.PasadasAsync(coop, 3);
        mensajeDeAnulacion = await MensajeAsync(http, t, anulacion, "DocumentoAnulado");
        Estado(mensajeDeAnulacion).Should().Be(2, $"la anulación se contabiliza: {mensajeDeAnulacion}");
        Comprobante(mensajeDeAnulacion).Should().NotBeNull();
        (Comprobante(mensajeDeAnulacion) != resumidoDeP6).Should().BeTrue("es un comprobante nuevo");
        var original = await InventarioE2E.GetAsync(http, t, $"/api/accounting/documents/{resumidoDeP6}");
        original.GetProperty("status").ToString().Should().NotBe("Reversed", "el resumido queda intacto").And.NotBe("3");

        // ------------------------------------------------------------ (4) traslado sin paso → NotApplicable (US7-4)
        var traslado = await InventarioE2E.BorradorAsync(http, t, "/api/inventory/transfers", new
        {
            documentTypePublicId = esc.Tipos["TRD"], warehousePublicId = esc.Bodega("PRIN"), destinationWarehousePublicId = esc.Bodega("PV1"),
            lines = esc.Lineas(new EscenarioDeInventario.Linea("P1", 2)),
        });
        var idTraslado = traslado.GetProperty("publicId").GetGuid();
        (await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/inventory/transfers/{idTraslado}/dispatch", new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await despachador.PasadaAsync(coop);
        (await MensajesAsync(http, t, idTraslado)).Where(m => m.GetProperty("kind").GetInt32() == 1)
            .Should().NotBeEmpty().And.OnlyContain(m => Estado(m) == 4, "el traslado no pasa");

        // Envío posterior de lo que no pasó (FR-078, US7-10): el traslado pasa a su lote y se contabiliza con su fecha.
        // Primero la cadena pasa a en línea desde hoy; lo ya confirmado conserva su modo sellado («no pasa») hasta enviarlo.
        await ParametroAsync(http, t, "Contabilidad.ModoDePaso", "EnLinea", hoy, cadena: "Transfers");
        var delTraslado = (await MensajesAsync(http, t, idTraslado)).Where(m => m.GetProperty("kind").GetInt32() == 1).ToList();
        var envio = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/inventory/messages/send-not-applicable", new
        {
            from = hoy.ToString("yyyy-MM-dd"), to = hoy.ToString("yyyy-MM-dd"), documentTypePublicIds = new[] { esc.Tipos["TRD"] },
            cutoffMessagePublicId = delTraslado[^1].GetProperty("messagePublicId").GetGuid(), reason = "La contadora pidió los traslados",
        });
        envio.StatusCode.Should().Be(HttpStatusCode.Accepted, await envio.Content.ReadAsStringAsync());
        await despachador.PasadasAsync(coop, 3);
        (await MensajesAsync(http, t, idTraslado)).Where(m => m.GetProperty("kind").GetInt32() == 1)
            .Should().OnlyContain(m => Estado(m) == 2 && TipoDeComprobante(m) == "TR", "enviado después, se contabiliza");

        // ------------------------------------------ (5) reenvío de un mensaje procesado: sin segundo comprobante (US7-5)
        var reenvio = await despachador.EnviarComoProcesoAsync<Result<ResultadoDeConsumo>>(coop,
            $"Mensaje:{compraRecibida.GetProperty("messagePublicId").GetGuid()}",
            new PostInventoryMessagesCommand([compraRecibida.GetProperty("messagePublicId").GetGuid()], null));
        reenvio.IsSuccess.Should().BeTrue();
        reenvio.Value.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        (await ComprobantesDelDocumentoAsync(fx, esc, recepcion)).Should().Be(1, "un solo comprobante por la compra (SC-002)");

        // ------------------------------------------ (6) validación previa: lo que no es contabilizable no se confirma (US7-2)
        var sinRegla = await esc.AjusteAsync(http, t, "AJN", "PRIN", [new("P1", 1)], causa: "DANO");
        var rechazo = await InventarioE2E.FallaAsync(await InventarioE2E.PedirConfirmarAsync(http, t, Ajustes, sinRegla), "Inventory.Prevalidation.NotPostable");
        rechazo.GetProperty("data").GetProperty("errors").EnumerateArray().Should().NotBeEmpty()
            .And.Contain(e => e.GetProperty("whoFixes").ValueKind == System.Text.Json.JsonValueKind.Object, "dice quién corrige");
        (await InventarioE2E.GetAsync(http, t, $"{Ajustes}/{sinRegla}")).GetProperty("status").GetInt32().Should().Be(0, "sigue en borrador");
        (await MensajesAsync(http, t, sinRegla)).Should().BeEmpty("no emite nada");

        // ------------------ (7) cierre con pendientes → rechazo en la bandeja → reapertura → reproceso con la fecha original (US7-6)
        var merma = await esc.AjusteConfirmadoAsync(http, t, "AJN", "PRIN", [new("P1", 1)], causa: "MERMA");
        var idMerma = merma.GetProperty("publicId").GetGuid();
        Estado(await MensajeAsync(http, t, idMerma, "AjusteInventarioAprobado")).Should().Be(0, "en línea y todavía sin despachar");
        var cierre = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/accounting/periods/{hoy.Year}/{hoy.Month}/close", new { });
        await InventarioE2E.FallaAsync(cierre, "Accounting.Period.InventoryPending");
        var reconocido = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/accounting/periods/{hoy.Year}/{hoy.Month}/close",
            new { acknowledgeInventoryPending = true });
        reconocido.StatusCode.Should().Be(HttpStatusCode.NoContent, await reconocido.Content.ReadAsStringAsync());

        // Con el mes cerrado, la validación previa también detiene un documento fechado en él (quickstart §4.4, caso 4).
        var enMesCerrado = await esc.AjusteAsync(http, t, "AJN", "PRIN", [new("P1", 1)], causa: "MERMA");
        await InventarioE2E.FallaAsync(await InventarioE2E.PedirConfirmarAsync(http, t, Ajustes, enMesCerrado), "Inventory.Prevalidation.NotPostable");

        await despachador.PasadaAsync(coop);
        var rechazada = await MensajeAsync(http, t, idMerma, "AjusteInventarioAprobado");
        Estado(rechazada).Should().Be(3, $"el período cerrado lo rechaza a la bandeja: {rechazada}");
        rechazada.GetProperty("lastError").GetProperty("code").GetString().Should().Be("Accounting.Period.Closed");
        (await InventarioE2E.GetAsync(http, t, $"{Ajustes}/{idMerma}")).GetProperty("status").GetInt32().Should().Be(2, "el documento sigue confirmado");

        await ContabilidadE2E.ReabrirMesAsync(http, t, hoy.Year, hoy.Month, "Contabilizar la merma rechazada");
        var reproceso = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/inventory/messages/reprocess", new
        {
            messagePublicIds = new[] { rechazada.GetProperty("messagePublicId").GetGuid() }, reason = "Mes reabierto",
        });
        reproceso.StatusCode.Should().Be(HttpStatusCode.Accepted, await reproceso.Content.ReadAsStringAsync());
        await despachador.PasadasAsync(coop, 3);
        var reprocesada = await MensajeAsync(http, t, idMerma, "AjusteInventarioAprobado");
        Estado(reprocesada).Should().Be(2, $"reprocesada: {reprocesada}");
        TipoDeComprobante(reprocesada).Should().Be("SI");
        var comprobanteDeLaMerma = await InventarioE2E.GetAsync(http, t, $"/api/accounting/documents/{Comprobante(reprocesada)}");
        DateOnly.Parse(comprobanteDeLaMerma.GetProperty("date").GetString()!).Should().Be(hoy, "con la fecha original del documento");

        // ------------------------------------------------ (8) SC-021: lo Postable procesado sin cambios no quedó rechazado
        var postablesRechazados = await InventarioE2E.GetAsync(http, t, "/api/inventory/messages?prevalidationOutcome=Postable&status=Rejected&pageSize=100");
        postablesRechazados.GetProperty("messages").GetProperty("items").EnumerateArray()
            .Should().BeEmpty("sólo el cierre del mes rechazó algo, y ya se reprocesó");

        // ------------------------------------------------ (9) conciliación con diferencia cero sin pendientes (US7-7, SC-005)
        var conciliacion = await InventarioE2E.InformeAsync(http, t, "reconciliation", $"asOf={hoy:yyyy-MM-dd}");
        var conjuntos = conciliacion.Filas.Where(f => ContabilidadE2E.Tabla.Texto(f, 0).StartsWith("Conjunto ", StringComparison.Ordinal)).ToList();
        conjuntos.Should().NotBeEmpty($"hay conjuntos de cuentas mapeadas (notas: {string.Join(" | ", conciliacion.Notas)})");
        foreach (var fila in conjuntos) conciliacion.Numero(fila, "Diferencia").Should().Be(0m, $"conjunto {ContabilidadE2E.Tabla.Texto(fila, 0)}");

        // ------------------------------------------------------------------ (10) huérfanos cero y el libro cuadra
        (await ComprobantesSinReciboAsync(fx, esc)).Should().Be(0, "ningún comprobante de origen INV sin recibo");
        (await RecibosSinComprobanteAsync(fx, esc)).Should().Be(0, "ningún recibo de negocio sin comprobante salvo valor cero");
        (await DescuadreDelLibroAsync(fx, esc)).Should().Be(0m, "el balance de prueba cuadra");
    }
}
