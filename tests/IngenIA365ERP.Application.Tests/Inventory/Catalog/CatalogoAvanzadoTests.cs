using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Components;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Catalog.Variants;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Warehouses;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// Feature 012, I6, T907 (US15; FR-023, FR-026; data-model §1.6, §1.11; T918–T920): el catálogo avanzado por la aplicación.
/// Plantillas, combos y kits se crean por el alta de siempre, ya sin <c>Inventory.Product.KindNotAvailable</c>, y una variante sólo
/// nace de su plantilla; los atributos con sus valores; la generación de variantes con lo heredado de la plantilla (grupo contable,
/// impuestos, unidades, seguimiento) y su <c>SearchText</c>, sin repetir combinaciones; una plantilla no entra a documentos y un
/// combo sí a las ventas, sin existencia ni política de reorden; los componentes por <c>SetProductComponentsCommand</c>; y el
/// seguimiento por lote, serie y vencimiento, que ya no responde <c>.TrackingNotAvailable</c>, exige lote para el vencimiento y no
/// cambia con existencia ni con borradores que citen el producto.
/// </summary>
public class CatalogoAvanzadoTests
{
    // ------------------------------------------------------------------------------------------------ auxiliares --

    private static async Task<(CatalogoDePrueba C, VariantAttributeDto Talla, VariantAttributeDto Color)> ConAtributosAsync()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var guardar = new SaveVariantAttributeCommandHandler(c.Db, c.Reloj);
        var talla = await guardar.Handle(new SaveVariantAttributeCommand(null, "talla", "Talla", [new("s", "Pequeña", 1), new("m", "Mediana", 2)]), default);
        var color = await guardar.Handle(new SaveVariantAttributeCommand(null, "COLOR", "Color", [new("AZUL", "Azul"), new("ROJO", "Rojo")]), default);
        talla.IsSuccess.Should().BeTrue(talla.IsFailure ? talla.Error.Message : string.Empty);
        color.IsSuccess.Should().BeTrue(color.IsFailure ? color.Error.Message : string.Empty);
        c.Olvidar();
        return (c, talla.Value, color.Value);
    }

    private static Task<Result<VariantesGeneradasDto>> GenerarAsync(CatalogoDePrueba c, Guid plantilla, IReadOnlyList<AtributoElegido> atributos,
        IReadOnlyList<VarianteAjustada>? ajustes = null) =>
        new GenerateProductVariantsCommandHandler(c.Db).Handle(new GenerateProductVariantsCommand(plantilla, atributos, ajustes), default);

    private static AtributoElegido Todos(VariantAttributeDto a) => new(a.PublicId, a.Values.Select(v => v.PublicId).ToList());

    private static async Task<ProductDto> PlantillaAsync(CatalogoDePrueba c, bool lote = false) =>
        await c.ProductoAsync(c.Alta("CAM", "Camisa", ProductKind.Template, tratamiento: VatSaleTreatment.Taxed, impuestos: c.Iva19(),
            unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Purchase)], lote: lote, marca: c.Diana.PublicId));

    private static UpdateProductCommand Edicion(CatalogoDePrueba c, ProductDto p, bool lote = false, bool serie = false, bool vencimiento = false,
        ProductKind? clase = null) =>
        new(p.PublicId, p.Name, p.ShortName, p.Description, p.Category.PublicId, p.Brand?.PublicId, p.BaseUnit.PublicId,
            p.AccountingGroup?.PublicId, p.VatSaleTreatment, p.WithholdingConcept?.PublicId, p.Reference, p.Weight, p.Volume, lote, serie, vencimiento,
            Kind: clase);

    // ------------------------------------------------------------------------------------------------- las clases --

    [Theory]
    [InlineData(ProductKind.Template)]
    [InlineData(ProductKind.Combo)]
    [InlineData(ProductKind.Kit)]
    public async Task Plantillas_combos_y_kits_se_crean_ya_sin_KindNotAvailable(ProductKind clase)
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var r = await c.CrearProductoAsync(c.Alta("X1", "Producto", clase));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : string.Empty);
        r.Value.Kind.Should().Be(clase);
    }

    [Fact]
    public async Task Una_plantilla_y_un_combo_no_exigen_concepto_de_retencion_y_una_plantilla_tampoco_grupo()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        (await c.CrearProductoAsync(c.Alta("T1", "Plantilla", ProductKind.Template, conGrupo: false, conConcepto: false))).IsSuccess.Should().BeTrue();
        (await c.CrearProductoAsync(c.Alta("C1", "Combo", ProductKind.Combo, conConcepto: false))).IsSuccess.Should().BeTrue();
        (await c.CrearProductoAsync(c.Alta("C2", "Combo", ProductKind.Combo, conGrupo: false))).Error.Code.Should().Be("Inventory.Product.AccountingGroupRequired");
        (await c.CrearProductoAsync(c.Alta("K1", "Kit", ProductKind.Kit, conConcepto: false))).Error.Code.Should().Be("Inventory.Product.WithholdingConceptRequired");
    }

    [Fact]
    public async Task Una_variante_no_nace_por_el_alta_sino_de_su_plantilla()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var r = await c.CrearProductoAsync(c.Alta("V1", "Variante suelta", ProductKind.Variant));

        r.Error.Code.Should().Be("Inventory.Variant.ParentRequired");
        r.Error.Code.Should().NotBe("Inventory.Product.KindNotAvailable");
    }

    [Fact]
    public async Task La_clase_cambia_sin_movimientos_y_no_con_ellos()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta("P1", "Kit de aseo"));
        var editar = new UpdateProductCommandHandler(c.Db, c.Reloj);

        var aKit = await editar.Handle(Edicion(c, p, clase: ProductKind.Kit), default);
        aKit.IsSuccess.Should().BeTrue(aKit.IsFailure ? aKit.Error.Code : string.Empty);
        aKit.Value.Kind.Should().Be(ProductKind.Kit);
        c.Olvidar();

        await c.MovimientoAsync(p.PublicId, confirmado: false);
        var otraVez = await editar.Handle(Edicion(c, aKit.Value, clase: ProductKind.Inventoriable), default);
        otraVez.Error.Code.Should().Be("Inventory.Product.KindLocked");
    }

    // ---------------------------------------------------------------------------------------------- los atributos --

    [Fact]
    public async Task Un_atributo_guarda_sus_valores_en_mayusculas_y_por_orden()
    {
        var (c, talla, color) = await ConAtributosAsync();

        talla.Code.Should().Be("TALLA");
        talla.Values.Select(v => v.Code).Should().Equal("S", "M");
        var lista = await new ListVariantAttributesQueryHandler(c.Db).Handle(new ListVariantAttributesQuery(), default);
        lista.Value.Select(a => a.Code).Should().Equal("COLOR", "TALLA");
        color.VariantsUsing.Should().Be(0);
    }

    [Fact]
    public async Task Un_atributo_no_repite_codigo_ni_valores()
    {
        var (c, _, _) = await ConAtributosAsync();
        var guardar = new SaveVariantAttributeCommandHandler(c.Db, c.Reloj);

        (await guardar.Handle(new SaveVariantAttributeCommand(null, "Talla", "Otra", [new("X", "X")]), default)).Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        (await guardar.Handle(new SaveVariantAttributeCommand(null, "MATERIAL", "Material", [new("ALG", "Algodón"), new("alg", "Otro")]), default))
            .Error.Code.Should().Be("Inventory.VariantAttribute.ValueDuplicate");
    }

    [Fact]
    public async Task Un_valor_que_usa_una_variante_no_se_retira_pero_se_renombra()
    {
        var (c, talla, color) = await ConAtributosAsync();
        var plantilla = await PlantillaAsync(c);
        (await GenerarAsync(c, plantilla.PublicId, [Todos(talla), Todos(color)])).IsSuccess.Should().BeTrue();
        c.Olvidar();
        var guardar = new SaveVariantAttributeCommandHandler(c.Db, c.Reloj);

        var retirar = await guardar.Handle(new SaveVariantAttributeCommand(talla.PublicId, "TALLA", "Talla", [new("S", "Pequeña")]), default);
        retirar.Error.Code.Should().Be("Inventory.VariantAttribute.InUse");
        c.Olvidar();

        var renombrar = await guardar.Handle(new SaveVariantAttributeCommand(talla.PublicId, "TALLA", "Talla de camisa",
            [new("S", "Small", 1), new("M", "Medium", 2), new("L", "Large", 3)]), default);
        renombrar.IsSuccess.Should().BeTrue(renombrar.IsFailure ? renombrar.Error.Message : string.Empty);
        renombrar.Value.Values.Select(v => v.Name).Should().Equal("Small", "Medium", "Large");
        renombrar.Value.VariantsUsing.Should().Be(4);
    }

    // ---------------------------------------------------------------------------------------------- las variantes --

    [Fact]
    public async Task Dos_tallas_por_dos_colores_dan_cuatro_variantes_con_lo_heredado_de_la_plantilla()
    {
        var (c, talla, color) = await ConAtributosAsync();
        var plantilla = await PlantillaAsync(c, lote: true);

        var r = await GenerarAsync(c, plantilla.PublicId, [Todos(talla), Todos(color)],
            [new VarianteAjustada("talla=m;color=azul", Barcode: "7700000000017")]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Created.Select(v => v.VariantKey).Should().BeEquivalentTo(
            "COLOR=AZUL;TALLA=S", "COLOR=ROJO;TALLA=S", "COLOR=AZUL;TALLA=M", "COLOR=ROJO;TALLA=M");
        r.Value.Created.Select(v => v.Code).Should().Contain("CAM-M-AZUL");
        r.Value.Created.Single(v => v.Code == "CAM-M-AZUL").Barcode.Should().Be("7700000000017");
        r.Value.AlreadyExisting.Should().BeEmpty();

        c.Olvidar();
        var variante = await c.Db.Products.Include(p => p.Units).Include(p => p.Taxes).Include(p => p.VariantValues)
            .SingleAsync(p => p.Code == "CAM-M-AZUL");
        var padre = c.Producto(plantilla.PublicId);
        variante.Kind.Should().Be(ProductKind.Variant);
        variante.ParentProductId.Should().Be(padre.Id);
        variante.Name.Should().Be("Camisa Mediana Azul");
        variante.AccountingGroupId.Should().Be(padre.AccountingGroupId);
        variante.WithholdingConceptId.Should().Be(padre.WithholdingConceptId);
        variante.BaseUnitId.Should().Be(padre.BaseUnitId);
        variante.BrandId.Should().Be(c.Diana.Id);
        variante.TracksLot.Should().BeTrue("el seguimiento se hereda de la plantilla");
        variante.VatSaleTreatment.Should().Be(VatSaleTreatment.Taxed);
        variante.Taxes.Should().ContainSingle(t => t.TaxRateCode == "IVA19");
        variante.Units.Should().ContainSingle(u => u.Factor == 12m && u.IsDefaultPurchase);
        variante.VariantValues.Should().HaveCount(2);
        variante.SearchText.Should().Contain("CAM-M-AZUL").And.Contain("CAMISA MEDIANA AZUL").And.Contain("DIANA").And.Contain("7700000000017");
    }

    [Fact]
    public async Task Agregar_un_valor_genera_solo_lo_que_falta_y_lo_que_ya_existe_no_se_repite()
    {
        var (c, talla, color) = await ConAtributosAsync();
        var plantilla = await PlantillaAsync(c);
        (await GenerarAsync(c, plantilla.PublicId, [Todos(talla), Todos(color)])).IsSuccess.Should().BeTrue();
        c.Olvidar();
        var guardar = new SaveVariantAttributeCommandHandler(c.Db, c.Reloj);
        var conL = (await guardar.Handle(new SaveVariantAttributeCommand(talla.PublicId, "TALLA", "Talla",
            [new("S", "Pequeña", 1), new("M", "Mediana", 2), new("L", "Grande", 3)]), default)).Value;
        c.Olvidar();

        var r = await GenerarAsync(c, plantilla.PublicId, [Todos(conL), Todos(color)]);

        r.Value.Created.Select(v => v.VariantKey).Should().BeEquivalentTo("COLOR=AZUL;TALLA=L", "COLOR=ROJO;TALLA=L");
        r.Value.AlreadyExisting.Should().HaveCount(4);
        c.Olvidar();

        var repetida = await GenerarAsync(c, plantilla.PublicId, [Todos(conL), Todos(color)]);
        repetida.Error.Code.Should().Be("Inventory.Variant.CombinationExists");
        (await c.Db.Products.CountAsync(p => p.Kind == ProductKind.Variant)).Should().Be(6);
    }

    [Fact]
    public async Task Solo_una_plantilla_tiene_variantes_y_el_codigo_de_barras_es_unico()
    {
        var (c, talla, color) = await ConAtributosAsync();
        var suelto = await c.ProductoAsync(c.Alta("P1", "Producto", codigos: [new CodigoPedido("7700000000024", null)]));
        var plantilla = await PlantillaAsync(c);

        (await GenerarAsync(c, suelto.PublicId, [Todos(talla)])).Error.Code.Should().Be("Inventory.Variant.NotATemplate");
        (await GenerarAsync(c, plantilla.PublicId, [Todos(talla)], [new VarianteAjustada("TALLA=S", Barcode: "7700000000024")]))
            .Error.Code.Should().Be("Inventory.Barcode.Duplicate");
        (await GenerarAsync(c, plantilla.PublicId, [Todos(talla)], [new VarianteAjustada("TALLA=S", Code: "P1")]))
            .Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        (await c.Db.Products.CountAsync(p => p.Kind == ProductKind.Variant)).Should().Be(0, "todo o nada");
    }

    [Fact]
    public async Task La_lista_de_variantes_de_una_plantilla_trae_sus_valores()
    {
        var (c, talla, color) = await ConAtributosAsync();
        var plantilla = await PlantillaAsync(c);
        await GenerarAsync(c, plantilla.PublicId, [Todos(talla), Todos(color)]);
        c.Olvidar();

        var lista = await new ListProductVariantsQueryHandler(c.Db).Handle(new ListProductVariantsQuery(plantilla.PublicId), default);

        lista.Value.Should().HaveCount(4);
        lista.Value.First(v => v.Code == "CAM-S-ROJO").Values.Select(v => v.ValueCode).Should().Equal("ROJO", "S");
    }

    // ------------------------------------------------------------------------------ plantillas y combos en documentos --

    [Fact]
    public async Task Una_plantilla_no_entra_a_documentos_y_un_combo_si_entra_a_la_venta_sin_existencia()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var plantilla = c.Producto((await PlantillaAsync(c)).PublicId);
        var combo = c.Producto((await c.ProductoAsync(c.Alta("CMB", "Combo desayuno", ProductKind.Combo))).PublicId);
        var maestros = new MaestrosDelDocumentoEnBase(c.Db);

        (await maestros.ProductosAsync([plantilla.PublicId], default)).Single().EsPlantilla.Should().BeTrue();

        InventoryDocument Venta(params int[] productos)
        {
            var d = new InventoryDocument { Class = DocumentClass.SalesInvoice, DocumentTypeId = 1, Prefix = string.Empty, OperationDate = CatalogoDePrueba.Hoy, BranchId = 1 };
            var n = 0;
            foreach (var id in productos) d.Lines.Add(new InventoryDocumentLine { Document = d, LineNumber = ++n, ProductId = id, Quantity = 1m, QuantityBase = 1m });
            return d;
        }

        var conPlantilla = await ReglasDeLineasDeVenta.ProductosAsync(Venta(plantilla.Id), maestros, default);
        conPlantilla.Error.Code.Should().Be("Inventory.Product.NotInventoriable");

        var conCombo = await ReglasDeLineasDeVenta.ProductosAsync(Venta(combo.Id), maestros, default);
        conCombo.IsSuccess.Should().BeTrue();
        conCombo.Value.Should().NotContain(combo.Id, "un combo no tiene existencia propia");
    }

    [Fact]
    public async Task Un_combo_no_tiene_politica_de_reorden()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var tipo = c.Db.WarehouseTypes.First(t => t.Behavior == WarehouseBehavior.Operational);
        var bodega = new Warehouse { Code = "PV1", Name = "Punto 1", BranchId = 1, WarehouseTypeId = tipo.Id };
        c.Db.Warehouses.Add(bodega);
        await c.Db.SaveChangesAsync();
        var combo = await c.ProductoAsync(c.Alta("CMB", "Combo", ProductKind.Combo));
        var alcance = Substitute.For<IAlcanceDeInventario>();
        alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);

        var r = await new SetReorderPolicyCommandHandler(c.Db, alcance).Handle(new SetReorderPolicyCommand(combo.PublicId, bodega.PublicId, 1m, 10m, 2m), default);

        r.Error.Code.Should().Be("Inventory.Product.NotInventoriable");
        c.Producto(combo.PublicId).EsInventariable.Should().BeFalse();
    }

    // -------------------------------------------------------------------------------------------- los componentes --

    [Fact]
    public async Task Los_componentes_de_un_combo_se_reemplazan_y_se_consultan()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var a = await c.ProductoAsync(c.Alta("A", "Café"));
        var b = await c.ProductoAsync(c.Alta("B", "Pan"));
        var d = await c.ProductoAsync(c.Alta("D", "Jugo"));
        var combo = await c.ProductoAsync(c.Alta("CMB", "Desayuno", ProductKind.Combo));
        var fijar = new SetProductComponentsCommandHandler(c.Db, c.Reloj);

        var primera = await fijar.Handle(new SetProductComponentsCommand(combo.PublicId, [new(a.PublicId, 1m), new(b.PublicId, 2m)]), default);
        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? primera.Error.Message : string.Empty);
        c.Olvidar();

        var segunda = await fijar.Handle(new SetProductComponentsCommand(combo.PublicId, [new(b.PublicId, 3m), new(d.PublicId, 1m)]), default);
        segunda.IsSuccess.Should().BeTrue(segunda.IsFailure ? segunda.Error.Message : string.Empty);
        c.Olvidar();

        var consulta = await new GetProductComponentsQueryHandler(c.Db).Handle(new GetProductComponentsQuery(combo.PublicId), default);
        consulta.Value.Components.Select(x => (x.Component.Code, x.Quantity)).Should().Equal(("B", 3m), ("D", 1m));
        (await c.Db.ProductComponents.IgnoreQueryFilters().CountAsync(x => x.IsDeleted)).Should().Be(1, "lo retirado queda de baja lógica");
    }

    [Fact]
    public async Task Un_componente_es_inventariable_o_variante_sin_ciclos_y_con_los_decimales_de_su_unidad()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var a = await c.ProductoAsync(c.Alta("A", "Café"));
        var flete = await c.ProductoAsync(c.Alta("FLETE", "Flete", ProductKind.Service, "SRV"));
        var kit = await c.ProductoAsync(c.Alta("KIT", "Kit", ProductKind.Kit));
        var fijar = new SetProductComponentsCommandHandler(c.Db, c.Reloj);

        var servicio = await fijar.Handle(new SetProductComponentsCommand(kit.PublicId, [new(flete.PublicId, 1m)]), default);
        servicio.Error.Code.Should().Be("Inventory.Component.InvalidKind");

        var ciclo = await fijar.Handle(new SetProductComponentsCommand(kit.PublicId, [new(kit.PublicId, 1m)]), default);
        ciclo.Error.Code.Should().Be("Inventory.Component.Cycle");

        var decimales = await fijar.Handle(new SetProductComponentsCommand(kit.PublicId, [new(a.PublicId, 1.5m)]), default);
        decimales.Error.Code.Should().Be("Inventory.Component.InvalidQuantity", "la unidad UND no admite decimales");

        var sinComponentes = await fijar.Handle(new SetProductComponentsCommand(kit.PublicId, []), default);
        sinComponentes.Error.Code.Should().Be("Inventory.Component.Required");

        var noEsCombo = await fijar.Handle(new SetProductComponentsCommand(a.PublicId, [new(a.PublicId, 1m)]), default);
        noEsCombo.Error.Code.Should().Be("Inventory.Component.NotAComboOrKit");
        noEsCombo.Error.Should().BeOfType<ErrorConDatos>();

        (await c.Db.ProductComponents.CountAsync()).Should().Be(0);
    }

    // ------------------------------------------------------------------------------------------- el seguimiento --

    [Fact]
    public async Task Lote_serie_y_vencimiento_ya_no_responden_TrackingNotAvailable_y_el_vencimiento_exige_lote()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var conLote = await c.CrearProductoAsync(c.Alta("L1", "Con lote", lote: true));
        conLote.IsSuccess.Should().BeTrue(conLote.IsFailure ? conLote.Error.Code : string.Empty);
        var p = await c.ProductoAsync(c.Alta("P1", "Sin seguimiento"));
        var editar = new UpdateProductCommandHandler(c.Db, c.Reloj);

        (await editar.Handle(Edicion(c, p, vencimiento: true), default)).Error.Code.Should().Be("Inventory.Product.ExpiryRequiresLot");
        c.Olvidar();
        var todo = await editar.Handle(Edicion(c, p, lote: true, serie: true, vencimiento: true), default);
        todo.IsSuccess.Should().BeTrue(todo.IsFailure ? todo.Error.Code : string.Empty);
        todo.Value.TracksSerial.Should().BeTrue();

        var servicio = await c.CrearProductoAsync(c.Alta("S1", "Servicio", ProductKind.Service, "SRV", lote: true));
        servicio.Error.Code.Should().Be("Inventory.Product.TrackingNotApplicable");
    }

    [Fact]
    public async Task El_seguimiento_no_cambia_con_borradores_que_citen_el_producto()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta("P1", "Arroz"));
        await c.MovimientoAsync(p.PublicId, confirmado: false);

        var r = await new UpdateProductCommandHandler(c.Db, c.Reloj).Handle(Edicion(c, p, lote: true), default);

        r.Error.Code.Should().Be("Inventory.Product.TrackingLocked");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { hasStock = false, drafts = 1 });
    }

    [Fact]
    public async Task El_seguimiento_no_cambia_con_existencia_y_si_con_existencia_en_cero()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta("P1", "Arroz"));
        await c.MovimientoAsync(p.PublicId, confirmado: true);
        var saldo = new StockBalance { ProductId = c.Producto(p.PublicId).Id, WarehouseId = 1, Physical = 5m };
        c.Db.StockBalances.Add(saldo);
        await c.Db.SaveChangesAsync();
        var editar = new UpdateProductCommandHandler(c.Db, c.Reloj);

        (await editar.Handle(Edicion(c, p, lote: true), default)).Error.Code.Should().Be("Inventory.Product.TrackingLocked");

        c.Olvidar();
        var enCero = await c.Db.StockBalances.SingleAsync();
        enCero.Physical = 0m;
        await c.Db.SaveChangesAsync();
        c.Olvidar();
        var r = await editar.Handle(Edicion(c, p, lote: true), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : string.Empty);
        r.Value.TracksLot.Should().BeTrue();
    }
}
