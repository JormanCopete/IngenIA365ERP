using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Sales.Promotions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pricing.Promotions;

/// <summary>Lo que el motor necesita saber de un producto (nuevo): su plantilla si es variante y la ruta de su categoría (la suya y sus ancestras).</summary>
public sealed record ProductoParaPromocion(int? ParentProductId, IReadOnlyList<int> Categorias);

/// <summary>
/// Las promociones que pueden aplicar a un documento, con su contexto y los productos (nuevo). Sin promociones vigentes, <see cref="Vacio"/>:
/// la precificación no hace nada más.
/// </summary>
public sealed record PromocionesParaElDocumento(
    IReadOnlyList<PromocionVigente> Promociones,
    ContextoDePromocion Contexto,
    IReadOnlyDictionary<int, ProductoParaPromocion> Productos)
{
    public bool Vacio => Promociones.Count == 0;

    /// <summary>La línea del motor para un producto del documento; la cantidad y el precio van por unidad base, sin impuestos.</summary>
    public LineaDePromocion Linea(int lineNumber, int productId, decimal cantidadBase, decimal precioPorUnidadBase, decimal bruto)
    {
        var producto = Productos.GetValueOrDefault(productId) ?? new ProductoParaPromocion(null, []);
        return new LineaDePromocion(lineNumber, productId, producto.ParentProductId, producto.Categorias, cantidadBase, precioPorUnidadBase, bruto);
    }
}

/// <summary>
/// La carga de las promociones para <see cref="MotorDePromociones"/> (feature 012, I6, T874; FR-055, FR-024; data-model §14
/// «Promociones»): en <b>una sola consulta</b> las promociones vivas, activas y vigentes a la fecha de operación con sus ámbitos y tramos
/// vivos; si no hay ninguna, no pregunta nada más. Si hay, expande cada ámbito de categoría a sus descendientes (la ruta
/// <c>Path</c> de <c>INV_ProductCategories</c>, hasta cinco niveles) para que el motor no tenga que conocer el árbol, resuelve el
/// segmento del cliente (la clase de su asociado vivo, T51) y deja el canal del documento, y trae de cada producto del documento su
/// plantilla y la ruta de su categoría. Sólo lee. (nuevo)
/// </summary>
public sealed class LectorDePromocionesVigentes(IApplicationDbContext db)
{
    public async Task<PromocionesParaElDocumento> LeerAsync(DateOnly fecha, int? compradorPersonId, int? salesChannelId, IReadOnlyCollection<int> productIds,
        RedondeoDeMontos montos, ResiduoDeRedondeo residuo, CancellationToken ct)
    {
        var filas = await db.Promotions.AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive && p.ValidFrom <= fecha && p.ValidTo >= fecha)
            .Include(p => p.Scopes.Where(s => !s.IsDeleted))
            .Include(p => p.Tiers.Where(t => !t.IsDeleted))
            .ToListAsync(ct);
        if (filas.Count == 0)
            return new PromocionesParaElDocumento([], new ContextoDePromocion(fecha, salesChannelId, null, montos, residuo), new Dictionary<int, ProductoParaPromocion>());

        var segmento = await ReglasDeListaDePrecios.SegmentoDeAsync(db, compradorPersonId, ct);
        var rutas = await db.ProductCategories.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.Id, c.Path }).ToListAsync(ct);
        var descendientes = rutas.ToDictionary(c => c.Id,
            c => rutas.Where(o => o.Path.Contains($"/{c.Id}/", StringComparison.Ordinal)).Select(o => o.Id).Append(c.Id).Distinct().ToList());

        var promociones = filas.Select(p => new PromocionVigente(p.Id, p.Code, p.Name, p.Kind, p.Rate, p.Amount, p.BuyQuantity, p.PayQuantity, p.BundlePrice,
                p.IsCumulative, p.ValidFrom, p.ValidTo, p.IsActive,
                p.Scopes.SelectMany(s => s.ScopeKind == PromotionScopeKind.Category && s.ProductCategoryId is int c
                        ? (descendientes.TryGetValue(c, out var ds) ? ds : new List<int> { c }).Select(d => new AmbitoDePromocion(PromotionScopeKind.Category, ProductCategoryId: d))
                        : new[] { new AmbitoDePromocion(s.ScopeKind, s.ProductId, s.ProductCategoryId, s.Segment, s.SalesChannelId, s.RequiredQuantity) })
                    .ToList(),
                p.Tiers.OrderBy(t => t.MinQuantity).Select(t => new TramoDePromocion(t.MinQuantity, t.UnitPrice)).ToList()))
            .ToList();

        var productos = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ParentProductId, p.CategoryId }).ToListAsync(ct);
        var rutaPorCategoria = rutas.ToDictionary(c => c.Id, c => c.Path);
        return new PromocionesParaElDocumento(promociones, new ContextoDePromocion(fecha, salesChannelId, segmento, montos, residuo),
            productos.ToDictionary(p => p.Id, p => new ProductoParaPromocion(p.ParentProductId, RutaDe(rutaPorCategoria.GetValueOrDefault(p.CategoryId), p.CategoryId))));
    }

    /// <summary>La ruta «/1/5/9/» en Id (la categoría y sus ancestras); sin ruta, sólo la categoría.</summary>
    public static IReadOnlyList<int> RutaDe(string? path, int categoryId)
    {
        var ids = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0).Where(id => id > 0).ToList();
        if (!ids.Contains(categoryId)) ids.Add(categoryId);
        return ids;
    }
}
