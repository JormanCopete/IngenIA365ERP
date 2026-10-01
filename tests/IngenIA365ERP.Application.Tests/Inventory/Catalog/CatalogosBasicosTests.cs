using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.AccountingGroups;
using IngenIA365ERP.Application.Inventory.Catalog.AdjustmentCauses;
using IngenIA365ERP.Application.Inventory.Catalog.Brands;
using IngenIA365ERP.Application.Inventory.Catalog.Categories;
using IngenIA365ERP.Application.Inventory.Catalog.SalesChannels;
using IngenIA365ERP.Application.Inventory.Catalog.UnitsOfMeasure;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// Feature 012, T188 (contracts/api.md §3.1–§3.4, §3.7, §3.8, §3.10; data-model §1.1–§1.5, §5.10): los catálogos básicos
/// del inventario. Unidades con decimales que sólo suben si hay cantidades que los usan y código Rec. 20 validado; categorías
/// de hasta 5 niveles (también al mover una rama), sin ciclos y con la ruta reescrita en los descendientes; grupos contables
/// con código inmutable; marcas que se inactivan con productos; canales en uso; la causa que usa el sistema; y el código
/// repetido nombrando al existente. InMemory, reloj fijo.
/// </summary>
public class CatalogosBasicosTests
{
    private static ErrorConDatos ConDatos(Error e) => e.Should().BeOfType<ErrorConDatos>().Subject;

    // ------------------------------------------------------------------------------ unidades --

    [Fact]
    public async Task La_unidad_valida_su_codigo_DIAN_contra_la_Rec20()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var crear = new CreateUnitOfMeasureCommandHandler(c.Db, c.Reloj);

        var mala = await crear.Handle(new CreateUnitOfMeasureCommand("BULTO", "Bulto", "blt", 0, "XYZ"), default);
        var buena = await crear.Handle(new CreateUnitOfMeasureCommand("bulto", "Bulto", "blt", 0, "kgm"), default);

