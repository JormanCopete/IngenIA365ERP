using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Projections;

/// <summary>
/// El estado de costo de un producto en su ámbito (<c>INV_CostStates</c>; feature 012, T249; FR-042, FR-043; data-model §3.4):
/// proyección del kardex con cantidad, valor, promedio y último costo. <see cref="ScopeWarehouseId"/> 0 = cooperativa, o la
/// bodega (el tránsito tiene el suyo en ámbito bodega). Con <see cref="Quantity"/> = 0, <see cref="Value"/> = 0 siempre (lo
/// asegura la línea <c>RoundingResidue</c>). El valor por bodega es cantidad × promedio del ámbito, nunca Σ <c>TotalCost</c>
/// por bodega. La escriben sólo <c>RegistroDeKardex</c> y <c>RebuildInventoryProjectionsCommand</c>. Sin diferencias de
/// auditoría.
/// </summary>
[SinDiffDeAuditoria]
public class CostState : AuditableEntity
{
    public int ProductId { get; set; }

    /// <summary>0 = cooperativa; o el Id de la bodega. Sin FK (0 es centinela).</summary>
    public int ScopeWarehouseId { get; set; }

    /// <summary>El vigente para el ámbito.</summary>
    public CostMethod Method { get; set; } = CostMethod.WeightedAverage;

    /// <summary>Σ <c>QuantityBase</c> del ámbito.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Σ <c>TotalCost</c> del ámbito, ajustes incluidos.</summary>
    public decimal Value { get; set; }

    /// <summary><c>Value / Quantity</c> con cantidad positiva; si no, conserva <see cref="LastUnitCost"/>.</summary>
    public decimal AverageCost { get; set; }

    /// <summary>Último costo de entrada: lo usan la salida en negativo y la existencia cero.</summary>
    public decimal LastUnitCost { get; set; }
}
