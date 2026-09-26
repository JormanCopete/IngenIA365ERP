using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, T327 (FR-050, US9-1, US9-2; contracts/api.md §14.4; mensajes.md §6.4, §6.10): la factura del proveedor contra sus
/// recepciones, la unicidad de su número y su CUFE, impuestos y retenciones por el catálogo vigente, la diferencia de precio
/// (dos vías, E6), el mensaje sin costo, los eventos RADIAN iniciales y lo que impide anularla.
/// </summary>
public class FacturaDeProveedorTests
{
    // --------------------------------------------------------------------------------------- contra la recepción --

    [Fact]
    public async Task Contra_una_recepcion_de_otro_proveedor_o_sin_confirmar_no_se_guarda()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var deB = await c.RecepcionDeTresDocenasAsync(c.ProveedorB);
        var otra = await c.GuardarAsync(await c.FacturaContraAsync(deB, ComprasDePrueba.Documento()));
        otra.Error.Code.Should().Be("Inventory.Purchase.ReceiptFromOtherSupplier");

        var borrador = (await c.GuardarAsync(c.Recepcion(lineas: [c.Linea(c.P1, 1m, 1_000m)]))).Value;
        var sinConfirmar = await c.GuardarAsync(await c.FacturaContraAsync(borrador, ComprasDePrueba.Documento()));
        sinConfirmar.Error.Code.Should().Be("Inventory.Purchase.ReceiptNotConfirmed");
    }

    [Fact]
    public async Task No_pasa_de_lo_recibido_menos_lo_facturado_y_lo_avisa_al_guardar()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        await c.ConfirmadoAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento("1"), cantidad: 2m));

        var segunda = await c.GuardarAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento("2"), cantidad: 2m));
        segunda.IsSuccess.Should().BeTrue();
        segunda.Value.Warnings.Should().Contain(w => w.Code == "Inventory.Purchase.InvoiceExceedsReceived");

        var confirmada = await c.ConfirmarAsync(segunda.Value.PublicId);
        confirmada.Error.Code.Should().Be("Inventory.Purchase.InvoiceExceedsReceived");
        var datos = JsonSerializer.Serialize(CatalogoDePrueba.Datos(confirmada.Error));
        datos.Should().Contain("\"received\":36").And.Contain("\"invoiced\":24").And.Contain("\"available\":12");
    }

    [Fact]
    public async Task Los_servicios_van_sin_recepcion_y_la_mercancia_no()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var flete = await c.ServicioAsync();
        var servicio = await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento("SV1"), lineas: [c.Linea(flete, 1m, 50_000m)]));
        c.Documento(servicio.PublicId).Status.Should().Be(DocumentStatus.Confirmed);

        var mercancia = await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento("SV2"), lineas: [c.Linea(c.P1, 1m, 1_000m)]));
        mercancia.Error.Code.Should().Be("Inventory.Purchase.GoodsWithoutReceipt");
    }

    [Fact]
    public async Task No_reune_recepciones_con_modos_de_paso_distintos()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var enLinea = await c.RecepcionDeTresDocenasAsync();
        c.K.Parametro(ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes");
        var porLotes = await c.RecepcionDeTresDocenasAsync();
        c.Documento(porLotes.PublicId).PostingMode.Should().Be(PostingMode.Batch);

        var lineas = (await c.LineasAsync(enLinea.PublicId)).Concat(await c.LineasAsync(porLotes.PublicId))
            .Select(l => new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, l.Quantity, UnitPrice: l.UnitPrice, ReceiptLinePublicId: l.PublicId)).ToArray();
        var r = await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento(), lineas: lineas));
        r.Error.Code.Should().Be("Inventory.Purchase.MixedPostingDestinations");
    }

    [Fact]
    public async Task La_factura_copia_el_modo_de_su_recepcion_aunque_el_parametro_cambie()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        c.K.Parametro(ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes");
        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento()));
        c.Documento(factura.PublicId).PostingMode.Should().Be(PostingMode.Online, "es un derivado: copia el modo de la entrega de su recepción");
    }

    // ------------------------------------------------------------------------------------------------ unicidad --

    [Fact]
    public async Task El_mismo_numero_del_mismo_proveedor_no_se_registra_dos_veces_y_anularlo_lo_libera()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var flete = await c.ServicioAsync();
        var primera = await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento("4521"), lineas: [c.Linea(flete, 1m, 50_000m)]));

        var repetida = await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento("4521"), lineas: [c.Linea(flete, 1m, 50_000m)]));
        repetida.Error.Code.Should().Be("Inventory.SupplierInvoice.Duplicate");
        JsonSerializer.Serialize(CatalogoDePrueba.Datos(repetida.Error)).Should().Contain(primera.PublicId.ToString());

        // Otro proveedor puede tener el mismo número.
        (await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento("4521"), c.ProveedorB, lineas: [c.Linea(flete, 1m, 50_000m)]))).IsSuccess.Should().BeTrue();

        (await c.Anular().Handle(new VoidInventoryDocumentCommand(primera.PublicId, DocumentClassGroup.Purchases, "registro errado"), default)).IsSuccess.Should().BeTrue();
        (await c.C.Db.SupplierInvoiceDetails.SingleAsync(d => d.DocumentId == c.Documento(primera.PublicId).Id)).IsReleased.Should().BeTrue();
        (await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento("4521"), lineas: [c.Linea(flete, 1m, 50_000m)]))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CUFE_repetido_electronica_sin_CUFE_y_vencimiento_antes_de_la_emision()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var flete = await c.ServicioAsync();
        var cufe = new string('a', 96);
        await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento("1", cufe: cufe, electronica: true), lineas: [c.Linea(flete, 1m, 1_000m)]));

        (await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento("2", cufe: cufe.ToUpperInvariant(), electronica: true), lineas: [c.Linea(flete, 1m, 1_000m)])))
            .Error.Code.Should().Be("Inventory.SupplierInvoice.CufeDuplicate");
        (await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento("3", electronica: true), lineas: [c.Linea(flete, 1m, 1_000m)])))
            .Error.Code.Should().Be("Inventory.SupplierInvoice.CufeRequired");
        (await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento("4", credito: true, vence: CatalogoDePrueba.Hoy.AddDays(-1)), lineas: [c.Linea(flete, 1m, 1_000m)])))
            .Error.Code.Should().Be("Inventory.SupplierInvoice.DueDateInvalid");
        (await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento("5") with { PaymentForm = "Credit", DueDate = null }, lineas: [c.Linea(flete, 1m, 1_000m)])))
            .Error.Code.Should().Be("Inventory.SupplierInvoice.DueDateInvalid");
    }

    // ------------------------------------------------------------------------------------ impuestos y retenciones --

    [Fact]
    public async Task La_base_igual_al_minimo_retiene_y_por_debajo_no()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var minimo = 27m * ComprasDePrueba.Uvt; // 1.414.098

        var igual = await c.ConfirmadoAsync(await c.FacturaContraAsync(
            await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P1, 1m, minimo)])), ComprasDePrueba.Documento("1")));
        var doc = c.Documento(igual.PublicId);
        doc.WithholdingTotal.Should().Be(35_352.45m);
        doc.AmountDue.Should().Be(doc.Total - 35_352.45m);
        var foto = await c.C.Db.DocumentTaxLines.Where(t => t.DocumentId == doc.Id).ToListAsync();
        foto.Should().ContainSingle(t => t.Treatment == TaxTreatment.WithholdingApplied && t.TaxRateCode == "RFCOMP25" && t.Base == minimo);

        var debajo = await c.ConfirmadoAsync(await c.FacturaContraAsync(
            await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P1, 1m, minimo - 1m)])), ComprasDePrueba.Documento("2")));
        c.Documento(debajo.PublicId).WithholdingTotal.Should().Be(0m);
    }

    [Fact]
    public async Task Al_proveedor_autorretenedor_no_se_le_retiene()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.ProveedorA.IsSelfWithholder = true;
        await c.C.Db.SaveChangesAsync();
        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(
            await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P1, 1m, 2_000_000m)])), ComprasDePrueba.Documento()));
        c.Documento(factura.PublicId).WithholdingTotal.Should().Be(0m);
    }

    [Fact]
    public async Task ReteIVA_sobre_el_IVA_si_la_cooperativa_es_agente_de_retencion_de_IVA()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.Parametro(ParametrosTributarios.Modulo, ParametrosTributarios.AgenteRetencionIva, "true");
        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(
            await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P3, 10m, 1_000m)])), ComprasDePrueba.Documento()));

        var doc = c.Documento(factura.PublicId);
        var foto = await c.C.Db.DocumentTaxLines.Where(t => t.DocumentId == doc.Id).ToListAsync();
        foto.Should().Contain(t => t.TaxRateCode == "IVA19" && t.Treatment == TaxTreatment.Deductible && t.Amount == 1_900m);
        foto.Should().Contain(t => t.TaxRateCode == "RIVA15" && t.Treatment == TaxTreatment.WithholdingApplied && t.Base == 1_900m && t.Amount == 285m);
        doc.Total.Should().Be(11_900m);
        doc.AmountDue.Should().Be(11_615m);
    }

    [Fact]
    public async Task ReteICA_con_la_tarifa_general_del_municipio_de_la_operacion()
    {
        var c = await ComprasDePrueba.CrearAsync();
        await c.ReteIcaDeCaliAsync(0.01m);
        c.ProveedorA.CiiuCode = "4711";
        await c.C.Db.SaveChangesAsync();
        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(
            await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P1, 10m, 10_000m)])), ComprasDePrueba.Documento()));

        var doc = c.Documento(factura.PublicId);
        doc.OperationMunicipalityDaneCode.Should().Be("76001");
        var foto = await c.C.Db.DocumentTaxLines.Where(t => t.DocumentId == doc.Id).ToListAsync();
        foto.Should().ContainSingle(t => t.Kind == TaxKind.ReteIca && t.MunicipalityDaneCode == "76001" && t.Amount == 1_000m);
    }

    [Fact]
    public async Task Sin_UVT_vigente_no_se_confirma()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        c.C.Db.PayrollLegalParameters.RemoveRange(c.C.Db.PayrollLegalParameters);
        await c.C.Db.SaveChangesAsync();

        var guardada = await c.GuardarAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento()));
        guardada.Value.Warnings.Should().Contain(w => w.Code == "Taxation.Uvt.Missing");
        (await c.ConfirmarAsync(guardada.Value.PublicId)).Error.Code.Should().Be("Taxation.Uvt.Missing");
    }

    [Fact]
    public async Task Un_impuesto_del_producto_sin_tarifa_vigente_no_se_confirma()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P3, 1m, 1_000m)]));
        var iva19 = c.C.Tarifa("IVA19");
        iva19.ValidTo = new DateOnly(2025, 12, 31);
        await c.C.Db.SaveChangesAsync();

        var guardada = await c.GuardarAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento()));
        guardada.Value.Warnings.Should().Contain(w => w.Code == "Inventory.ProductTax.RateNotInForce");
        var r = await c.ConfirmarAsync(guardada.Value.PublicId);
        r.Error.Code.Should().Be("Inventory.ProductTax.RateNotInForce");
        JsonSerializer.Serialize(CatalogoDePrueba.Datos(r.Error)).Should().Contain("\"lineNumber\":1").And.Contain("\"taxCode\":\"IVA\"");
    }

    // ---------------------------------------------------------------------------------- diferencia de precio (E6) --

    [Fact]
    public async Task Un_precio_distinto_no_se_retiene_y_ajusta_el_costo_de_lo_que_sigue_en_existencia()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento(), precio: 16_200m));

        var doc = c.Documento(factura.PublicId);
        doc.Status.Should().Be(DocumentStatus.Confirmed);
        var ajustes = await c.C.Db.KardexEntries.Where(k => k.DocumentId == doc.Id).ToListAsync();
        ajustes.Should().ContainSingle(k => k.Reason == KardexReason.PriceDifference && k.TotalCost == 1_800m && k.QuantityBase == 0m);
        var estado = await c.C.Db.CostStates.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P1));
        estado.Value.Should().Be(48_600m);
        estado.AverageCost.Should().Be(1_350m);

        c.MensajesDe(factura.PublicId).Should().Equal("FacturaProveedorRegistrada", "AjusteDeCostoReconocido");
        var clave = await c.C.Db.IntegrationMessages.Where(m => m.OriginPublicId == factura.PublicId && m.Type == "AjusteDeCostoReconocido")
            .Select(m => m.OriginEventKey).SingleAsync();
        clave.Should().Be($"Confirmation:{recepcion.PublicId:N}");
        var ajuste = JsonNode.Parse(c.ContenidoDe(factura.PublicId, "AjusteDeCostoReconocido"))!;
        ajuste["affectedDocument"]!["publicId"]!.GetValue<Guid>().Should().Be(recepcion.PublicId);
        ajuste["lines"]![0]!["inventoryAmount"]!.GetValue<decimal>().Should().Be(1_800m);
        ajuste["lines"]![0]!["soldAmount"]!.GetValue<decimal>().Should().Be(0m);
    }

    [Fact]
    public async Task Con_parte_vendida_la_diferencia_se_reparte_entre_existencia_y_vendido()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        var salida = await c.K.AjusteAsync(c.K.Borrador("AJN", causa: c.K.Causa(), lineas: [c.K.Linea(c.P1, 24m)]));
        salida.Confirmacion.IsSuccess.Should().BeTrue(salida.Confirmacion.IsFailure ? salida.Confirmacion.Error.Message : null);

        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento(), precio: 16_200m));
        var ajuste = JsonNode.Parse(c.ContenidoDe(factura.PublicId, "AjusteDeCostoReconocido"))!;
        ajuste["lines"]![0]!["inventoryAmount"]!.GetValue<decimal>().Should().Be(600m);
        ajuste["lines"]![0]!["soldAmount"]!.GetValue<decimal>().Should().Be(1_200m);
        var estado = await c.C.Db.CostStates.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P1));
        estado.Quantity.Should().Be(12m);
        estado.Value.Should().Be(16_200m);
    }

    [Fact]
    public async Task El_mismo_precio_no_deja_ajuste_de_costo()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento()));
        c.MensajesDe(factura.PublicId).Should().Equal("FacturaProveedorRegistrada");
        (await c.C.Db.KardexEntries.CountAsync(k => k.DocumentId == c.Documento(factura.PublicId).Id)).Should().Be(0);
    }

    // ----------------------------------------------------------------------------------------------- el mensaje --

    [Fact]
    public async Task FacturaProveedorRegistrada_cumple_sus_invariantes_y_no_lleva_costo()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var flete = await c.ServicioAsync();
        var recepcion = await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P1, 2m, 710_000m), c.Linea(c.P3, 10m, 1_000m, descuento: 1_000m)]));
        var lineas = await c.LineasAsync(recepcion.PublicId);
        var factura = await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento(), lineas:
        [
            new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, 2m, UnitPrice: 710_000m, ReceiptLinePublicId: lineas[0].PublicId),
            new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, 10m, UnitPrice: 1_000m, DiscountAmount: 1_000m, ReceiptLinePublicId: lineas[1].PublicId),
            c.Linea(flete, 1m, 80_000m),
        ]));

        var m = JsonNode.Parse(c.ContenidoDe(factura.PublicId, "FacturaProveedorRegistrada"))!;
        var totales = m["totals"]!;
        var sumaNeta = m["lines"]!.AsArray().Sum(l => l!["netAmount"]!.GetValue<decimal>());
        sumaNeta.Should().Be(totales["subtotal"]!.GetValue<decimal>() - totales["discountTotal"]!.GetValue<decimal>());
        totales["amountPayable"]!.GetValue<decimal>().Should().Be(totales["total"]!.GetValue<decimal>() - totales["withholdingTotal"]!.GetValue<decimal>());
        totales["withholdingTotal"]!.GetValue<decimal>().Should().BeGreaterThan(0m);
        m["lines"]!.AsArray().Should().Contain(l => l!["lineKind"]!.GetValue<string>() == "Service");
        m["derivedFrom"]!.AsArray().Should().ContainSingle();
        m["supplierDocument"]!["kind"]!.GetValue<string>().Should().Be("Invoice");
        m.ToJsonString().Should().NotContain("\"cost\"");
    }

    // -------------------------------------------------------------------------------------------- eventos RADIAN --

    [Fact]
    public async Task A_credito_nacen_los_dos_eventos_pendientes_y_de_contado_no_aplican()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var flete = await c.ServicioAsync();
        var credito = await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento("C1", credito: true), lineas: [c.Linea(flete, 1m, 1_000m)]));
        var contado = await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento("C2"), lineas: [c.Linea(flete, 1m, 1_000m)]));

        var deCredito = await c.C.Db.SupplierInvoiceEvents.Where(e => e.DocumentId == c.Documento(credito.PublicId).Id).ToListAsync();
        deCredito.Should().HaveCount(2).And.OnlyContain(e => e.Status == SupplierInvoiceEventStatus.Pending);
        var deContado = await c.C.Db.SupplierInvoiceEvents.Where(e => e.DocumentId == c.Documento(contado.PublicId).Id).ToListAsync();
        deContado.Should().HaveCount(2).And.OnlyContain(e => e.Status == SupplierInvoiceEventStatus.NotApplicable);
    }

    [Fact]
    public async Task Con_notas_vigentes_no_se_anula()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento()));
        await c.ConfirmadoAsync(await NotaDeProveedorTests.NotaAsync(c, factura, "NC1", "Credit", valor: 1_000m));

        var r = await c.Anular().Handle(new VoidInventoryDocumentCommand(factura.PublicId, DocumentClassGroup.Purchases, "error"), default);
        r.Error.Code.Should().Be("Inventory.Document.HasDependents");
    }
}
