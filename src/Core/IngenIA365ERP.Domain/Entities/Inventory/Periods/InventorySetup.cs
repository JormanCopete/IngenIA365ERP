using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Periods;

/// <summary>
/// La puesta en marcha del módulo (<c>INV_Setup</c>; feature 012, T283; FR-047; data-model §6.1): <b>fila única</b>. La crea
/// el primer registro de una fecha de corte (US4); desde entonces ningún documento se fecha antes de <see cref="StartDate"/>
/// y todo lo fechado en o antes de <see cref="LastClosedDate"/> está en un período cerrado. Como los meses cierran en orden
/// y sólo se reabre el último, «¿período cerrado?» se reduce a comparar con <see cref="LastClosedDate"/>. El cerrojo la toma
/// compartida al confirmar y exclusiva al cerrar o reabrir (<c>ICerrojoDeInventario</c>).
/// </summary>
public class InventorySetup : AuditableEntity
{
    /// <summary>Primer día del mes del primer corte.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Último día del último mes cerrado; nulo = ninguno.</summary>
    public DateOnly? LastClosedDate { get; set; }

    public DateTime StartedAt { get; set; }

    public int StartedByUserId { get; set; }
}
