using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// Feature 012, T190 (contracts/api.md §3.6.1–§3.6.3; data-model §1.7–§1.9): lo que cuelga del producto. Unidades alternas
/// (no la base, sin repetir, factor fijo con movimientos, no se retiran en uso, una por defecto de cada lado), códigos de
/// barras (únicos entre los vivos nombrando al dueño —US1-4—, borrar libera, el empaque es una alterna del producto) e
/// impuestos (IVA ↔ tratamiento, tarifa de la definición, sin retenciones, unidades en impuestos por unidad, sin repetir;
/// la tarifa se guarda por código).
/// </summary>
public class ProductSubrecursosTests
{
    private static ErrorConDatos ConDatos(Error e) => e.Should().BeOfType<ErrorConDatos>().Subject;

    // ------------------------------------------------------------------------- unidades alternas --

    [Fact]
    public async Task La_unidad_base_no_es_alterna_y_una_alterna_no_se_repite()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());
        var agregar = new AddProductUnitCommandHandler(c.Db);

        var base_ = await agregar.Handle(new AddProductUnitCommand(p.PublicId, c.Unidad("UND").PublicId, 1m, ProductUnitUsage.Both), default);
        var caja = await agregar.Handle(new AddProductUnitCommand(p.PublicId, c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both), default);
        var otraVez = await agregar.Handle(new AddProductUnitCommand(p.PublicId, c.Unidad("DOC").PublicId, 24m, ProductUnitUsage.Sale), default);

        base_.Error.Code.Should().Be("Inventory.ProductUnit.IsBaseUnit");
        caja.IsSuccess.Should().BeTrue();
        otraVez.Error.Code.Should().Be("Inventory.ProductUnit.Duplicate");
    }

    [Fact]
    public void El_factor_es_positivo_con_hasta_seis_decimales()
    {
        var v = new AddProductUnitCommandValidator();
        v.Validate(new AddProductUnitCommand(Guid.NewGuid(), Guid.NewGuid(), 0m, ProductUnitUsage.Both)).IsValid.Should().BeFalse();
        v.Validate(new AddProductUnitCommand(Guid.NewGuid(), Guid.NewGuid(), 0.1234567m, ProductUnitUsage.Both)).IsValid.Should().BeFalse();
        v.Validate(new AddProductUnitCommand(Guid.NewGuid(), Guid.NewGuid(), 0.453592m, ProductUnitUsage.Both)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Con_movimientos_en_la_unidad_su_factor_no_cambia()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta(unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both)]));
        var alterna = p.Units.Single();
        await c.MovimientoAsync(p.PublicId, cantidad: 3m, enBase: 36m, unidad: "DOC");
        var editar = new UpdateProductUnitCommandHandler(c.Db);

        var factor = await editar.Handle(new UpdateProductUnitCommand(p.PublicId, alterna.ProductUnitPublicId, 10m, ProductUnitUsage.Both), default);
        var uso = await editar.Handle(new UpdateProductUnitCommand(p.PublicId, alterna.ProductUnitPublicId, 12m, ProductUnitUsage.Purchase), default);

        factor.Error.Code.Should().Be("Inventory.ProductUnit.FactorLocked");
        uso.IsSuccess.Should().BeTrue("el uso sí cambia");
        uso.Value.Usage.Should().Be(ProductUnitUsage.Purchase);
    }

    [Fact]
    public async Task Una_alterna_en_un_borrador_o_con_codigo_de_barras_vivo_no_se_retira()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta(
            unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both), new UnidadPedida(c.Unidad("PAR").PublicId, 2m, ProductUnitUsage.Sale)],
            codigos: [new CodigoPedido("17702001000011", c.Unidad("DOC").PublicId)]));
        var docena = p.Units.Single(u => u.Unit.Code == "DOC");
        var par = p.Units.Single(u => u.Unit.Code == "PAR");
        await c.MovimientoAsync(p.PublicId, cantidad: 1m, enBase: 2m, unidad: "PAR", confirmado: false);
        var retirar = new RemoveProductUnitCommandHandler(c.Db, c.Reloj);

        var conCodigo = await retirar.Handle(new RemoveProductUnitCommand(p.PublicId, docena.ProductUnitPublicId), default);
        var enBorrador = await retirar.Handle(new RemoveProductUnitCommand(p.PublicId, par.ProductUnitPublicId), default);

        conCodigo.Error.Code.Should().Be("Inventory.ProductUnit.InUse");
        enBorrador.Error.Code.Should().Be("Inventory.ProductUnit.InUse");
    }

    [Fact]
    public async Task Hay_una_sola_unidad_por_defecto_de_compra_y_una_de_venta()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta(unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both)]));

        var paca = await new AddProductUnitCommandHandler(c.Db).Handle(
            new AddProductUnitCommand(p.PublicId, c.Unidad("JGO").PublicId, 25m, ProductUnitUsage.Both, IsDefaultPurchase: true), default);

        paca.Value.IsDefaultPurchase.Should().BeTrue();
        var alternas = c.Db.ProductUnits.Where(u => u.ProductId == c.Producto(p.PublicId).Id).ToList();
        alternas.Count(u => u.IsDefaultPurchase).Should().Be(1, "marcar otra por defecto desmarca la anterior");
        alternas.Count(u => u.IsDefaultSale).Should().Be(1);
        alternas.Single(u => u.IsDefaultSale).Factor.Should().Be(12m, "la de venta sigue siendo la docena");
    }

    // ------------------------------------------------------------------------ códigos de barras --

    [Fact]
    public async Task Un_codigo_que_ya_tiene_otro_producto_se_rechaza_nombrandolo()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p1 = await c.ProductoAsync(c.Alta("P1", "Arroz Diana", codigos: [new CodigoPedido("7702001000014", null)]));
        var p2 = await c.ProductoAsync(c.Alta("P2", "Frijol"));

        var r = await new AddProductBarcodeCommandHandler(c.Db).Handle(new AddProductBarcodeCommand(p2.PublicId, " 7702001000014", null), default);

        r.Error.Code.Should().Be("Inventory.Barcode.Duplicate");
        ConDatos(r.Error).Data.Should().BeEquivalentTo(new { productPublicId = p1.PublicId, productCode = "P1", productName = "Arroz Diana" });
    }

    [Fact]
    public async Task Borrar_un_codigo_lo_deja_libre_para_otro_producto()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p1 = await c.ProductoAsync(c.Alta("P1", codigos: [new CodigoPedido("abc-123", null)]));
        var p2 = await c.ProductoAsync(c.Alta("P2", "Frijol"));
        var codigo = p1.Barcodes.Single();

        (await new RemoveProductBarcodeCommandHandler(c.Db, c.Reloj).Handle(new RemoveProductBarcodeCommand(p1.PublicId, codigo.BarcodePublicId), default))
            .IsSuccess.Should().BeTrue();
        var r = await new AddProductBarcodeCommandHandler(c.Db).Handle(new AddProductBarcodeCommand(p2.PublicId, "ABC-123", null), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Barcode.Should().Be("ABC-123");
        r.Value.IsPrimary.Should().BeTrue();
        c.Producto(p1.PublicId).SearchText.Should().NotContain("ABC-123", "el texto de búsqueda sigue a los códigos vivos");
        c.Producto(p2.PublicId).SearchText.Should().Contain("ABC-123");
    }

    [Fact]
    public async Task El_empaque_de_un_codigo_es_una_unidad_alterna_del_mismo_producto()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p1 = await c.ProductoAsync(c.Alta("P1", unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both)]));
        var p2 = await c.ProductoAsync(c.Alta("P2", "Frijol"));
        var agregar = new AddProductBarcodeCommandHandler(c.Db);

        var ajeno = await agregar.Handle(new AddProductBarcodeCommand(p2.PublicId, "999", p1.Units.Single().ProductUnitPublicId), default);
        var propio = await agregar.Handle(new AddProductBarcodeCommand(p1.PublicId, "999", p1.Units.Single().ProductUnitPublicId), default);

        ajeno.Error.Code.Should().Be("Inventory.ProductUnit.NotFound");
        propio.Value.UnitCode.Should().Be("DOC");
    }

    [Fact]
    public void El_codigo_de_barras_no_admite_espacios_ni_mas_de_48_caracteres()
    {
        var v = new AddProductBarcodeCommandValidator();
        v.Validate(new AddProductBarcodeCommand(Guid.NewGuid(), "770 200", null)).IsValid.Should().BeFalse();
        v.Validate(new AddProductBarcodeCommand(Guid.NewGuid(), new string('1', 49), null)).IsValid.Should().BeFalse();
        v.Validate(new AddProductBarcodeCommand(Guid.NewGuid(), "7702001000014", null)).IsValid.Should().BeTrue();
    }

    // -------------------------------------------------------------------------------- impuestos --

    private static Task<Result<ProductTaxesDto>> ImpuestosAsync(CatalogoDePrueba c, ProductDto p, VatSaleTreatment tratamiento, params ImpuestoPedido[] impuestos) =>
        new SetProductTaxesCommandHandler(c.Db, c.Reloj).Handle(new SetProductTaxesCommand(p.PublicId, tratamiento, c.Concepto(), impuestos), default);

    [Fact]
    public async Task El_tratamiento_de_IVA_manda_sobre_la_tarifa()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());
        var iva = c.Impuesto("IVA").PublicId;

        (await ImpuestosAsync(c, p, VatSaleTreatment.Taxed)).Error.Code.Should().Be("Inventory.ProductTax.VatRateRequired");
        (await ImpuestosAsync(c, p, VatSaleTreatment.Taxed, new ImpuestoPedido(iva, null, null))).Error.Code
            .Should().Be("Inventory.ProductTax.VatRateRequired", "gravado lleva la tarifa");
        (await ImpuestosAsync(c, p, VatSaleTreatment.Exempt, new ImpuestoPedido(iva, c.Tarifa("IVA19").PublicId, null))).Error.Code
            .Should().Be("Inventory.ProductTax.VatRateNotAllowed");
        (await ImpuestosAsync(c, p, VatSaleTreatment.Exempt, new ImpuestoPedido(iva, c.Tarifa("IVAEXE").PublicId, null))).IsSuccess
            .Should().BeTrue("el exento puede llevar la tarifa en cero");
    }

    [Fact]
    public async Task La_tarifa_es_de_la_definicion_y_una_retencion_no_es_impuesto_del_producto()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());

        var ajena = await ImpuestosAsync(c, p, VatSaleTreatment.Taxed, new ImpuestoPedido(c.Impuesto("IVA").PublicId, c.Tarifa("INC8").PublicId, null));
        var retencion = await ImpuestosAsync(c, p, VatSaleTreatment.Excluded, new ImpuestoPedido(c.Impuesto("RETEFUENTE").PublicId, null, null));

        ajena.Error.Code.Should().Be("Inventory.ProductTax.RateNotOfDefinition");
        retencion.Error.Code.Should().Be("Inventory.ProductTax.WithholdingNotAllowed");
    }

    [Fact]
    public async Task Un_impuesto_por_unidad_pide_unidades_gravables_y_ninguno_se_repite()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());
        var bolsa = c.Impuesto("BOLSAS").PublicId;

        (await ImpuestosAsync(c, p, VatSaleTreatment.Excluded, new ImpuestoPedido(bolsa, c.Tarifa("BOLSA").PublicId, null))).Error.Code
            .Should().Be("Inventory.ProductTax.UnitsRequired");
        (await ImpuestosAsync(c, p, VatSaleTreatment.Excluded,
                new ImpuestoPedido(bolsa, c.Tarifa("BOLSA").PublicId, 1m), new ImpuestoPedido(bolsa, null, 1m))).Error.Code
            .Should().Be("Inventory.ProductTax.Duplicate");
    }

    [Fact]
    public async Task La_tarifa_se_guarda_por_su_codigo_y_el_conjunto_se_reemplaza()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta(tratamiento: VatSaleTreatment.Taxed, impuestos: c.Iva19()));

        var r = await ImpuestosAsync(c, p, VatSaleTreatment.Taxed,
            new ImpuestoPedido(c.Impuesto("IVA").PublicId, c.Tarifa("IVA5").PublicId, null),
            new ImpuestoPedido(c.Impuesto("BOLSAS").PublicId, c.Tarifa("BOLSA").PublicId, 1m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var filas = c.Db.ProductTaxes.Where(t => t.ProductId == c.Producto(p.PublicId).Id).ToList();
        filas.Should().HaveCount(2, "el IVA 19 se reemplazó por el 5 en la misma fila del IVA");
        filas.Single(t => t.TaxDefinitionId == c.Impuesto("IVA").Id).TaxRateCode.Should().Be("IVA5");
        typeof(Domain.Entities.Inventory.Catalog.ProductTax).GetProperty("TaxRateId").Should().BeNull("se guarda el código, no la fila de la vigencia");
        r.Value.Taxes.Select(t => t.TaxRate!.Code).Should().BeEquivalentTo("IVA5", "BOLSA");
    }
}
