using FluentAssertions;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// 2026-11-15 (pedido del dueño tras el 07/11 de COOFLOPAL): el precio acompaña a la unidad, las alternas sólo con
/// <c>Productos.VariasUnidades = Permitir</c>, la unidad base por defecto en el alta y volver a «no permitir» se rechaza con alternas.
/// </summary>
public class UnidadesDelProductoTests
{
    private static async Task PermitirAsync(CatalogoDePrueba c, string valor)
    {
        c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeInventario.Modulo, Key = ParametrosDeInventario.ProductosVariasUnidades, ScopeKind = ParameterScopeKind.None,
            Value = valor, ValidFrom = CatalogoDePrueba.Hoy.AddDays(-30), Reason = "prueba",
        });
        await c.Db.SaveChangesAsync();
    }

    private static UnidadesDelProducto Unidades(CatalogoDePrueba c) => new(c.Db, new LectorDeParametros(c.Db));

    private static async Task<PriceList> ListaAsync(CatalogoDePrueba c)
    {
        var lista = new PriceList { Code = "GENERAL", Name = "Lista general", IsActive = true, ValidFrom = CatalogoDePrueba.Hoy.AddDays(-30) };
        c.Db.PriceLists.Add(lista);
        await c.Db.SaveChangesAsync();
        return lista;
    }

    [Fact]
    public async Task Sin_permitir_varias_unidades_no_se_agrega_una_alterna()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());

        var r = await new AddProductUnitCommandHandler(c.Db, Unidades(c), c.Reloj)
            .Handle(new AddProductUnitCommand(p.PublicId, c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both), default);

        r.Error.Code.Should().Be("Inventory.ProductUnit.NotAllowed");
    }

    [Fact]
    public async Task Con_permitir_varias_unidades_si_se_agrega()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await PermitirAsync(c, ParametrosDeInventario.VariasUnidadesPermitir);
        var p = await c.ProductoAsync(c.Alta());

        var r = await new AddProductUnitCommandHandler(c.Db, Unidades(c), c.Reloj)
            .Handle(new AddProductUnitCommand(p.PublicId, c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
    }

    [Fact]
    public async Task El_alta_sin_unidad_toma_la_unidad_base_por_defecto()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var alta = c.Alta() with { BaseUnitPublicId = Guid.Empty };

        var r = await new CreateProductCommandHandler(c.Db, c.Reloj, unidades: Unidades(c)).Handle(alta, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.BaseUnit.Code.Should().Be("UND");
    }

    [Fact]
    public async Task Cambiar_la_unidad_base_lleva_sus_precios_a_la_nueva()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());
        var lista = await ListaAsync(c);
        var producto = await c.Db.Products.SingleAsync(x => x.PublicId == p.PublicId);
        c.Db.PriceListItems.Add(new PriceListItem { PriceListId = lista.Id, ProductId = producto.Id, UnitId = producto.BaseUnitId, Price = 25100m });
        await c.Db.SaveChangesAsync();
        var cto = c.Unidad("DOC");

        await UnidadesDelProducto.MoverPreciosAsync(c.Db, producto.Id, producto.BaseUnitId, cto.Id, default);
        await c.Db.SaveChangesAsync();

        var vivos = await c.Db.PriceListItems.Where(i => i.ProductId == producto.Id && !i.IsDeleted).ToListAsync();
        vivos.Should().ContainSingle().Which.UnitId.Should().Be(cto.Id);
        vivos[0].Price.Should().Be(25100m);
    }

    [Fact]
    public async Task Si_la_lista_ya_tenia_precio_en_la_nueva_unidad_se_retira_el_de_la_anterior()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta());
        var lista = await ListaAsync(c);
        var producto = await c.Db.Products.SingleAsync(x => x.PublicId == p.PublicId);
        var cto = c.Unidad("DOC");
        c.Db.PriceListItems.AddRange(
            new PriceListItem { PriceListId = lista.Id, ProductId = producto.Id, UnitId = producto.BaseUnitId, Price = 25100m },
            new PriceListItem { PriceListId = lista.Id, ProductId = producto.Id, UnitId = cto.Id, Price = 26000m });
        await c.Db.SaveChangesAsync();

        await UnidadesDelProducto.MoverPreciosAsync(c.Db, producto.Id, producto.BaseUnitId, cto.Id, default);
        await c.Db.SaveChangesAsync();

        var vivos = await c.Db.PriceListItems.Where(i => i.ProductId == producto.Id && !i.IsDeleted).ToListAsync();
        vivos.Should().ContainSingle().Which.Price.Should().Be(26000m);
    }

    [Fact]
    public async Task Retirar_una_alterna_retira_sus_precios()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await PermitirAsync(c, ParametrosDeInventario.VariasUnidadesPermitir);
        var p = await c.ProductoAsync(c.Alta(unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both)]));
        var lista = await ListaAsync(c);
        var producto = await c.Db.Products.SingleAsync(x => x.PublicId == p.PublicId);
        var doc = c.Unidad("DOC");
        c.Db.PriceListItems.AddRange(
            new PriceListItem { PriceListId = lista.Id, ProductId = producto.Id, UnitId = producto.BaseUnitId, Price = 2000m },
            new PriceListItem { PriceListId = lista.Id, ProductId = producto.Id, UnitId = doc.Id, Price = 22000m });
        await c.Db.SaveChangesAsync();

        var r = await new RemoveProductUnitCommandHandler(c.Db, c.Reloj).Handle(new RemoveProductUnitCommand(p.PublicId, p.Units.Single().ProductUnitPublicId), default);

        r.IsSuccess.Should().BeTrue();
        var vivos = await c.Db.PriceListItems.Where(i => i.ProductId == producto.Id && !i.IsDeleted).ToListAsync();
        vivos.Should().ContainSingle().Which.UnitId.Should().Be(producto.BaseUnitId);
    }

    [Fact]
    public async Task Dejar_de_permitir_varias_unidades_con_alternas_vivas_se_rechaza_con_la_lista()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await PermitirAsync(c, ParametrosDeInventario.VariasUnidadesPermitir);
        var p = await c.ProductoAsync(c.Alta(unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Both)]));
        var definicion = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, ParametrosDeInventario.ProductosVariasUnidades)!;
        var alta = new AltaDeParametro(definicion, ParameterScopeKind.None, null, null, ParametrosDeInventario.VariasUnidadesNoPermitir,
            CatalogoDePrueba.Hoy, false);

        var r = await new ReglaDeVariasUnidades(c.Db).AplicarAsync(alta, "motivo", null, default);

        r.Error.Code.Should().Be("Inventory.ProductUnit.StillInUse");
        r.Error.Message.Should().Contain(p.Code);
    }
}
