using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// Feature 012, T191 (FR-020, T43; contracts/api.md §3.5, «Búsqueda»): la lectura exacta por código de barras de un empaque
/// devuelve el producto con su unidad y factor (US1-5) y por código de producto; mientras se escribe, cada término sin tildes
/// ni mayúsculas, en el orden prefijo de código → prefijo de nombre → resto por nombre; <c>take</c> hasta 50; los inactivos
/// sólo si se piden y los bloqueados con su estado; el disponible sólo con bodega del alcance y <c>Inventory.Stock.View</c>.
/// </summary>
public class SearchProductsQueryTests
{
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly IExistenciasParaElCatalogo _existencias = Substitute.For<IExistenciasParaElCatalogo>();

    public SearchProductsQueryTests()
    {
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private SearchProductsQueryHandler Handler(CatalogoDePrueba c) => new(c.Db, _alcance, _permisos, _existencias);

    [Fact]
    public async Task Leer_el_codigo_de_la_caja_elige_el_producto_con_su_empaque()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p1 = await c.ProductoAsync(c.Alta("P1", unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both)],
            codigos: [new CodigoPedido("7702001000014", null), new CodigoPedido("17702001000011", c.Unidad("DOC").PublicId)]));

        var caja = await Handler(c).Handle(new SearchProductsQuery("17702001000011"), default);
        var unidad = await Handler(c).Handle(new SearchProductsQuery("7702001000014"), default);

        caja.Value.Exact!.PublicId.Should().Be(p1.PublicId);
        caja.Value.Exact.MatchedBarcode.Should().Be("17702001000011");
        caja.Value.Exact.PackUnit.Should().BeEquivalentTo(new PackUnitDto(p1.Units.Single().ProductUnitPublicId, "DOC", 12m));
        unidad.Value.Exact!.PackUnit.Should().BeNull("el código de la unidad base no trae empaque");
    }

    [Fact]
    public async Task La_lectura_exacta_tambien_acierta_por_codigo_de_producto()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta("ARZ-001"));

        var r = await Handler(c).Handle(new SearchProductsQuery("arz-001"), default);

        r.Value.Exact!.PublicId.Should().Be(p.PublicId);
        r.Value.Exact.MatchedBarcode.Should().BeNull();
    }

    [Fact]
    public void Un_caracter_no_alcanza_para_buscar()
    {
        var v = new SearchProductsQueryValidator();
        v.Validate(new SearchProductsQuery("a")).IsValid.Should().BeFalse();
        v.Validate(new SearchProductsQuery(" é ")).IsValid.Should().BeFalse();
        v.Validate(new SearchProductsQuery("ab")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Busca_sin_tildes_ni_mayusculas_y_con_todos_los_terminos()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await c.ProductoAsync(c.Alta("C1", "CAFÉ ÁGUILA ROJA 500 G"));
        await c.ProductoAsync(c.Alta("C2", "Café Sello Rojo"));

        var r = await Handler(c).Handle(new SearchProductsQuery("cafe aguila"), default);

        r.Value.Items.Select(i => i.Code).Should().Equal("C1");
        (await Handler(c).Handle(new SearchProductsQuery("ROJ"), default)).Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Ordena_por_prefijo_de_codigo_luego_prefijo_de_nombre_luego_nombre()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await c.ProductoAsync(c.Alta("Z9", "Aceite de arroz"));
        await c.ProductoAsync(c.Alta("X1", "Arroz Roa"));
        await c.ProductoAsync(c.Alta("ARR-2", "Zapallo"));
        await c.ProductoAsync(c.Alta("Y5", "Arroz Diana"));

        var r = await Handler(c).Handle(new SearchProductsQuery("arr"), default);

        r.Value.Items.Select(i => i.Code).Should().Equal("ARR-2", "Y5", "X1", "Z9");
    }

    [Fact]
    public async Task Devuelve_hasta_cincuenta()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        for (var i = 0; i < 55; i++) await c.ProductoAsync(c.Alta($"GR{i:00}", $"Grano {i:00}"));

        (await Handler(c).Handle(new SearchProductsQuery("grano", Take: 100), default)).Value.Items.Should().HaveCount(50);
        (await Handler(c).Handle(new SearchProductsQuery("grano"), default)).Value.Items.Should().HaveCount(20, "por defecto, 20");
    }

    [Fact]
    public async Task Los_inactivos_solo_si_se_piden_y_los_bloqueados_salen_con_su_estado()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var inactivo = await c.ProductoAsync(c.Alta("I1", "Frijol inactivo"));
        var bloqueado = await c.ProductoAsync(c.Alta("B1", "Frijol bloqueado"));
        c.Producto(inactivo.PublicId).Status = ProductStatus.Inactive;
        c.Producto(bloqueado.PublicId).Status = ProductStatus.Blocked;
        await c.Db.SaveChangesAsync();

        var normal = await Handler(c).Handle(new SearchProductsQuery("frijol"), default);
        var todos = await Handler(c).Handle(new SearchProductsQuery("frijol", IncludeInactive: true), default);

        normal.Value.Items.Should().ContainSingle().Which.Status.Should().Be(ProductStatus.Blocked);
        todos.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task El_disponible_sale_solo_con_bodega_del_alcance_y_permiso_de_existencias()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta("P1", "Panela"));
        var tipo = c.Db.WarehouseTypes.First();
        var bodega = new Warehouse { Code = "PRIN", Name = "Principal", BranchId = 1, WarehouseTypeId = tipo.Id };
        c.Db.Warehouses.Add(bodega);
        await c.Db.SaveChangesAsync();
        _existencias.DisponibleAsync(Arg.Any<IReadOnlyCollection<int>>(), bodega.Id, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, decimal> { [c.Producto(p.PublicId).Id] = 7m });

        var sinBodega = await Handler(c).Handle(new SearchProductsQuery("panela"), default);
        var conBodega = await Handler(c).Handle(new SearchProductsQuery("panela", bodega.PublicId), default);
        _permisos.HasPermissionAsync("Inventory.Stock.View", Arg.Any<CancellationToken>()).Returns(false);
        var sinPermiso = await Handler(c).Handle(new SearchProductsQuery("panela", bodega.PublicId), default);

        sinBodega.Value.Items.Single().Available.Should().BeNull();
        conBodega.Value.Items.Single().Available.Should().Be(7m);
        sinPermiso.Value.Items.Single().Available.Should().BeNull();
    }

    [Fact]
    public async Task Una_bodega_fuera_del_alcance_es_la_misma_que_no_existe()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await c.ProductoAsync(c.Alta("P1", "Panela"));
        var bodega = new Warehouse { Code = "PRIN", Name = "Principal", BranchId = 1, WarehouseTypeId = c.Db.WarehouseTypes.First().Id };
        c.Db.Warehouses.Add(bodega);
        await c.Db.SaveChangesAsync();
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);

        var fuera = await Handler(c).Handle(new SearchProductsQuery("panela", bodega.PublicId), default);
        var inexistente = await Handler(c).Handle(new SearchProductsQuery("panela", Guid.NewGuid()), default);

        fuera.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
        fuera.Error.Should().Be(inexistente.Error);
    }
}
