using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, T326 (FR-044, FR-049; contracts/api.md §14.2; mensajes.md §6.3): la recepción de compra. Entra al kardex en
/// unidad base al costo de entrada (neto de descuentos más el IVA no descontable), deja la foto de lo que sumó al costo, emite
/// «CompraRecibida» con la remisión y sella el modo de la cadena; municipio desde la sucursal; proveedor obligatorio; con factura
/// no se anula.
/// </summary>
public class RecepcionDeCompraTests
{
    [Fact]
    public async Task Tres_docenas_a_15600_son_36_unidades_a_1300_y_el_documento_muestra_3()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();

        var linea = (await c.LineasAsync(recepcion.PublicId)).Single();
        linea.Quantity.Should().Be(3m);
        linea.Factor.Should().Be(12m);
        linea.QuantityBase.Should().Be(36m);
        linea.UnitCost.Should().Be(1_300m);

        var kardex = await c.C.Db.KardexEntries.Where(k => k.DocumentId == linea.DocumentId).ToListAsync();
        kardex.Should().ContainSingle();
        kardex[0].Kind.Should().Be(KardexEntryKind.Entry);
        kardex[0].QuantityBase.Should().Be(36m);
        kardex[0].UnitCost.Should().Be(1_300m);
        kardex[0].TotalCost.Should().Be(46_800m);

