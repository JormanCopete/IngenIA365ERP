using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// La compra directa desde el XML del proveedor (decisiones del dueño del 2026-11-14): el emparejamiento de las líneas
/// (<see cref="EquivalenciasDelProveedor"/>) y el control contra el XML en <see cref="ConfirmDirectPurchaseCommand"/>: lo que no cuadra se
/// confirma con una nota aclaratoria (sin ella no se guarda nada), las equivalencias se recuerdan, el producto sin la unidad del proveedor
/// avisa al supervisor y los servicios van sólo a la factura.
/// </summary>
public class CompraDirectaDesdeXmlTests
{
    private const string Codigo = "ABC-1";

    private static ConfirmDirectPurchaseCommandHandler Handler(ComprasDePrueba c) =>
        new(c.C.Db, c.Guardar(), c.Confirmacion(), c.CompraDirecta, c.K.Vista(), c.K.Lector(), c.Alertas);

    /// <summary>3 docenas de P1 a 15.600 (46.800, sin IVA), con el bloque del XML que se le pase.</summary>
    private static ConfirmDirectPurchaseCommand Pedido(ComprasDePrueba c, DirectPurchaseXmlRequest? xml, string numero = "4521",
        IReadOnlyList<SaveInventoryDraftLine>? servicios = null) =>
        new(c.Recepcion(lineas: [c.Linea(c.P1, 3m, 15_600m, c.Docena)]), new DirectPurchaseInvoiceRequest(c.Tipo("FCP"), ComprasDePrueba.Documento(numero)),
            Xml: xml, ServiceLines: servicios);

    private static DirectPurchaseXmlRequest Xml(ComprasDePrueba c, decimal total = 46_800m, decimal iva = 0m, decimal descuento = 0m, string? nota = null,
        bool recordar = true, bool sinUnidad = false) =>
        new(total, iva, descuento, 0m, nota, [new DirectPurchaseXmlLineRequest(c.P1, c.Docena, Codigo, "DZN", recordar, sinUnidad)]);

    // ------------------------------------------------------------------------------------------- la compra --

