using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>Un componente de un combo o de un kit: el producto y cuánto lleva por unidad, en su unidad base. (nuevo)</summary>
public sealed record ComponenteDelCompuesto(int ProductId, decimal Quantity);

/// <summary>
/// El combo y el kit en las estrategias (feature 012, I6, T926, T928; US15-2, US15-3; data-model §1.11): los componentes vivos de
/// <c>INV_ProductComponents</c> y las salidas de un combo vendido —una por componente, cantidad × <c>ProductComponent.Quantity</c> en la
/// bodega y la ubicación de la línea, al promedio de su ámbito—. El combo no tiene kardex propio: la línea del documento es la del
/// combo (su precio y su <c>VentaFacturada</c> por el grupo del combo) y las filas del kardex son de sus componentes (su
/// <c>CostoDeVentaReconocido</c> por el grupo de cada componente). El cambio de componentes rige para lo que se venda o ensamble
/// después: cada confirmación lee los vigentes. (nuevo)
/// </summary>
public static class ExpansionDeCombos
{
    /// <summary>Los componentes vivos de cada producto de la clase pedida (<see cref="ProductKind.Combo"/> o <see cref="ProductKind.Kit"/>).</summary>
    public static async Task<IReadOnlyDictionary<int, IReadOnlyList<ComponenteDelCompuesto>>> ComponentesAsync(IApplicationDbContext db,
        IEnumerable<int> productos, ProductKind clase, CancellationToken ct)
    {
        var ids = productos.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, IReadOnlyList<ComponenteDelCompuesto>>();
        var compuestos = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.Kind == clase).Select(p => p.Id).ToListAsync(ct);
        if (compuestos.Count == 0) return new Dictionary<int, IReadOnlyList<ComponenteDelCompuesto>>();
        var filas = await db.ProductComponents.AsNoTracking()
            .Where(c => compuestos.Contains(c.ProductId))
            .OrderBy(c => c.ComponentProductId)
            .Select(c => new { c.ProductId, c.ComponentProductId, c.Quantity })
            .ToListAsync(ct);
        return compuestos.ToDictionary(id => id, id => (IReadOnlyList<ComponenteDelCompuesto>)filas
            .Where(f => f.ProductId == id).Select(f => new ComponenteDelCompuesto(f.ComponentProductId, f.Quantity)).ToList());
    }

    /// <summary>
    /// El disponible de cada combo en la bodega (I6, T927): el mínimo, entre sus componentes, de (disponible del componente ÷ cantidad
    /// por combo) redondeado hacia abajo —combos enteros—; un combo sin componentes, cero. El disponible de un componente es físico −
    /// reservado. Los productos que no son combo no vienen.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, decimal>> DisponibleDeCombosAsync(IApplicationDbContext db, IReadOnlyCollection<int> productos,
        int bodegaId, CancellationToken ct)
    {
        var combos = await ComponentesAsync(db, productos, ProductKind.Combo, ct);
        if (combos.Count == 0) return new Dictionary<int, decimal>();
        var componentes = combos.Values.SelectMany(c => c).Select(c => c.ProductId).Distinct().ToList();
        var disponibles = await db.StockBalances.AsNoTracking().Where(s => s.WarehouseId == bodegaId && componentes.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, s => s.Physical - s.Reserved, ct);
        return combos.ToDictionary(c => c.Key, c => c.Value.Count == 0
            ? 0m
            : Math.Max(0m, c.Value.Min(x => x.Quantity <= 0m ? 0m : Math.Floor(disponibles.GetValueOrDefault(x.ProductId) / x.Quantity))));
    }

    /// <summary>La cantidad de un componente para <paramref name="unidades"/> del compuesto, a los 4 decimales de la cantidad.</summary>
    public static decimal Cantidad(decimal unidades, ComponenteDelCompuesto componente) =>
        Math.Round(unidades * componente.Quantity, 4, MidpointRounding.AwayFromZero);

    /// <summary>Las salidas de un combo vendido: una por componente, al costo vigente de su ámbito.</summary>
    public static IEnumerable<MovimientoDeKardex> Salidas(InventoryDocumentLine linea, int bodega, IReadOnlyList<ComponenteDelCompuesto> componentes) =>
        componentes.Select(c => (c, q: Cantidad(linea.QuantityBase, c))).Where(x => x.q > 0m)
            .Select(x => new MovimientoDeKardex(linea, bodega, -x.q, ValoracionDelMovimiento.AlCostoVigente, LocationId: linea.LocationId)
            {
                ProductId = x.c.ProductId,
            });

    /// <summary>
    /// Las salidas de una venta: una por línea viva de producto inventariable y, por cada combo, una por componente (T926). Los servicios y
    /// las plantillas no mueven existencia.
    /// </summary>
    public static async Task<IReadOnlyList<MovimientoDeKardex>> SalidasDeVentaAsync(IApplicationDbContext db, InventoryDocument documento,
        IReadOnlySet<int> inventariables, CancellationToken ct)
    {
        if (documento.WarehouseId is not int bodega) return [];
        var vivas = documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber).ToList();
        var combos = await ComponentesAsync(db, vivas.Where(l => !inventariables.Contains(l.ProductId)).Select(l => l.ProductId), ProductKind.Combo, ct);
        var movimientos = new List<MovimientoDeKardex>();
        foreach (var linea in vivas)
        {
            if (inventariables.Contains(linea.ProductId))
                movimientos.Add(new MovimientoDeKardex(linea, bodega, -linea.QuantityBase, ValoracionDelMovimiento.AlCostoVigente, LocationId: linea.LocationId));
            else if (combos.TryGetValue(linea.ProductId, out var componentes))
                movimientos.AddRange(Salidas(linea, bodega, componentes));
        }
        return movimientos;
    }
}
