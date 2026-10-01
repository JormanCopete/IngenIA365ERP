using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using static IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing.EscenarioDeFacturacionElectronica;

namespace IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;

/// <summary>
/// T685 (feature 012, I4, US8; quickstart §6.1, §6.2, §6.4, §6.8, §6.9; escenarios US8-1, US8-2, US8-6, US8-7 y US8-9), sobre
/// <c>CanalSimulado</c> y en el motor de <c>DB_PROVIDER</c>: la preparación dice lo que falta y una venta fiscal no se confirma hasta estar
/// lista; la factura se valida con el número siguiente de su resolución, CUFE, QR y artefactos, y sólo entonces se entrega; los artefactos son
/// adjuntos que se leen con el permiso de ventas; «Anular» una factura validada es su nota crédito total con CUDE y número propio; una
/// resolución de 1 a 10 avisa al 9 y se agota al 11, y una nota sobre una factura de resolución vencida sí sale; el documento soporte lleva
/// CUDS y su nota de ajuste. Cada caso en su cooperativa aislada. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class DocumentosElectronicosTests(CentralIdentityApiFixture fx)
{
    private const int Validado = 2;
    private const int ValidadoConNotificaciones = 3;

    [Fact]
    public async Task Sin_configuracion_la_venta_fiscal_no_confirma_y_despues_la_factura_se_valida_con_su_numero_CUFE_QR_y_artefactos()
    {
        var esc = await PrepararAsync(fx, "felisto", configurar: false);
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var supervisor = esc.Ventas.Supervisor;
        var comprador = await PersonaAsync(http, t, "Wilson", 7);

        // (1) Sin configuración: la preparación lista lo que falta, con quién lo arregla, y la venta fiscal no se confirma.
        var preparacion = await InventarioE2E.GetAsync(http, t, $"/api/electronic-invoicing/readiness?documentType={esc.Tipo("FE")}");
        preparacion.GetProperty("verdict").GetInt32().Should().Be(3, $"Blocked: {preparacion}");
        var faltan = preparacion.GetProperty("missing").EnumerateArray().ToList();
        faltan.Select(m => m.GetProperty("code").GetString()).Should().Contain("ElectronicInvoicing.Readiness.NoSettings");
        faltan.Should().OnlyContain(m => m.GetProperty("whoFixes").GetProperty("permission").GetString() != null);

        await EscenarioDeVentas.SesionAsync(http, supervisor.Token, esc.CajaElectronica);
        var bloqueada = await esc.BorradorDeFacturaAsync(http, supervisor, comprador);
        var noLista = await InventarioE2E.FallaAsync(await PedirConfirmarAsync(http, supervisor.Token, Facturas, Id(bloqueada), AmountDue(bloqueada)),
            "ElectronicInvoicing.NotReady");
        noLista.GetProperty("data").GetProperty("missing").GetArrayLength().Should().BeGreaterThan(0);

        // (2) Configuración, credencial verificada y resoluciones asociadas: la preparación queda vacía.
        await esc.ConfigurarAsync(fx, http);
        var lista = await InventarioE2E.GetAsync(http, t, $"/api/electronic-invoicing/readiness?documentType={esc.Tipo("FE")}");
        lista.GetProperty("verdict").GetInt32().Should().Be(1, $"Electronic: {lista}");
        lista.GetProperty("missing").GetArrayLength().Should().Be(0);
        var clave = (await InventarioE2E.GetAsync(http, t, $"/api/electronic-invoicing/resolutions/{esc.Resoluciones["FE"]}"))
            .GetProperty("resolution").GetProperty("channels")[0].GetProperty("technicalKeyMasked").GetString();
        clave.Should().EndWith("ab12").And.NotContain("clave-tecnica", "la clave técnica sólo se muestra enmascarada");

        // (3) La misma venta ahora confirma: número 1 de la resolución FE, pendiente de emisión (la oficina no espera a la DIAN).
        var confirmada = await InventarioE2E.ExitoAsync(http, supervisor.Token, HttpMethod.Post, $"{Facturas}/{Id(bloqueada)}/confirm",
            new { expectedAmountDue = AmountDue(bloqueada) });
        var factura = await VentaAsync(http, t, Id(confirmada));
        factura.GetProperty("prefix").GetString().Should().Be("FE");
        factura.GetProperty("number").GetInt64().Should().Be(1, "el número siguiente del rango");
        var electronico = Electronico(factura);
        factura.GetProperty("electronic").GetProperty("deliverable").GetBoolean().Should().BeFalse("todavía no está validada");

        // (4) La emisión: validada, con CUFE, QR y los artefactos; sólo entonces se entrega.
        var emitido = await EmitirAsync(http, t, electronico);
        var detalle = await DetalleAsync(http, t, electronico);
        Estado(detalle).Should().Be(Validado, $"{emitido} / {detalle}");
        detalle.GetProperty("document").GetProperty("number").GetString().Should().Be("FE1");
        detalle.GetProperty("document").GetProperty("uniqueCode").GetString().Should().HaveLength(96);
        detalle.GetProperty("document").GetProperty("uniqueCodeKind").GetInt32().Should().Be(1, "CUFE");
        detalle.GetProperty("qrContent").GetString().Should().NotBeNullOrWhiteSpace();
        var artefactos = detalle.GetProperty("versions")[0].GetProperty("artifacts").EnumerateArray()
            .Select(a => a.GetProperty("artifact").GetString()).ToList();
        artefactos.Should().Contain(["Canonical", "SignedXml", "AttachedDocument", "GraphicRepresentation"]);
        detalle.GetProperty("transmissions").GetArrayLength().Should().Be(1, "una sola transmisión, con su respuesta");

        var venta = await VentaAsync(http, t, Id(confirmada));
        venta.GetProperty("electronic").GetProperty("deliverable").GetBoolean().Should().BeTrue("se entrega después de validar");

        // (5) Los artefactos se leen con Inventory.Sales.View; sin ese permiso, el mismo 404 que lo inexistente.
        var enlace = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{electronico}/download-link?artifact=GraphicRepresentation");
        enlace.StatusCode.Should().Be(HttpStatusCode.OK, await enlace.Content.ReadAsStringAsync());
        await InventarioE2E.RolAsync(http, esc.Coop, "FESINVENTA", null, extra: ["Inventory.Purchases.View"]);
        var sinVentas = await esc.UsuarioSoloConAsync(fx, http, "FESINVENTA", $"sinventas.{esc.Coop.TenantPublicId:N}@coop.fe.test");
        var negado = await InventarioE2E.MandarAsync(http, sinVentas, HttpMethod.Post, $"{Documentos}/{electronico}/download-link?artifact=GraphicRepresentation");
        negado.StatusCode.Should().Be(HttpStatusCode.NotFound, await negado.Content.ReadAsStringAsync());

        // (6) Un documento contrario sobre la factura electrónica no procede: se corrige con su nota.
        var contrario = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Facturas}/{Id(confirmada)}/void", new { reason = "Error" });
        await InventarioE2E.FallaAsync(contrario, "Inventory.Document.FiscalUseCorrection");

        // (7) «Anular»: la nota crédito total con devolución, su propio número y su CUDE (SC-003).
        var nota = await InventarioE2E.BorradorAsync(http, supervisor.Token, Notas, new
        {
            originDocumentPublicId = Id(confirmada), reason = "El cliente devolvió todo", totalVoid = true, withReturn = true,
            lines = Array.Empty<object>(), refunds = Array.Empty<object>(), documentTypePublicId = esc.Tipo("NC"), correctionConceptCode = "2",
        });
        var notaConfirmada = await InventarioE2E.ExitoAsync(http, supervisor.Token, HttpMethod.Post, $"{Notas}/{Id(nota)}/confirm",
            new { expectedAmountDue = AmountDue(nota) });
        var notaDeVenta = await VentaAsync(http, t, Id(notaConfirmada));
        notaDeVenta.GetProperty("prefix").GetString().Should().Be("NC");
        var notaElectronica = Electronico(notaDeVenta);
        await EmitirAsync(http, t, notaElectronica);
        var detalleDeLaNota = await DetalleAsync(http, t, notaElectronica);
        Estado(detalleDeLaNota).Should().BeOneOf(Validado, ValidadoConNotificaciones);
        detalleDeLaNota.GetProperty("document").GetProperty("uniqueCodeKind").GetInt32().Should().Be(2, "CUDE");
        detalleDeLaNota.GetProperty("correctsDocument").GetProperty("number").GetString().Should().Be("FE1");
        (await esc.MensajesAsync(fx, "NotaCreditoEmitida", Id(notaConfirmada), "\"isTotalVoid\":true")).Should().Be(1, "marcada como anulación total");
        (await esc.MensajesAsync(fx, "DevolucionRegistrada", Id(notaConfirmada))).Should().Be(1);
    }

    [Fact]
    public async Task Una_resolucion_de_1_a_10_avisa_al_9_se_agota_al_11_y_la_nota_de_una_resolucion_vencida_si_sale()
    {
        var esc = await PrepararAsync(fx, "feresol");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var supervisor = esc.Ventas.Supervisor;
        var comprador = await PersonaAsync(http, t, "Rango", 8);

        // (1) Una factura con la resolución FX, que vence hoy.
        var deFx = await esc.FacturaConfirmadaAsync(http, supervisor, comprador, "FX");
        var vencida = await ElectronicoAsync(http, t, Id(deFx));
        await EmitirAsync(http, t, vencida);
        Estado(await DetalleAsync(http, t, vencida)).Should().Be(Validado);

        // (2) Ocho facturas FE: todavía no avisa; la novena, sí (Dian.AvisoResolucionPorcentaje = 0,90).
        for (var i = 1; i <= 8; i++) await esc.FacturaConfirmadaAsync(http, supervisor, comprador);
        await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, "einvoicing.alerts");
        (await esc.AlertasAsync(http, "Dian.ResolucionPorAgotar")).Should().NotContain(a => a.ToString().Contains("prefijo FE"), "8 de 10 no llega al 90 %");
        await esc.FacturaConfirmadaAsync(http, supervisor, comprador);
        await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, "einvoicing.alerts");
        (await esc.AlertasAsync(http, "Dian.ResolucionPorAgotar")).Should().Contain(a => a.ToString().Contains("prefijo FE"), "9 de 10 avisa");

        // (3) La décima consume el último número; la undécima no se numera fuera de la resolución.
        var decima = await esc.FacturaConfirmadaAsync(http, supervisor, comprador);
        (await VentaAsync(http, t, Id(decima))).GetProperty("number").GetInt64().Should().Be(10);
        await EscenarioDeVentas.SesionAsync(http, supervisor.Token, esc.CajaElectronica);
        var once = await esc.BorradorDeFacturaAsync(http, supervisor, comprador);
        var agotada = await InventarioE2E.FallaAsync(await PedirConfirmarAsync(http, supervisor.Token, Facturas, Id(once), AmountDue(once)),
            "ElectronicInvoicing.NotReady");
        agotada.GetProperty("data").GetProperty("missing").EnumerateArray()
            .Should().Contain(m => m.GetProperty("code").GetString() == "ElectronicInvoicing.Readiness.NoResolution"
                                   && m.GetProperty("message").GetString()!.Contains("agotó"), agotada.ToString());
        var resolucion = await InventarioE2E.GetAsync(http, t, $"/api/electronic-invoicing/resolutions/{esc.Resoluciones["FE"]}");
        resolucion.GetProperty("resolution").GetProperty("lastIssuedNumber").GetInt64().Should().Be(10, "nunca se numera fuera del rango");

        // (4) Mañana la resolución FX está vencida y la nota crédito de su factura sí sale: las notas no numeran con resolución.
        using var manana = fx.Despachador.Adelantar(TimeSpan.FromDays(1));
        var nota = await InventarioE2E.BorradorAsync(http, supervisor.Token, Notas, new
        {
            originDocumentPublicId = Id(deFx), reason = "Anulación de la factura de la resolución vencida", totalVoid = true, withReturn = true,
            lines = Array.Empty<object>(), refunds = Array.Empty<object>(), documentTypePublicId = esc.Tipo("NC"), correctionConceptCode = "2",
        });
        var notaConfirmada = await InventarioE2E.ExitoAsync(http, supervisor.Token, HttpMethod.Post, $"{Notas}/{Id(nota)}/confirm",
            new { expectedAmountDue = AmountDue(nota) });
        var notaElectronica = await ElectronicoAsync(http, t, Id(notaConfirmada));
        await EmitirAsync(http, t, notaElectronica);
        var detalle = await DetalleAsync(http, t, notaElectronica);
        Estado(detalle).Should().Be(Validado, detalle.ToString());
        detalle.GetProperty("correctsDocument").GetProperty("number").GetString().Should().Be("FX1");
    }

    [Fact]
    public async Task El_documento_soporte_sale_con_CUDS_y_su_nota_de_ajuste_al_devolver()
    {
        var esc = await PrepararAsync(fx, "fesoporte");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        const string Compras = "/api/inventory/purchases";

        // Proveedor B, no obligado a facturar: la recepción propone su documento soporte.
        var documento = (6_100_000 + Random.Shared.Next(1, 99_999)) * 10L + 6;
        var alta = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento.ToString(), firstName = "Proveedor", lastName = "NoObligado", email = $"prov.{documento}@coop.fe.test",
            address = "Vereda El Placer", isObligatedToInvoice = false,
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var proveedor = (await InventarioE2E.LeerAsync(alta)).GetGuid();

        var recepcion = await InventarioE2E.BorradorAsync(http, t, $"{Compras}/receipts", new
        {
            documentTypePublicId = esc.Inv.Tipos["REC"], warehousePublicId = esc.Inv.Bodega("PRIN"), supplierPersonPublicId = proveedor,
            lines = new[] { new { productPublicId = esc.Inv.P("P7").Id, unitPublicId = esc.Inv.P("P7").Unidad, quantity = 5m, unitPrice = 50_000m } },
        });
        var idRecepcion = recepcion.GetProperty("publicId").GetGuid();
        var recibida = await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/receipts", idRecepcion);
        var propuesta = recibida.GetProperty("warnings").EnumerateArray().Single(w => w.GetProperty("code").GetString() == "Inventory.SupportDocument.Proposed");
        var soporte = propuesta.GetProperty("data").GetProperty("supportDocumentPublicId").GetGuid();

        // El documento soporte numera con su resolución y se valida con CUDS; emite FacturaProveedorRegistrada.
        await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/support-documents", soporte);
        var detalleDs = await InventarioE2E.GetAsync(http, t, $"{Compras}/support-documents/{soporte}");
        var dsElectronico = await ElectronicoDeCompraAsync(http, t, soporte);
        await EmitirAsync(http, t, dsElectronico);
        var ds = await DetalleAsync(http, t, dsElectronico);
        Estado(ds).Should().Be(Validado, ds.ToString());
        ds.GetProperty("document").GetProperty("number").GetString().Should().Be("DS1");
        ds.GetProperty("document").GetProperty("uniqueCodeKind").GetInt32().Should().Be(3, "CUDS");
        (await esc.MensajesAsync(fx, "FacturaProveedorRegistrada", soporte)).Should().Be(1);

        // Devolverle una unidad: la devolución y la nota de ajuste del documento soporte, con su propio código.
        var lineaRecibida = (await InventarioE2E.GetAsync(http, t, $"{Compras}/receipts/{idRecepcion}"))
            .GetProperty("document").GetProperty("lines")[0].GetProperty("linePublicId").GetGuid();
        var devolucion = await InventarioE2E.BorradorAsync(http, t, $"{Compras}/returns", new
        {
            documentTypePublicId = esc.Inv.Tipos["DVP"], warehousePublicId = esc.Inv.Bodega("PRIN"), supplierPersonPublicId = proveedor, reason = "Mercancía averiada",
            lines = new[] { new { productPublicId = esc.Inv.P("P7").Id, unitPublicId = esc.Inv.P("P7").Unidad, quantity = 1m, receiptLinePublicId = lineaRecibida } },
        });
        await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/returns", devolucion.GetProperty("publicId").GetGuid());

        var lineaDelSoporte = detalleDs.GetProperty("document").GetProperty("lines")[0].GetProperty("linePublicId").GetGuid();
        var nota = await InventarioE2E.BorradorAsync(http, t, $"{Compras}/support-documents", new
        {
            documentTypePublicId = esc.Tipo("NDS"), supplierPersonPublicId = proveedor, supplierInvoicePublicId = soporte, noteKind = "Credit",
            reason = "Devolución de una unidad", correctionConceptCode = "1",
            lines = new[] { new { invoiceLinePublicId = lineaDelSoporte, productPublicId = esc.Inv.P("P7").Id, unitPublicId = esc.Inv.P("P7").Unidad, quantity = 1m, amount = 50_000m } },
        });
        var idNota = nota.GetProperty("publicId").GetGuid();
        await InventarioE2E.ConfirmarAsync(http, t, $"{Compras}/support-documents", idNota);
        var notaElectronica = await ElectronicoDeCompraAsync(http, t, idNota);
        await EmitirAsync(http, t, notaElectronica);
        var detalleDeLaNota = await DetalleAsync(http, t, notaElectronica);
        Estado(detalleDeLaNota).Should().Be(Validado, detalleDeLaNota.ToString());
        detalleDeLaNota.GetProperty("correctsDocument").GetProperty("number").GetString().Should().Be("DS1");
        (await esc.MensajesAsync(fx, "FacturaProveedorRegistrada", idNota)).Should().Be(1, "la nota, con su signo");

        // Los artefactos de compra se leen con Inventory.Purchases.View.
        var enlace = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{dsElectronico}/download-link?artifact=SignedXml");
        enlace.StatusCode.Should().Be(HttpStatusCode.OK, await enlace.Content.ReadAsStringAsync());
    }

    /// <summary>El documento electrónico de un documento de compras: lo busca en la bandeja por su origen.</summary>
    private static async Task<Guid> ElectronicoDeCompraAsync(HttpClient http, string t, Guid documento)
    {
        var bandeja = await InventarioE2E.GetAsync(http, t, $"{Documentos}?pageSize=100");
        return bandeja.GetProperty("items").EnumerateArray()
            .Single(d => d.GetProperty("source").GetProperty("documentPublicId").GetGuid() == documento)
            .GetProperty("electronicDocumentPublicId").GetGuid();
    }
}
