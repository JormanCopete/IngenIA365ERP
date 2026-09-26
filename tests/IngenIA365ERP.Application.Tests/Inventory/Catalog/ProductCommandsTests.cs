using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog.Brands;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Domain.Common.Text;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// Feature 012, T189 (FR-023, FR-025, FR-027, FR-028; contracts/api.md §3.5; data-model §1.6): el alta, la edición, el estado
/// y el borrado del producto por <see cref="ReglasDeProducto"/>. En I1 sólo inventariable y servicio, sin lote ni serie;
/// grupo contable y concepto de retención obligatorios; con movimientos la unidad base y el grupo no cambian por la edición;
/// un producto con historia no se borra (US1-3); el texto de búsqueda se normaliza y sigue a la marca.
/// </summary>
public class ProductCommandsTests
{
    private static ErrorConDatos ConDatos(Error e) => e.Should().BeOfType<ErrorConDatos>().Subject;

    private static UpdateProductCommand Edicion(CatalogoDePrueba c, ProductDto p, Guid? unidadBase = null, Guid? grupo = null, string? nombre = null) =>
        new(p.PublicId, nombre ?? p.Name, p.ShortName, p.Description, p.Category.PublicId, p.Brand?.PublicId, unidadBase ?? p.BaseUnit.PublicId,
            grupo ?? p.AccountingGroup?.PublicId, p.VatSaleTreatment, c.Concepto(), p.Reference, p.Weight, p.Volume, false, false, false);