        mala.Error.Code.Should().Be("Inventory.Unit.DianCodeUnknown");
        ConDatos(mala.Error).Data.Should().BeEquivalentTo(new { dianUnitCode = "XYZ" });
        buena.IsSuccess.Should().BeTrue();
        buena.Value.Code.Should().Be("BULTO");
        buena.Value.DianUnitCode.Should().Be("KGM");
    }

    [Fact]
    public async Task Los_decimales_de_una_unidad_usada_no_bajan_de_lo_registrado()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta(unidadBase: "KG"));
        await c.MovimientoAsync(p.PublicId, cantidad: 1.25m, enBase: 1.25m);
        var editar = new UpdateUnitOfMeasureCommandHandler(c.Db, c.Reloj);
        var kg = c.Unidad("KG");

        var baja = await editar.Handle(new UpdateUnitOfMeasureCommand(kg.PublicId, kg.Name, kg.Symbol, 1, kg.DianUnitCode), default);
        var alcanza = await editar.Handle(new UpdateUnitOfMeasureCommand(kg.PublicId, kg.Name, kg.Symbol, 2, kg.DianUnitCode), default);
        var sube = await editar.Handle(new UpdateUnitOfMeasureCommand(kg.PublicId, kg.Name, kg.Symbol, 4, kg.DianUnitCode), default);

        baja.Error.Code.Should().Be("Inventory.Unit.DecimalsInUse");
        ConDatos(baja.Error).Data.Should().BeEquivalentTo(new { maxDecimalsUsed = 2 });
        alcanza.IsSuccess.Should().BeTrue();
        sube.IsSuccess.Should().BeTrue();
        sube.Value.AllowedDecimals.Should().Be(4);
    }

    [Fact]
    public async Task La_unidad_de_un_producto_activo_no_se_inactiva_y_nombra_ejemplos()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await c.ProductoAsync(c.Alta("P1"));
        await c.ProductoAsync(c.Alta("P2", "Frijol"));
        var und = c.Unidad("UND");

        var r = await new SetUnitOfMeasureActiveCommandHandler(c.Db).Handle(new SetUnitOfMeasureActiveCommand(und.PublicId, false, "No se usa"), default);

        r.Error.Code.Should().Be("Inventory.Unit.InUse");
        ConDatos(r.Error).Data.Should().BeEquivalentTo(new { products = 2, examples = new[] { "P1", "P2" } });
    }

    [Fact]
    public async Task Una_unidad_sembrada_no_se_elimina_se_inactiva_con_motivo()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var galon = c.Unidad("GL");
        galon.IsSeeded.Should().BeTrue("la sembró InventoryUnitsSeeder");

        new SetUnitOfMeasureActiveCommandValidator().Validate(new SetUnitOfMeasureActiveCommand(galon.PublicId, false, " ")).IsValid.Should().BeFalse();
        var r = await new SetUnitOfMeasureActiveCommandHandler(c.Db).Handle(new SetUnitOfMeasureActiveCommand(galon.PublicId, false, "No vendemos por galón"), default);

        r.IsSuccess.Should().BeTrue();
        c.Unidad("GL").IsActive.Should().BeFalse();
        c.Unidad("GL").IsDeleted.Should().BeFalse("inactivar no borra");
        var lista = await new ListUnitsOfMeasureQueryHandler(c.Db).Handle(new ListUnitsOfMeasureQuery(), default);
        lista.Value.Should().NotContain(u => u.Code == "GL");
        (await new ListUnitsOfMeasureQueryHandler(c.Db).Handle(new ListUnitsOfMeasureQuery(IncludeInactive: true), default))
            .Value.Should().Contain(u => u.Code == "GL");
    }

    [Fact]
    public async Task La_semilla_de_unidades_es_idempotente_y_lleva_codigos_Rec20()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        (await InventoryUnitsSeeder.AplicarAsync(c.Db, DateTime.UtcNow, null, default)).Should().Be(0, "la versión ya está sembrada");
        c.Unidad("UND").DianUnitCode.Should().Be("94");
        c.Unidad("KG").DianUnitCode.Should().Be("KGM");
        c.Unidad("LT").DianUnitCode.Should().Be("LTR");
        c.Unidad("UND").AllowedDecimals.Should().Be(0);
    }

    // ---------------------------------------------------------------------------- categorías --

    private static async Task<CategoryDto> CategoriaAsync(CatalogoDePrueba c, string codigo, Guid? padre)
    {
        var r = await new CreateProductCategoryCommandHandler(c.Db).Handle(new CreateProductCategoryCommand(codigo, codigo + " nombre", padre), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    [Fact]
    public async Task Las_categorias_tienen_hasta_cinco_niveles()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        Guid? padre = null;
        for (var nivel = 1; nivel <= 5; nivel++) padre = (await CategoriaAsync(c, "N" + nivel, padre)).PublicId;

        var sexta = await new CreateProductCategoryCommandHandler(c.Db).Handle(new CreateProductCategoryCommand("N6", "Sexta", padre), default);

        sexta.Error.Code.Should().Be("Inventory.Category.TooDeep");
        ConDatos(sexta.Error).Data.Should().BeEquivalentTo(new { maxLevel = 5 });
    }

    [Fact]
    public async Task Mover_una_rama_reescribe_ruta_y_nivel_de_los_descendientes()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var granos = await CategoriaAsync(c, "GRANOS", c.Abarrotes.PublicId);
        var arroz = await CategoriaAsync(c, "ARROZ", granos.PublicId);
        var bebidas = await CategoriaAsync(c, "BEBIDAS", null);

        var r = await new UpdateProductCategoryCommandHandler(c.Db).Handle(new UpdateProductCategoryCommand(granos.PublicId, "Granos", bebidas.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var enBase = c.Db.ProductCategories.ToDictionary(x => x.PublicId);
        enBase[arroz.PublicId].Path.Should().StartWith(enBase[bebidas.PublicId].Path, "el descendiente cuelga ahora de la nueva rama");
        enBase[arroz.PublicId].Level.Should().Be(3);
        r.Value.Path.Should().Be("BEBIDAS nombre › Granos");
        var lista = await new ListProductCategoriesQueryHandler(c.Db).Handle(new ListProductCategoriesQuery(), default);
        lista.Value.Single(x => x.Code == "ARROZ").Path.Should().Be("BEBIDAS nombre › Granos › ARROZ nombre");
    }

    [Fact]
    public async Task Mover_una_rama_cuyo_descendiente_pasaria_del_nivel_cinco_se_rechaza()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var a = await CategoriaAsync(c, "A", null);
        var b = await CategoriaAsync(c, "B", a.PublicId);
        await CategoriaAsync(c, "C", b.PublicId);
        var x = await CategoriaAsync(c, "X", null);
        var y = await CategoriaAsync(c, "Y", x.PublicId);
        var z = await CategoriaAsync(c, "Z", y.PublicId);

        var r = await new UpdateProductCategoryCommandHandler(c.Db).Handle(new UpdateProductCategoryCommand(a.PublicId, "A", z.PublicId), default);

        r.Error.Code.Should().Be("Inventory.Category.TooDeep", "A quedaría en 4 y C en 6");
    }

    [Fact]
    public async Task Una_categoria_no_queda_debajo_de_si_misma()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var a = await CategoriaAsync(c, "A", null);
        var b = await CategoriaAsync(c, "B", a.PublicId);
        var editar = new UpdateProductCategoryCommandHandler(c.Db);

        (await editar.Handle(new UpdateProductCategoryCommand(a.PublicId, "A", b.PublicId), default)).Error.Code.Should().Be("Inventory.Category.Cycle");
        (await editar.Handle(new UpdateProductCategoryCommand(a.PublicId, "A", a.PublicId), default)).Error.Code.Should().Be("Inventory.Category.Cycle");
    }

    [Fact]
    public async Task Una_categoria_con_hijas_o_productos_activos_no_se_inactiva()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await CategoriaAsync(c, "GRANOS", c.Abarrotes.PublicId);
        await c.ProductoAsync(c.Alta());

        var r = await new SetProductCategoryActiveCommandHandler(c.Db)
            .Handle(new SetProductCategoryActiveCommand(c.Abarrotes.PublicId, false, "Reorganización"), default);

        r.Error.Code.Should().Be("Inventory.Category.InUse");
        ConDatos(r.Error).Data.Should().BeEquivalentTo(new { children = 1, products = 1 });
    }

    // --------------------------------------------------------------------- grupos y marcas --

    [Fact]
    public async Task El_grupo_contable_no_cambia_de_codigo_y_en_uso_no_se_inactiva()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await c.ProductoAsync(c.Alta());

        var editado = await new UpdateAccountingGroupCommandHandler(c.Db)
            .Handle(new UpdateAccountingGroupCommand(c.GrupoAbarrotes.PublicId, "Abarrotes y granos", "Lo que va a la cuenta 1435"), default);
        var inactivar = await new SetAccountingGroupActiveCommandHandler(c.Db)
            .Handle(new SetAccountingGroupActiveCommand(c.GrupoAbarrotes.PublicId, false, "Se fusiona"), default);

        editado.Value.Code.Should().Be("ABARR", "la matriz contable lo usa: el código no se edita");
        typeof(UpdateAccountingGroupCommand).GetProperty("Code").Should().BeNull("la edición no recibe código");
        inactivar.Error.Code.Should().Be("Inventory.AccountingGroup.InUse");
        ConDatos(inactivar.Error).Data.Should().BeEquivalentTo(new { products = 1 });
    }

    [Fact]
    public async Task Una_marca_con_productos_activos_se_puede_inactivar()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await c.ProductoAsync(c.Alta(marca: c.Diana.PublicId));

        var r = await new SetBrandActiveCommandHandler(c.Db).Handle(new SetBrandActiveCommand(c.Diana.PublicId, false, "Ya no la traemos"), default);

        r.IsSuccess.Should().BeTrue("los productos conservan la marca");
        c.Db.Brands.Single(b => b.Code == "DIANA").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task El_codigo_repetido_nombra_el_existente_con_su_PublicId()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var r = await new CreateBrandCommandHandler(c.Db).Handle(new CreateBrandCommand("diana", "Otra"), default);

        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        ConDatos(r.Error).Data.Should().BeEquivalentTo(new { existingPublicId = c.Diana.PublicId, existingName = "Diana" });
    }

    // -------------------------------------------------------------------- canales y causas --

    [Fact]
    public async Task Un_canal_que_usa_un_tipo_de_documento_activo_no_se_inactiva()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var canal = (await new CreateSalesChannelCommandHandler(c.Db).Handle(new CreateSalesChannelCommand("MOSTRADOR", "Mostrador"), default)).Value;
        var id = c.Db.SalesChannels.Single().Id;
        c.Db.InventoryDocumentTypes.Add(new InventoryDocumentType { Code = "FV", Name = "Factura", Class = DocumentClass.SalesInvoice, SalesChannelId = id, IsActive = true });
        await c.Db.SaveChangesAsync();

        var r = await new SetSalesChannelActiveCommandHandler(c.Db).Handle(new SetSalesChannelActiveCommand(canal.PublicId, false, "Cierra"), default);

        r.Error.Code.Should().Be("Inventory.SalesChannel.InUse");
        r.Error.Message.Should().Contain("FV");
    }

    [Fact]
    public async Task La_causa_diferencia_de_conteo_la_usa_el_sistema_y_no_se_inactiva()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var conteo = c.Db.AdjustmentCauses.Single(x => x.Code == AdjustmentCausesSeeder.CodigoDiferenciaDeConteo);
        var merma = c.Db.AdjustmentCauses.Single(x => x.Code == "MERMA");
        var cambiar = new SetAdjustmentCauseActiveCommandHandler(c.Db);

        var r = await cambiar.Handle(new SetAdjustmentCauseActiveCommand(conteo.PublicId, false, "No la usamos"), default);
        var otra = await cambiar.Handle(new SetAdjustmentCauseActiveCommand(merma.PublicId, false, "No la usamos"), default);

        r.Error.Code.Should().Be("Inventory.AdjustmentCause.RequiredBySystem");
        otra.IsSuccess.Should().BeTrue();
        c.Db.AdjustmentCauses.Count(x => x.IsRequiredBySystem).Should().Be(2, "diferencia de conteo y reclamación al transportador");
    }

    [Fact]
    public async Task Las_consultas_de_los_catalogos_ordenan_por_codigo_y_esconden_los_inactivos()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await new CreateBrandCommandHandler(c.Db).Handle(new CreateBrandCommand("ALPINA", "Alpina"), default);

        var marcas = await new ListBrandsQueryHandler(c.Db).Handle(new ListBrandsQuery(), default);
        var grupos = await new ListAccountingGroupsQueryHandler(c.Db).Handle(new ListAccountingGroupsQuery(), default);
        var causas = await new ListAdjustmentCausesQueryHandler(c.Db).Handle(new ListAdjustmentCausesQuery(), default);

        marcas.Value.Select(m => m.Code).Should().Equal("ALPINA", "DIANA");
        grupos.Value.Should().ContainSingle(g => g.Code == "ABARR");
        causas.Value.Should().HaveCount(7);
    }
}
