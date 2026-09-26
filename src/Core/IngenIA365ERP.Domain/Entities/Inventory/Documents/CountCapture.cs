using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Una tanda de lecturas de un contador sobre una línea de la foto en una ronda (<c>INV_CountCaptures</c>; feature 012, US11,
/// T389; data-model §8.2; FR-040, US11-2). Es un <b>hecho</b>: sólo se inserta mientras el conteo está abierto; una lectura
/// equivocada se corrige con otra de cantidad negativa (<see cref="IsCorrection"/>), nunca reescribiendo ésta. Lo contado de una
/// línea en una ronda es la suma de las tandas de todos los contadores en esa ronda. (nuevo)
/// </summary>
public class CountCapture : AuditableEntity, IHechoInmutable
{
    public int DocumentId { get; init; }

    public int SnapshotLineId { get; init; }

    public CountSnapshotLine? SnapshotLine { get; init; }

    /// <summary>1 = primer conteo; 2… = reconteos.</summary>
    public byte Round { get; init; }

    /// <summary><c>SEC_Users.Id</c> de quien contó.</summary>
    public int CounterUserId { get; init; }

    /// <summary>La suma de las lecturas de la tanda, en unidad base (cada lectura suma; negativa = corrección).</summary>
    public decimal Quantity { get; init; }

    /// <summary>Cuántas lecturas del lector trae la tanda.</summary>
    public int Reads { get; init; }

    public bool IsCorrection { get; init; }

    /// <summary>El instante de la lectura (el que manda el lector, o el de registro si no lo trae).</summary>
    public DateTime CapturedAt { get; init; }
}