        var documento = c.Documento(recepcion.PublicId);
        documento.Status.Should().Be(DocumentStatus.Confirmed);
        documento.Number.Should().Be(1);
        documento.Prefix.Should().Be("REC");
        documento.PostingMode.Should().Be(PostingMode.Online, "la recepción es la raíz de la cadena Purchases: sella el modo");
        documento.Subtotal.Should().Be(46_800m);
        documento.Total.Should().Be(46_800m);
    }

    [Fact]
    public async Task Emite_CompraRecibida_con_la_remision_y_la_linea_de_entrada_al_costo()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();

        c.MensajesDe(recepcion.PublicId).Should().Equal("CompraRecibida");
        var contenido = JsonNode.Parse(c.ContenidoDe(recepcion.PublicId, "CompraRecibida"))!;
        contenido["operation"]!.GetValue<string>().Should().Be("Compra");
        contenido["supplierDeliveryReference"]!.GetValue<string>().Should().Be("REM-77");
        var lineas = contenido["lines"]!.AsArray();
        lineas.Should().ContainSingle();
        lineas[0]!["accountingGroupCode"]!.GetValue<string>().Should().Be("ABARR");
        lineas[0]!["warehouseCode"]!.GetValue<string>().Should().Be("PRIN");
        lineas[0]!["quantityBase"]!.GetValue<decimal>().Should().Be(36m);
        lineas[0]!["cost"]!.GetValue<decimal>().Should().Be(46_800m);
    }

    [Fact]
    public async Task El_descuento_no_condicionado_baja_el_costo()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P1, 10m, 1_000m, descuento: 500m)]));
        (await c.LineasAsync(recepcion.PublicId)).Single().UnitCost.Should().Be(950m);
    }

    [Fact]
    public async Task Con_la_cooperativa_responsable_el_IVA_es_descontable_y_no_va_al_costo()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P3, 10m, 1_000m)]));

        (await c.LineasAsync(recepcion.PublicId)).Single().UnitCost.Should().Be(1_000m);
        var documento = c.Documento(recepcion.PublicId);
        documento.TaxTotal.Should().Be(1_900m);
        documento.Total.Should().Be(11_900m);
        (await c.C.Db.DocumentTaxLines.CountAsync(t => t.DocumentId == documento.Id)).Should().Be(0, "la recepción sólo guarda lo que fue al costo");
    }

    [Fact]
    public async Task Sin_ser_responsable_de_IVA_el_IVA_va_al_costo_y_queda_su_foto()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.Parametro(ParametrosTributarios.Modulo, ParametrosTributarios.ResponsableIva, "false");
        var recepcion = await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P3, 10m, 1_000m)]));

        (await c.LineasAsync(recepcion.PublicId)).Single().UnitCost.Should().Be(1_190m);
        var documento = c.Documento(recepcion.PublicId);
        var foto = await c.C.Db.DocumentTaxLines.Where(t => t.DocumentId == documento.Id).ToListAsync();
        foto.Should().ContainSingle();
        foto[0].Treatment.Should().Be(TaxTreatment.AddedToCost);
        foto[0].Amount.Should().Be(1_900m);
        foto[0].TaxRateCode.Should().Be("IVA19");
    }

    [Fact]
    public async Task Un_tipo_con_IVA_no_descontable_lo_lleva_al_costo()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.K.Tipo("REC").VatNonDeductible = true;
        await c.C.Db.SaveChangesAsync();
        var recepcion = await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P3, 10m, 1_000m)]));
        (await c.LineasAsync(recepcion.PublicId)).Single().UnitCost.Should().Be(1_190m);
    }

    [Fact]
    public async Task El_municipio_se_propone_desde_la_sucursal_y_uno_inexistente_no_se_admite()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var propuesto = await c.GuardarAsync(c.Recepcion(lineas: [c.Linea(c.P1, 1m, 1_000m)]));
        propuesto.IsSuccess.Should().BeTrue(propuesto.IsFailure ? propuesto.Error.Message : null);
        c.Documento(propuesto.Value.PublicId).OperationMunicipalityDaneCode.Should().Be("76001");

        var otro = await c.GuardarAsync(c.Recepcion(municipio: "76520", lineas: [c.Linea(c.P1, 1m, 1_000m)]));
        c.Documento(otro.Value.PublicId).OperationMunicipalityDaneCode.Should().Be("76520");

        var inexistente = await c.GuardarAsync(c.Recepcion(municipio: "99999", lineas: [c.Linea(c.P1, 1m, 1_000m)]));
        inexistente.IsFailure.Should().BeTrue();
        inexistente.Error.Code.Should().Be("Inventory.Purchase.MunicipalityUnknown");
    }

    [Fact]
    public async Task Sin_proveedor_se_guarda_con_aviso_y_no_se_confirma()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var borrador = c.Recepcion(lineas: [c.Linea(c.P1, 1m, 1_000m)]) with { SupplierPersonPublicId = null };
        var guardado = await c.GuardarAsync(borrador);
        guardado.IsSuccess.Should().BeTrue();
        guardado.Value.Warnings.Should().Contain(w => w.Code == "Inventory.Document.FieldRequired");

        var confirmado = await c.ConfirmarAsync(guardado.Value.PublicId);
        confirmado.IsFailure.Should().BeTrue();
        confirmado.Error.Code.Should().Be("Inventory.Document.FieldRequired");
    }

    [Fact]
    public async Task La_recepcion_no_admite_campos_de_la_factura_ni_costo_digitado()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var conDocumento = await c.GuardarAsync(c.Recepcion(lineas: [c.Linea(c.P1, 1m, 1_000m)]) with { Supplier = ComprasDePrueba.Documento() });
        conDocumento.Error.Code.Should().Be("Validation.Invalid");

        var conCosto = await c.GuardarAsync(c.Recepcion(lineas: [c.Linea(c.P1, 1m, 1_000m) with { UnitCost = 900m }]));
        conCosto.Error.Code.Should().Be("Validation.Invalid");
    }

    [Fact]
    public async Task Los_campos_de_compras_no_aplican_a_un_ajuste()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var ajuste = c.K.Borrador("AJP", lineas: [c.K.Linea(c.P1, 1m, 1_000m)]) with { OperationMunicipalityDaneCode = "76001" };
        var r = await c.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, ajuste), default);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Validation.Invalid");
    }

    [Fact]
    public async Task Con_factura_vigente_la_recepcion_no_se_anula_y_la_nombra()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        var lineaDeRecepcion = (await c.LineasAsync(recepcion.PublicId)).Single();
        var factura = await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento(),
            lineas: [new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, 3m, UnitPrice: 15_600m, ReceiptLinePublicId: lineaDeRecepcion.PublicId)]));

        var anulada = await c.Anular().Handle(new VoidInventoryDocumentCommand(recepcion.PublicId, DocumentClassGroup.Purchases, "error"), default);
        anulada.IsFailure.Should().BeTrue();
        anulada.Error.Code.Should().Be("Inventory.Document.HasDependents");
        System.Text.Json.JsonSerializer.Serialize(CatalogoDePrueba.Datos(anulada.Error)).Should().Contain(factura.PublicId.ToString());
    }

    [Fact]
    public async Task Anulada_primero_la_factura_la_recepcion_si_se_anula_al_costo_con_que_entro()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        var lineaDeRecepcion = (await c.LineasAsync(recepcion.PublicId)).Single();
        var factura = await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento(),
            lineas: [new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, 3m, UnitPrice: 15_600m, ReceiptLinePublicId: lineaDeRecepcion.PublicId)]));

        (await c.Anular().Handle(new VoidInventoryDocumentCommand(factura.PublicId, DocumentClassGroup.Purchases, "mal registrada"), default))
            .IsSuccess.Should().BeTrue();
        c.MensajesDe(c.Documento(factura.PublicId).VoidedByDocumentId is int a ? c.C.Db.InventoryDocuments.Single(d => d.Id == a).PublicId : Guid.Empty)
            .Should().Contain("DocumentoAnulado");

        var anulada = await c.Anular().Handle(new VoidInventoryDocumentCommand(recepcion.PublicId, DocumentClassGroup.Purchases, "error"), default);
        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? anulada.Error.Message : null);
        var existencia = await c.C.Db.StockBalances.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P1) && s.WarehouseId == c.K.Principal.Id);
        existencia.Physical.Should().Be(0m);
    }
}
