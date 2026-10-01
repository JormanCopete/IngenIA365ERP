using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Periods;

/// <summary>
/// Un mes de inventario de toda la cooperativa (<c>INV_Periods</c>; feature 012, T283; FR-047; data-model §6.2). Un mes sin
/// fila entre <c>INV_Setup.StartDate</c> y hoy está abierto: la fila nace con el primer cierre. Transiciones:
/// (abierto) → <see cref="InventoryPeriodStatus.Closed"/> con <c>CloseInventoryPeriodCommand</c> (en orden, con los avisos
/// reconocidos y el valorizado fijado en la versión <see cref="CloseVersion"/>) y <c>Closed → Open</c> con
/// <c>ReopenInventoryPeriodCommand</c> (sólo el último cerrado, con motivo; deja <c>Superseded</c> su valorizado). Cada
/// recierre sube <see cref="CloseVersion"/>.
/// </summary>
public class InventoryPeriod : AuditableEntity
{
    public short Year { get; set; }

    public byte Month { get; set; }

    public InventoryPeriodStatus Status { get; set; } = InventoryPeriodStatus.Open;

    /// <summary>1 en el primer cierre; +1 en cada recierre.</summary>
    public int CloseVersion { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int? ClosedByUserId { get; set; }

    /// <summary>Borradores, tránsitos sin resolver y mensajes pendientes, en lote o rechazados con fecha en el mes, tal como se aceptaron.</summary>
    public string? CloseWarningsJson { get; set; }

    /// <summary>Remisiones sin facturar con su valor por facturar (I6).</summary>
    public string? UnbilledShipmentsJson { get; set; }

    public int? UnbilledShipmentsAcceptedByUserId { get; set; }

    public string? UnbilledShipmentsAcceptedReason { get; set; }

    public DateTime? ReopenedAt { get; set; }

    public int? ReopenedByUserId { get; set; }

    public string? ReopenReason { get; set; }

    /// <summary>Primer día del mes.</summary>
    public DateOnly Inicio => new(Year, Month, 1);

    /// <summary>Último día del mes.</summary>
    public DateOnly Fin => Inicio.AddMonths(1).AddDays(-1);
}