    [Fact]
    public async Task El_alta_deja_el_producto_activo_con_sus_unidades_codigos_e_impuestos()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var r = await c.CrearProductoAsync(c.Alta(
            "p-1", tratamiento: VatSaleTreatment.Taxed, impuestos: c.Iva19(),
            unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Purchase)],
            codigos: [new CodigoPedido(" 7702001000014 ", null), new CodigoPedido("17702001000011", c.Unidad("DOC").PublicId)]));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var p = r.Value;
        p.Code.Should().Be("P-1");
        p.Status.Should().Be(ProductStatus.Active);
        p.Units.Should().ContainSingle(u => u.Unit.Code == "DOC" && u.Factor == 12m && u.IsDefaultPurchase && !u.IsDefaultSale);
        p.Barcodes.Should().HaveCount(2);
        p.Barcodes.Single(b => b.IsPrimary).Barcode.Should().Be("7702001000014");
        p.Barcodes.Single(b => b.Barcode == "17702001000011").UnitCode.Should().Be("DOC");
        p.Taxes.Should().ContainSingle(t => t.TaxDefinition.Code == "IVA" && t.TaxRate!.Code == "IVA19");
        p.HasMovements.Should().BeFalse();
    }

    [Fact]
    public async Task Un_producto_sin_grupo_contable_se_rechaza()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var r = await c.CrearProductoAsync(c.Alta(conGrupo: false));

        r.Error.Code.Should().Be("Inventory.Product.AccountingGroupRequired");
        (await c.ContarAsync<Domain.Entities.Inventory.Catalog.Product>()).Should().Be(0);
    }

    [Fact]
    public async Task Un_producto_sin_concepto_de_retencion_se_rechaza()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        (await c.CrearProductoAsync(c.Alta(conConcepto: false))).Error.Code.Should().Be("Inventory.Product.WithholdingConceptRequired");
    }

    [Theory]
    [InlineData(ProductKind.Combo)]
    [InlineData(ProductKind.Kit)]
    [InlineData(ProductKind.Template)]
    [InlineData(ProductKind.Variant)]
    public async Task Las_clases_de_I6_no_estan_disponibles(ProductKind clase)
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var r = await c.CrearProductoAsync(c.Alta(clase: clase));

        r.Error.Code.Should().Be("Inventory.Product.KindNotAvailable");
        ConDatos(r.Error).Data.Should().BeEquivalentTo(new { kind = clase.ToString(), availableIn = "I6" });
    }

    [Fact]
    public async Task El_lote_la_serie_y_el_vencimiento_llegan_con_I6()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var r = await c.CrearProductoAsync(c.Alta(lote: true));

        r.Error.Code.Should().Be("Inventory.Product.TrackingNotAvailable");
        ConDatos(r.Error).Data.Should().BeEquivalentTo(new { availableIn = "I6" });
    }

    [Fact]
    public async Task Un_servicio_tambien_lleva_grupo_y_se_crea()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var r = await c.CrearProductoAsync(c.Alta("FLETE", "Servicio de flete", ProductKind.Service, "SRV", tratamiento: VatSaleTreatment.Taxed, impuestos: c.Iva19()));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Kind.Should().Be(ProductKind.Service);
    }

    [Fact]
    public async Task Un_gravado_sin_IVA_se_rechaza()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        (await c.CrearProductoAsync(c.Alta(tratamiento: VatSaleTreatment.Taxed))).Error.Code.Should().Be("Inventory.ProductTax.VatRateRequired");
    }

    [Fact]
    public async Task Con_movimientos_la_unidad_base_no_cambia()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());
        await c.MovimientoAsync(p.PublicId, confirmado: false);
        var editar = new UpdateProductCommandHandler(c.Db, c.Reloj);

        var r = await editar.Handle(Edicion(c, p, unidadBase: c.Unidad("KG").PublicId), default);

        r.Error.Code.Should().Be("Inventory.Product.BaseUnitLocked", "un borrador ya cuenta como movimiento");
    }

    [Fact]
    public async Task Con_movimientos_el_grupo_se_cambia_por_la_reclasificacion()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var otro = new Domain.Entities.Inventory.Catalog.AccountingGroup { Code = "AGRO", Name = "Agroinsumos", IsActive = true };
        c.Db.AccountingGroups.Add(otro);
        await c.Db.SaveChangesAsync();
        var p = await c.ProductoAsync(c.Alta());
        var editar = new UpdateProductCommandHandler(c.Db, c.Reloj);

        var sinMovimientos = await editar.Handle(Edicion(c, p, grupo: otro.PublicId), default);
        await c.MovimientoAsync(p.PublicId);
        var conMovimientos = await editar.Handle(Edicion(c, sinMovimientos.Value, grupo: c.GrupoAbarrotes.PublicId), default);

        sinMovimientos.IsSuccess.Should().BeTrue("sin movimientos el grupo se edita como cualquier dato");
        sinMovimientos.Value.AccountingGroup!.Code.Should().Be("AGRO");
        conMovimientos.Error.Code.Should().Be("Inventory.Product.UseReclassifyAccountingGroup");
    }

    [Fact]
    public async Task Bloquear_exige_motivo_y_el_mismo_estado_se_rechaza()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());
        var cambiar = new SetProductStatusCommandHandler(c.Db, c.Reloj);

        new SetProductStatusCommandValidator().Validate(new SetProductStatusCommand(p.PublicId, ProductStatus.Blocked, "")).IsValid.Should().BeFalse();
        var bloqueado = await cambiar.Handle(new SetProductStatusCommand(p.PublicId, ProductStatus.Blocked, "Lote retirado por el INVIMA"), default);
        var otraVez = await cambiar.Handle(new SetProductStatusCommand(p.PublicId, ProductStatus.Blocked, "Otra vez"), default);

        bloqueado.Value.Status.Should().Be(ProductStatus.Blocked);
        otraVez.Error.Code.Should().Be("Inventory.Product.StatusUnchanged");
    }

    [Fact]
    public async Task Un_producto_con_historia_no_se_borra_y_ofrece_inactivar_o_bloquear()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());
        await c.MovimientoAsync(p.PublicId, confirmado: false);

        var r = await new DeleteProductCommandHandler(c.Db, c.Reloj).Handle(new DeleteProductCommand(p.PublicId), default);

        r.Error.Code.Should().Be("Inventory.Product.HasHistory");
        ConDatos(r.Error).Data.Should().BeEquivalentTo(new { alternatives = new[] { "Inactive", "Blocked" } });
    }

    [Fact]
    public async Task Sin_historia_el_borrado_es_logico_y_libera_sus_codigos_de_barras()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta(codigos: [new CodigoPedido("7702001000014", null)]));

        var r = await new DeleteProductCommandHandler(c.Db, c.Reloj).Handle(new DeleteProductCommand(p.PublicId), default);
        var otro = await c.CrearProductoAsync(c.Alta("P2", codigos: [new CodigoPedido("7702001000014", null)]));

        r.IsSuccess.Should().BeTrue();
        c.Db.Products.IgnoreQueryFilters().Single(x => x.PublicId == p.PublicId).IsDeleted.Should().BeTrue();
        otro.IsSuccess.Should().BeTrue("el código quedó libre");
    }

    [Fact]
    public async Task El_texto_de_busqueda_se_normaliza_sin_tildes_ni_minusculas()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var p = await c.ProductoAsync(c.Alta("caf-01", "Café Águila molido", referencia: "ref-ñandú", marca: c.Diana.PublicId,
            codigos: [new CodigoPedido("7702001000014", null)]));

        var texto = c.Producto(p.PublicId).SearchText;
        texto.Should().Be(NormalizadorDeBusqueda.Normalizar(texto));
        texto.Should().Contain("CAF-01").And.Contain("CAFE AGUILA MOLIDO").And.Contain("REF-ÑANDU").And.Contain("DIANA").And.Contain("7702001000014");
    }

    [Fact]
    public async Task Renombrar_la_marca_recalcula_el_texto_de_busqueda_de_sus_productos()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta(nombre: "Arroz blanco", marca: c.Diana.PublicId));

        var r = await new UpdateBrandCommandHandler(c.Db).Handle(new UpdateBrandCommand(c.Diana.PublicId, "Arroz Roa"), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Products.Should().Be(1);
        c.Producto(p.PublicId).SearchText.Should().Contain("ARROZ ROA").And.NotContain("DIANA");
    }

    [Fact]
    public async Task La_edicion_revisa_el_tratamiento_de_IVA_contra_los_impuestos_que_ya_tiene()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta(tratamiento: VatSaleTreatment.Taxed, impuestos: c.Iva19()));
        var editar = new UpdateProductCommandHandler(c.Db, c.Reloj);

        var exento = Edicion(c, p) with { VatSaleTreatment = VatSaleTreatment.Exempt };
        var r = await editar.Handle(exento, default);

        r.Error.Code.Should().Be("Inventory.ProductTax.VatRateNotAllowed");
    }
}