    [Fact]
    public async Task Sin_diferencias_con_el_XML_confirma_sin_nota_y_recuerda_la_equivalencia()
    {
        var c = await ComprasDePrueba.CrearAsync();

        var r = await Handler(c).Handle(Pedido(c, Xml(c)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var equivalencia = await c.C.Db.SupplierItemCodes.SingleAsync();
        equivalencia.SupplierPersonId.Should().Be(c.ProveedorA.Id);
        equivalencia.ItemCode.Should().Be(Codigo);
        equivalencia.ProductId.Should().Be(c.K.ProductoId(c.P1));
        equivalencia.ProductUnitId.Should().Be(await c.C.Db.ProductUnits.Where(u => u.Unit!.Code == "DOC").Select(u => (int?)u.Id).SingleAsync());
        c.Alertas.Levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_descuento_de_cabecera_sin_nota_aclaratoria_no_guarda_nada()
    {
        var c = await ComprasDePrueba.CrearAsync();

        var r = await Handler(c).Handle(Pedido(c, Xml(c, descuento: 1_000m)), default);

        r.Error.Code.Should().Be(ErroresDeCompras.XmlClarificationRequiredCode);
        r.Error.Message.Should().Contain("descuento de").And.Contain("nota aclaratoria");
        // Que no quede nada guardado lo hace la transacción de la compra (TransaccionExplicita revierte el resultado fallido); en memoria no hay
        // transacción, así que aquí sólo se comprueba lo que la compra hace después del control: ni equivalencias ni alertas.
        (await c.C.Db.SupplierItemCodes.CountAsync()).Should().Be(0);
        c.Alertas.Levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_la_nota_aclaratoria_confirma_y_la_nota_queda_en_la_recepcion_y_en_la_factura()
    {
        var c = await ComprasDePrueba.CrearAsync();

        var r = await Handler(c).Handle(Pedido(c, Xml(c, descuento: 1_000m, nota: "El proveedor dio pronto pago; llega nota crédito")), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        c.Documento(r.Value.Receipt.PublicId).Notes.Should().Contain("Nota aclaratoria: El proveedor dio pronto pago");
        c.Documento(r.Value.SupplierInvoice.PublicId).Notes.Should().Contain("Nota aclaratoria: El proveedor dio pronto pago");
    }

    [Fact]
    public async Task El_IVA_fuera_de_la_tolerancia_pide_la_nota_y_la_tolerancia_es_parametro()
    {
        var c = await ComprasDePrueba.CrearAsync();

        var sinNota = await Handler(c).Handle(Pedido(c, Xml(c, total: 47_300m, iva: 500m)), default);
        sinNota.Error.Code.Should().Be(ErroresDeCompras.XmlClarificationRequiredCode);
        sinNota.Error.Message.Should().Contain("IVA").And.Contain("total antes de retenciones");

        c.Parametro(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComprasToleranciaFrenteAlXml, "600");
        // En memoria no hay transacción que revertir el intento anterior: el segundo va con otro número de factura.
        var dentro = await Handler(c).Handle(Pedido(c, Xml(c, total: 47_300m, iva: 500m), numero: "4522"), default);
        dentro.IsSuccess.Should().BeTrue(dentro.IsFailure ? $"{dentro.Error.Code}: {dentro.Error.Message}" : null);
    }

    [Fact]
    public async Task El_producto_sin_la_unidad_del_proveedor_pide_la_nota_y_avisa_al_supervisor()
    {
        var c = await ComprasDePrueba.CrearAsync();

        var sinNota = await Handler(c).Handle(Pedido(c, Xml(c, sinUnidad: true)), default);
        sinNota.Error.Code.Should().Be(ErroresDeCompras.XmlClarificationRequiredCode);
        c.Alertas.Levantadas.Should().BeEmpty();

        var r = await Handler(c).Handle(Pedido(c, Xml(c, sinUnidad: true, nota: "Lo factura por caja de 12"), numero: "4522"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var alerta = c.Alertas.Levantadas.Should().ContainSingle().Subject;
        alerta.TypeCode.Should().Be(TiposDeAlerta.ProductoPorRevisar);
        alerta.EntityPublicId.Should().Be(c.P1);
        alerta.Body.Should().Contain("«DZN»").And.Contain("Lo factura por caja de 12");
    }

    [Fact]
    public async Task Los_servicios_del_XML_van_solo_a_la_factura()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var flete = await c.ServicioAsync();

        var r = await Handler(c).Handle(Pedido(c, null, servicios: [c.Linea(flete, 1m, 20_000m)]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        (await c.LineasAsync(r.Value.Receipt.PublicId)).Should().ContainSingle();
        (await c.LineasAsync(r.Value.SupplierInvoice.PublicId)).Should().HaveCount(2);
        c.Documento(r.Value.SupplierInvoice.PublicId).Subtotal.Should().Be(66_800m);
    }

    // ------------------------------------------------------------------------------------- el emparejamiento --

    private static LineaLeida Leida(string? codigo, string? barras = null, string? unidad = null) =>
        new(codigo, barras, "Artículo del proveedor", 1m, unidad, 1_000m, 0m, []);

    [Fact]
    public async Task La_equivalencia_gana_al_codigo_de_barras_y_el_codigo_de_barras_da_el_empaque()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var docena = await c.C.Db.ProductUnits.SingleAsync(u => u.Unit!.Code == "DOC");
        c.C.Db.ProductBarcodes.Add(new ProductBarcode { ProductId = c.K.ProductoId(c.P1), Barcode = "7700000000017", ProductUnitId = docena.Id });
        c.C.Db.SupplierItemCodes.Add(new SupplierItemCode { SupplierPersonId = c.ProveedorA.Id, ItemCode = "X-1", ProductId = c.K.ProductoId(c.P3) });
        await c.C.Db.SaveChangesAsync();

        var s = await EquivalenciasDelProveedor.SugerirAsync(c.C.Db, c.ProveedorA.Id,
            [Leida("x-1 ", "7700000000017"), Leida(null, "7700000000017"), Leida("7700000000017")], default);

        s[0]!.ProductPublicId.Should().Be(c.P3);
        s[0]!.MatchedBy.Should().Be(FormasDeEmparejar.Equivalencia);
        s[1]!.ProductPublicId.Should().Be(c.P1);
        s[1]!.UnitPublicId.Should().Be(c.Docena);
        s[1]!.MatchedBy.Should().Be(FormasDeEmparejar.CodigoDeBarras);
        s[2]!.ProductPublicId.Should().Be(c.P1, "el código del proveedor también se busca como código de barras");

        var otroProveedor = await EquivalenciasDelProveedor.SugerirAsync(c.C.Db, c.ProveedorB.Id, [Leida("X-1")], default);
        otroProveedor[0].Should().BeNull("la equivalencia es de un proveedor");
    }

    [Fact]
    public async Task La_referencia_ambigua_no_sugiere_y_la_unidad_se_cruza_con_el_codigo_DIAN()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var p4 = (await c.C.ProductoAsync(c.C.Alta("P4", "Otro"))).PublicId;
        c.C.Db.Products.Single(p => p.PublicId == c.P1).Reference = "REF-1";
        c.C.Db.Products.Single(p => p.PublicId == c.P3).Reference = "REF-9";
        c.C.Db.Products.Single(p => p.PublicId == p4).Reference = "REF-9";
        c.C.Unidad("DOC").DianUnitCode = "DZN";
        await c.C.Db.SaveChangesAsync();

        var s = await EquivalenciasDelProveedor.SugerirAsync(c.C.Db, c.ProveedorA.Id,
            [Leida("REF-9"), Leida("REF-1", unidad: "DZN"), Leida("p3", unidad: "DZN")], default);

        s[0].Should().BeNull("dos productos tienen esa referencia");
        s[1]!.ProductPublicId.Should().Be(c.P1);
        s[1]!.MatchedBy.Should().Be(FormasDeEmparejar.Referencia);
        s[1]!.UnitPublicId.Should().Be(c.Docena);
        s[1]!.UnitMatched.Should().BeTrue();
        s[2]!.ProductPublicId.Should().Be(c.P3);
        s[2]!.MatchedBy.Should().Be(FormasDeEmparejar.CodigoInterno);
        s[2]!.UnitMatched.Should().BeFalse("P3 no tiene la docena");
    }

    [Fact]
    public async Task Recordar_actualiza_la_equivalencia_que_ya_existe()
    {
        var c = await ComprasDePrueba.CrearAsync();
        await EquivalenciasDelProveedor.RecordarAsync(c.C.Db, c.ProveedorA.Id, [new EquivalenciaARecordar(" abc-1", c.K.ProductoId(c.P1), null)], default);
        await c.C.Db.SaveChangesAsync();

        await EquivalenciasDelProveedor.RecordarAsync(c.C.Db, c.ProveedorA.Id, [new EquivalenciaARecordar("ABC-1", c.K.ProductoId(c.P3), null)], default);
        await c.C.Db.SaveChangesAsync();

        var equivalencia = await c.C.Db.SupplierItemCodes.SingleAsync();
        equivalencia.ItemCode.Should().Be("ABC-1");
        equivalencia.ProductId.Should().Be(c.K.ProductoId(c.P3));
    }
}
