using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Projections;

/// <summary>
/// La existencia de un producto en una bodega (<c>INV_StockBalances</c>; feature 012, T249; FR-003, FR-004, FR-033;
/// data-model §3.2): una <b>proyección</b> del kardex, reconstruible, que nunca se da de baja (su único
/// <c>(ProductId, WarehouseId)</c> va sin filtro para que el cerrojo la cree con <c>INSERT … ON CONFLICT</c>). La escriben
/// sólo <c>RegistroDeKardex</c> y <c>RebuildInventoryProjectionsCommand</c>. Disponible = <see cref="Physical"/> −
/// <see cref="Reserved"/> (no se guarda). Fila exclusiva en el cerrojo. Sin diferencias de auditoría.
/// </summary>
[SinDiffDeAuditoria]
public class StockBalance : AuditableEntity
{
    public int ProductId { get; set; }

    /// <summary>Incluidas las de tránsito.</summary>
    public int WarehouseId { get; set; }

    /// <summary>= Σ <c>QuantityBase</c> del kardex de (producto, bodega).</summary>
    public decimal Physical { get; set; }

    /// <summary>= Σ de las reservas vigentes (I6); 0 hasta entonces.</summary>
    public decimal Reserved { get; set; }

    /// <summary>Fecha del último movimiento (nuevo): alimenta «sin movimiento» y el tablero.</summary>
    public DateOnly? LastMovementDate { get; set; }
}
