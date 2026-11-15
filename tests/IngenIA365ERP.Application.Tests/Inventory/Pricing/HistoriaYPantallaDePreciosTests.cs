using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Pricing;

/// <summary>
/// 2026-11-15 (pedido del dueño): la historia de cada precio de una lista (alta, cambio, retiro, paso de unidad) con su motivo, y las dos
/// consultas de la pantalla de precios —el buscador desde tres caracteres sobre todo el catálogo y el producto con sus precios por unidad y su
/// historia—.
/// </summary>
public class HistoriaYPantallaDePreciosTests
{
    private static async Task<(CatalogoDePrueba C, Guid Lista, ProductDto P1, ProductDto P2)> EscenarioAsync()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p1 = await c.ProductoAsync(c.Alta("ACEITE1", "Aceite Helix 20W50"));
        var p2 = await c.ProductoAsync(c.Alta("ACEITE2", "Aceite Total Ineo"));
        var lista = await new CreatePriceListCommandHandler(c.Db, Substitute.For<ICerrojoPorClave>())
            .Handle(new CreatePriceListCommand("GENERAL", "Lista general", true, null, CatalogoDePrueba.Hoy.AddDays(-30), null, "Lista nueva"), default);
        return (c, lista.Value, p1, p2);
    }

    private static Task<Result<PriceListItemsResultDto>> FijarAsync(CatalogoDePrueba c, Guid lista, Guid producto, decimal precio, string motivo,
        IReadOnlyList<Guid>? quitar = null) =>
        new SetPriceListItemsCommandHandler(c.Db).Handle(new SetPriceListItemsCommand(lista,
            quitar is null ? [new PriceListItemInput(producto, c.Unidad("UND").PublicId, precio)] : [], motivo, quitar), default);

    [Fact]
    public async Task Cada_alta_cambio_y_retiro_deja_su_fila_con_el_motivo()
    {
        var (c, lista, p1, _) = await EscenarioAsync();

        await FijarAsync(c, lista, p1.PublicId, 25100m, "Precio inicial");
        await FijarAsync(c, lista, p1.PublicId, 25100m, "Mismo precio");
        await FijarAsync(c, lista, p1.PublicId, 26000m, "Subió el proveedor");
        await FijarAsync(c, lista, p1.PublicId, 0m, "Se retira", quitar: [p1.PublicId]);

        var historia = await c.Db.PriceListItemHistory.OrderBy(h => h.Id).ToListAsync();
        historia.Select(h => (h.Kind, h.PreviousPrice, h.NewPrice, h.Reason)).Should().Equal(
            (PriceChangeKind.Created, (decimal?)null, (decimal?)25100m, "Precio inicial"),
            (PriceChangeKind.Updated, 25100m, 26000m, "Subió el proveedor"),
            (PriceChangeKind.Removed, 26000m, (decimal?)null, "Se retira"));
    }

    [Fact]
    public async Task Mover_el_precio_a_la_nueva_unidad_base_queda_en_la_historia()
    {
        var (c, lista, p1, _) = await EscenarioAsync();
        await FijarAsync(c, lista, p1.PublicId, 25100m, "Precio inicial");
        var producto = await c.Db.Products.SingleAsync(p => p.PublicId == p1.PublicId);
        var und = producto.BaseUnitId;
        var doc = c.Unidad("DOC").Id;

        await UnidadesDelProducto.MoverPreciosAsync(c.Db, producto.Id, und, doc, default);
        await c.Db.SaveChangesAsync();

        var ultima = await c.Db.PriceListItemHistory.OrderByDescending(h => h.Id).FirstAsync();
        ultima.Kind.Should().Be(PriceChangeKind.UnitChanged);
        ultima.UnitId.Should().Be(doc);
        ultima.PreviousUnitId.Should().Be(und);
        ultima.NewPrice.Should().Be(25100m);
    }

    [Fact]
    public async Task El_buscador_desde_tres_caracteres_trae_productos_con_y_sin_precio_y_sin_texto_solo_los_de_la_lista()
    {
        var (c, lista, p1, _) = await EscenarioAsync();
        await FijarAsync(c, lista, p1.PublicId, 25100m, "Precio inicial");
        var buscar = new SearchPriceListProductsQueryHandler(c.Db);

        var conTexto = (await buscar.Handle(new SearchPriceListProductsQuery(lista, "aceite"), default)).Value;
        var sinTexto = (await buscar.Handle(new SearchPriceListProductsQuery(lista, null), default)).Value;
        var corto = (await buscar.Handle(new SearchPriceListProductsQuery(lista, "ac"), default)).Value;

        conTexto.Items.Select(i => (i.Code, i.BasePrice)).Should().BeEquivalentTo(new[] { ("ACEITE1", (decimal?)25100m), ("ACEITE2", (decimal?)null) });
        sinTexto.Items.Should().ContainSingle().Which.Code.Should().Be("ACEITE1");
        corto.Items.Should().ContainSingle("con menos de tres caracteres se ven los productos de la lista");
    }

    [Fact]
    public async Task El_producto_trae_su_precio_por_unidad_y_su_historia_la_mas_reciente_primero()
    {
        var (c, lista, p1, _) = await EscenarioAsync();
        await FijarAsync(c, lista, p1.PublicId, 25100m, "Precio inicial");
        await FijarAsync(c, lista, p1.PublicId, 26000m, "Subió el proveedor");

        var r = await new GetPriceListProductQueryHandler(c.Db).Handle(new GetPriceListProductQuery(lista, p1.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Units.Should().ContainSingle().Which.Should().Match<PriceListProductUnitDto>(u => u.IsBase && u.Price == 26000m && !u.IsOrphan);
        r.Value.History.Select(h => h.Reason).Should().Equal("Subió el proveedor", "Precio inicial");
    }
}
