using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog;

/// <summary>
/// El grupo contable de un producto a una fecha (feature 012, T287; FR-027; data-model §1.10). La regla, en un solo sitio:
/// el <c>To</c> del último <see cref="ProductAccountingGroupChange"/> con <c>EffectiveDate ≤ D</c>; si D es anterior al primer
/// cambio, su <c>From</c>; sin cambios, el grupo actual del producto. Así el valorizado por grupo a una fecha pasada, el
/// cierre de un período y los mensajes de un documento no dependen del grupo de hoy. Lo usan <c>ValuationReportQuery</c>,
/// <c>CloseInventoryPeriodCommand</c> y <c>EmisionDeInventario</c>. (nuevo)
/// </summary>
public static class GrupoContableALaFecha
{
    /// <summary>La regla pura sobre el historial de un producto (en cualquier orden) y su grupo actual.</summary>
    public static int? De(int? actual, IEnumerable<ProductAccountingGroupChange> cambios, DateOnly fecha)
    {
        var ordenados = cambios.OrderBy(c => c.EffectiveDate).ThenBy(c => c.Id).ToList();
        if (ordenados.Count == 0) return actual;
        var ultimo = ordenados.LastOrDefault(c => c.EffectiveDate <= fecha);
        return ultimo?.ToAccountingGroupId ?? ordenados[0].FromAccountingGroupId;
    }

    /// <summary>El grupo a <paramref name="fecha"/> de cada producto (nulo si el producto no tiene grupo ni historial).</summary>
    public static async Task<IReadOnlyDictionary<int, int?>> DeAsync(IApplicationDbContext db, IReadOnlyCollection<int> productos, DateOnly fecha, CancellationToken ct)
    {
        if (productos.Count == 0) return new Dictionary<int, int?>();
        var actuales = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productos.Contains(p.Id))
            .Select(p => new { p.Id, p.AccountingGroupId }).ToDictionaryAsync(p => p.Id, p => p.AccountingGroupId, ct);
        var cambios = (await db.ProductAccountingGroupChanges.AsNoTracking().Where(c => productos.Contains(c.ProductId)).ToListAsync(ct))
            .ToLookup(c => c.ProductId);
        return productos.Distinct().ToDictionary(p => p, p => De(actuales.GetValueOrDefault(p), cambios[p], fecha));
    }

    /// <summary>Como <see cref="DeAsync"/>, pero con el código del grupo (lo que viaja en los mensajes).</summary>
    public static async Task<IReadOnlyDictionary<int, string?>> CodigosAsync(IApplicationDbContext db, IReadOnlyCollection<int> productos, DateOnly fecha, CancellationToken ct)
    {
        var grupos = await DeAsync(db, productos, fecha, ct);
        var ids = grupos.Values.OfType<int>().Distinct().ToList();
        var codigos = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().Where(g => ids.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Code, ct);
        return grupos.ToDictionary(g => g.Key, g => g.Value is int id ? codigos.GetValueOrDefault(id) : null);
    }
}
