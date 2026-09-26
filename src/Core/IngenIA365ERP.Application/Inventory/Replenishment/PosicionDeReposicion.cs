using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Replenishment;

/// <summary>La posición de un producto en una bodega (FR-035; contracts/api.md §4.4, §27). (nuevo)</summary>
/// <param name="Disponible">Físico − reservado de <c>INV_StockBalances</c> (el mismo de <c>GetStockQuery</c>).</param>
/// <param name="EnTransito">Despachado hacia la bodega y todavía sin recibir, devolver ni dar de baja.</param>
/// <param name="PorRecibir">Pedido a proveedores sin recibir: 0 hasta I5 (T793).</param>
public sealed record Posicion(decimal Disponible, decimal EnTransito, decimal PorRecibir)
{
    /// <summary>Disponible + en tránsito + por recibir.</summary>
    public decimal Valor => Disponible + EnTransito + PorRecibir;

    public static Posicion Cero { get; } = new(0m, 0m, 0m);
}

/// <summary>Un despacho confirmado hacia una bodega con lo que sigue en tránsito de un producto. (nuevo)</summary>
public sealed record DespachoEnTransito(
    int ProductId, Guid TransferPublicId, string? DispatchNumber, int FromWarehouseId, int ToWarehouseId, decimal Quantity, DateOnly DispatchedOn);

/// <summary>
/// <b>El único lector</b> de la posición de reposición por (producto, bodega) (feature 012, T257; FR-035; contracts/api.md §4.4):
/// disponible, en tránsito hacia la bodega —las líneas de <c>TransferDispatch</c> confirmados con destino en ella, menos lo
/// recibido, devuelto al origen o dado de baja por sus <c>INV_DocumentLineLinks</c> hacia documentos confirmados— y por recibir
/// (0 hasta I5), en lote para varias parejas. No aplica alcance: lo aplica quien la llama. La usan
/// <c>ListReorderPoliciesQuery</c>, <c>GetStockQuery</c>/<c>GetProductStockQuery</c> y, después, <c>AvisoDeReposicionAlConfirmar</c>
/// (T953), <c>RevisionDeReorden</c> (T954) y <c>ReorderAlertsReportQuery</c> (T956): ninguna recalcula la posición por su
/// cuenta. (nuevo)
/// </summary>
public sealed class PosicionDeReposicion(IApplicationDbContext db)
{
    /// <summary>La posición de cada pareja pedida; una pareja sin nada sale en cero.</summary>
    public async Task<IReadOnlyDictionary<(int ProductId, int WarehouseId), Posicion>> LeerAsync(
        IReadOnlyCollection<(int ProductId, int WarehouseId)> parejas, CancellationToken ct)
    {
        var resultado = new Dictionary<(int, int), Posicion>();
        if (parejas.Count == 0) return resultado;

        var productos = parejas.Select(p => p.ProductId).Distinct().ToList();
        var bodegas = parejas.Select(p => p.WarehouseId).Distinct().ToList();

        var disponibles = await db.StockBalances.AsNoTracking()
            .Where(s => productos.Contains(s.ProductId) && bodegas.Contains(s.WarehouseId))
            .Select(s => new { s.ProductId, s.WarehouseId, Disponible = s.Physical - s.Reserved })
            .ToListAsync(ct);
        var porDisponible = disponibles.ToDictionary(d => (d.ProductId, d.WarehouseId), d => d.Disponible);

        var transito = (await EnTransitoAsync(productos, bodegas, ct))
            .GroupBy(d => (d.ProductId, d.ToWarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));

        foreach (var pareja in parejas.Distinct())
        {
            resultado[pareja] = new Posicion(
                porDisponible.GetValueOrDefault(pareja),
                transito.GetValueOrDefault(pareja),
                0m);
        }
        return resultado;
    }

    /// <summary>
    /// Los despachos confirmados con destino en <paramref name="bodegasDestino"/> que todavía tienen algo en tránsito de
    /// <paramref name="productos"/> (lo pendiente de cada despacho, en unidad base). Sin destino, de todas las bodegas.
    /// </summary>
    public async Task<IReadOnlyList<DespachoEnTransito>> EnTransitoAsync(
        IReadOnlyCollection<int> productos, IReadOnlyCollection<int>? bodegasDestino, CancellationToken ct)
    {
        var consulta =
            from l in db.InventoryDocumentLines.AsNoTracking()
            join d in db.InventoryDocuments.AsNoTracking() on l.DocumentId equals d.Id
            where d.Class == DocumentClass.TransferDispatch && d.Status == DocumentStatus.Confirmed && !l.IsDeleted
                  && d.DestinationWarehouseId != null && productos.Contains(l.ProductId)
            select new { LineaId = l.Id, l.ProductId, Destino = d.DestinationWarehouseId!.Value, Origen = d.WarehouseId, l.QuantityBase, d.PublicId, d.Prefix, d.Number, d.OperationDate };
        if (bodegasDestino is not null) consulta = consulta.Where(x => bodegasDestino.Contains(x.Destino));
        var despachos = await consulta.ToListAsync(ct);
        if (despachos.Count == 0) return [];

        var lineas = despachos.Select(d => d.LineaId).ToList();
        var resueltos = await (
                from k in db.DocumentLineLinks.AsNoTracking()
                join t in db.InventoryDocumentLines.AsNoTracking() on k.TargetLineId equals t.Id
                join td in db.InventoryDocuments.AsNoTracking() on t.DocumentId equals td.Id
                where lineas.Contains(k.SourceLineId) && td.Status == DocumentStatus.Confirmed && !k.IsDeleted
                select new { k.SourceLineId, k.QuantityBase })
            .ToListAsync(ct);
        var porLinea = resueltos.GroupBy(r => r.SourceLineId).ToDictionary(g => g.Key, g => g.Sum(r => r.QuantityBase));

        return despachos
            .Select(d => new DespachoEnTransito(d.ProductId, d.PublicId, d.Number is null ? null : $"{d.Prefix}{d.Number}", d.Origen ?? 0, d.Destino,
                d.QuantityBase - porLinea.GetValueOrDefault(d.LineaId), d.OperationDate))
            .Where(d => d.Quantity > 0m)
            .ToList();
    }
}
