using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// Feature 022 (T021; FR-030, plan §8.2): la búsqueda de productos con el precio que aplica. Con contexto, cada resultado trae sus
/// unidades de venta; con <c>Inventory.Prices.View</c>, el precio de la base en la lista que gana y el de cada empaque que esa lista
/// trae (exacto por producto y unidad). Sin permiso, sin precio y sin error. Un contexto a la vez.
/// </summary>
public class SearchProductsWithPriceTests
{
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly IExistenciasParaElCatalogo _existencias = Substitute.For<IExistenciasParaElCatalogo>();

    public SearchProductsWithPriceTests()
    {
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private SearchProductsQueryHandler Handler(CatalogoDePrueba c) => new(c.Db, _alcance, _permisos, _existencias, c.Reloj);

    private static async Task<PriceList> ListaAsync(CatalogoDePrueba c, string codigo, int? persona = null, bool conImpuestos = false)
    {
        var lista = new PriceList { Code = codigo, Name = codigo, ValidFrom = CatalogoDePrueba.Hoy.AddDays(-30), IsActive = true, IncludesTaxes = conImpuestos };
        lista.FijarAmbito(persona, null, null, null);
        c.Db.PriceLists.Add(lista);
        await c.Db.SaveChangesAsync();
        return lista;
    }

    private static async Task PrecioAsync(CatalogoDePrueba c, PriceList lista, Guid producto, string unidad, decimal precio)
    {
        var p = c.Producto(producto);
        c.Db.PriceListItems.Add(new PriceListItem { PriceListId = lista.Id, ProductId = p.Id, UnitId = c.Unidad(unidad).Id, Price = precio });
        await c.Db.SaveChangesAsync();
    }

    private static async Task<(CatalogoDePrueba C, Guid Arroz)> EscenarioAsync()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var arroz = await c.ProductoAsync(c.Alta("ARZ", "Arroz Diana",
            unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both)]));
        return (c, arroz.PublicId);
    }

    [Fact]
    public async Task Con_contexto_y_permiso_trae_el_precio_de_la_base_y_el_del_empaque_que_la_lista_tiene()
    {
        var (c, arroz) = await EscenarioAsync();
        var general = await ListaAsync(c, "GENERAL", conImpuestos: true);
        await PrecioAsync(c, general, arroz, "UND", 2_500m);
        await PrecioAsync(c, general, arroz, "DOC", 28_000m);

        var r = await Handler(c).Handle(new SearchProductsQuery("arroz") { PriceDate = CatalogoDePrueba.Hoy }, default);

        var item = r.Value.Items.Single();
        item.Price.Should().Be(2_500m);
        item.PriceIncludesTaxes.Should().BeTrue();
        item.PriceListCode.Should().Be("GENERAL");
        item.Units!.Select(u => (u.UnitCode, u.Factor, u.IsBase, u.Price)).Should().Equal(("UND", 1m, true, 2_500m), ("DOC", 12m, false, 28_000m));
        item.Units![1].ProductUnitPublicId.Should().NotBeNull();
    }

    [Fact]
    public async Task El_empaque_sin_precio_propio_en_la_lista_ganadora_sale_sin_precio()
    {
        var (c, arroz) = await EscenarioAsync();
        var general = await ListaAsync(c, "GENERAL");
        await PrecioAsync(c, general, arroz, "UND", 2_500m);

        var r = await Handler(c).Handle(new SearchProductsQuery("arroz") { PriceDate = CatalogoDePrueba.Hoy }, default);

        r.Value.Items.Single().Units!.Single(u => !u.IsBase).Price.Should().BeNull("no se multiplica el precio de la base por el factor");
    }

    [Fact]
    public async Task Sin_permiso_de_precios_trae_las_unidades_sin_precio()
    {
        var (c, arroz) = await EscenarioAsync();
        await PrecioAsync(c, await ListaAsync(c, "GENERAL"), arroz, "UND", 2_500m);
        _permisos.HasPermissionAsync(PrecioEnLaBusqueda.VerPrecios, Arg.Any<CancellationToken>()).Returns(false);

        var r = await Handler(c).Handle(new SearchProductsQuery("arroz") { PriceDate = CatalogoDePrueba.Hoy }, default);

        var item = r.Value.Items.Single();
        item.Price.Should().BeNull();
        item.PriceListCode.Should().BeNull();
        item.Units!.Should().HaveCount(2).And.OnlyContain(u => u.Price == null);
    }

    [Fact]
    public async Task Sin_contexto_no_trae_precio_ni_unidades()
    {
        var (c, arroz) = await EscenarioAsync();
        await PrecioAsync(c, await ListaAsync(c, "GENERAL"), arroz, "UND", 2_500m);

        var item = (await Handler(c).Handle(new SearchProductsQuery("arroz"), default)).Value.Items.Single();

        item.Price.Should().BeNull();
        item.Units.Should().BeNull();
    }

    [Fact]
    public async Task La_lista_del_cliente_gana_a_la_general()
    {
        var (c, arroz) = await EscenarioAsync();
        var cliente = new Person { IdType = "C", TaxId = "900", FirstName = "Tienda", LastName = "Uno", Status = "A" };
        c.Db.People.Add(cliente);
        await c.Db.SaveChangesAsync();
        await PrecioAsync(c, await ListaAsync(c, "GENERAL"), arroz, "UND", 2_500m);
        await PrecioAsync(c, await ListaAsync(c, "TIENDA1", cliente.Id), arroz, "UND", 2_300m);

        var general = await Handler(c).Handle(new SearchProductsQuery("arroz") { PriceDate = CatalogoDePrueba.Hoy }, default);
        var delCliente = await Handler(c).Handle(new SearchProductsQuery("arroz") { PriceCustomer = cliente.PublicId }, default);

        general.Value.Items.Single().PriceListCode.Should().Be("GENERAL");
        delCliente.Value.Items.Single().Should().Match<ProductSearchItemDto>(i => i.Price == 2_300m && i.PriceListCode == "TIENDA1");
    }

    [Fact]
    public async Task Una_referencia_del_contexto_que_no_existe_es_404()
    {
        var (c, _) = await EscenarioAsync();

        var cliente = await Handler(c).Handle(new SearchProductsQuery("arroz") { PriceCustomer = Guid.NewGuid() }, default);
        var borrador = await Handler(c).Handle(new SearchProductsQuery("arroz") { PriceDraft = Guid.NewGuid() }, default);
        var sesion = await Handler(c).Handle(new SearchProductsQuery("arroz") { PriceCashSession = Guid.NewGuid() }, default);

        cliente.Error.Code.Should().Be("Inventory.PriceContext.Customer.NotFound");
        borrador.Error.Code.Should().Be("Inventory.PriceContext.Draft.NotFound");
        sesion.Error.Code.Should().Be("Inventory.PriceContext.CashSession.NotFound");
    }

    [Fact]
    public void Un_solo_contexto_de_precio_a_la_vez()
    {
        var v = new SearchProductsQueryValidator();

        v.Validate(new SearchProductsQuery("arroz") { PriceDraft = Guid.NewGuid(), PriceCustomer = Guid.NewGuid() }).IsValid.Should().BeFalse();
        v.Validate(new SearchProductsQuery("arroz") { PriceDraft = Guid.NewGuid(), PriceCashSession = Guid.NewGuid() }).IsValid.Should().BeFalse();
        v.Validate(new SearchProductsQuery("arroz") { PriceCustomer = Guid.NewGuid(), PriceWarehouse = Guid.NewGuid(), PriceDate = CatalogoDePrueba.Hoy })
            .IsValid.Should().BeTrue();
    }
}
