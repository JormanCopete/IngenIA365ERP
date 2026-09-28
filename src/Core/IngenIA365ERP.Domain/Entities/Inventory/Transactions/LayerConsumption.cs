using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Transactions;

/// <summary>
/// Un consumo de capa PEPS (<c>INV_LayerConsumptions</c>; feature 012, I5, T827; FR-043; data-model §3.5): un <b>hecho</b>
/// (<see cref="IHechoInmutable"/>, sólo inserción) que dice cuánto de qué capa tomó una línea del kardex y a qué costo. La línea
/// es la salida; o, con <see cref="Quantity"/> negativa, la anulación de una salida que devuelve lo que ésta consumió (también
/// la corrección de un retroactivo, si D6 se abre). Invariantes: Σ consumos de una salida = |<c>QuantityBase</c>|; Σ consumos de
/// una anulación = −<c>QuantityBase</c>; Σ consumos de una capa = <c>OriginalQuantity − RemainingQuantity</c>. Un solo escritor,
/// <c>RegistroDeKardex</c> (más la reconstrucción). <c>IX (ExitKardexEntryId)</c>, <c>IX (LayerId)</c>. Sin diferencias de
/// auditoría: el kardex es la referencia. (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class LayerConsumption : AuditableEntityLong, IHechoInmutable
{
    /// <summary>La línea del kardex que consume (la salida) o devuelve (la anulación, con cantidad negativa).</summary>
    public long ExitKardexEntryId { get; init; }

    /// <summary>La capa (<c>INV_CostLayers</c>).</summary>
    public long LayerId { get; init; }

    /// <summary>Positiva al consumir; negativa al devolver.</summary>
    public decimal Quantity { get; init; }

    /// <summary>El costo unitario de la capa.</summary>
    public decimal UnitCost { get; init; }
}
