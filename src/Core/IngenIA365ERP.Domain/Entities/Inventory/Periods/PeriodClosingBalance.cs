using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Periods;

/// <summary>
/// El valorizado fijado al cerrar un mes (<c>INV_PeriodClosingBalances</c>; feature 012, T283; FR-047, SC-017; data-model
/// §6.3): cantidad y valor por producto × bodega (incluidas las de tránsito), con el grupo contable del producto a la fecha de
/// cierre. Valor = cantidad × promedio del ámbito con el residuo por <c>Redondeo.Residuo</c>, de modo que Σ <see cref="Value"/>
/// por ámbito = <c>CostState.Value</c> a esa fecha. No se guardan filas en cero. Lo único que cambia después es
/// <see cref="Superseded"/>, que pone la reapertura; el valorizado a una fecha pasada parte del último cierre vigente anterior
/// y suma el kardex posterior. Sin diferencias de auditoría: lo audita el cierre.
/// </summary>
[SinDiffDeAuditoria]
public class PeriodClosingBalance : AuditableEntity
{
    public int PeriodId { get; init; }

    public InventoryPeriod? Period { get; init; }

    /// <summary>= <c>CloseVersion</c> del cierre que la fijó.</summary>
    public int Version { get; init; }

    public int ProductId { get; init; }

    public int WarehouseId { get; init; }

    /// <summary>El grupo del producto a la fecha de cierre (data-model §1.10).</summary>
    public int AccountingGroupId { get; init; }

    public decimal Quantity { get; init; }

    public decimal Value { get; init; }

    /// <summary>1 al reabrir el período: la versión deja de valer, pero no se borra.</summary>
    public bool Superseded { get; set; }
}
