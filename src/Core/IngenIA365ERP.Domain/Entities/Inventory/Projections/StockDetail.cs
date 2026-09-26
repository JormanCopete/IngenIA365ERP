using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Projections;

/// <summary>
/// La existencia de un producto por bodega, ubicación y lote (<c>INV_StockDetails</c>; feature 012, T249; data-model §3.3):
/// proyección del kardex. Dos únicos filtrados por lote (<c>[LotId] IS NULL</c> / <c>IS NOT NULL</c>), nunca por borrado.
/// Invariante: Σ <see cref="Quantity"/> por (producto, bodega) = <see cref="StockBalance.Physical"/>. La escriben sólo
/// <c>RegistroDeKardex</c> y <c>RebuildInventoryProjectionsCommand</c>. Sin diferencias de auditoría.
/// </summary>
[SinDiffDeAuditoria]
public class StockDetail : AuditableEntity
{
    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public int LocationId { get; set; }

    /// <summary>Sin FK hasta I6 (data-model §3.0); siempre nulo en I1.</summary>
    public int? LotId { get; set; }

    /// <summary>Σ del kardex de la combinación.</summary>
    public decimal Quantity { get; set; }
}
