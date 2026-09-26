using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Una línea de la foto de un conteo físico (<c>INV_CountSnapshotLines</c>; feature 012, US11, T389; data-model §8.1; FR-040):
/// producto × ubicación × lote del alcance, con la existencia teórica y el costo promedio del ámbito al abrir. Se escribe al abrir
/// (<c>OpenPhysicalCountCommand</c>) o durante la captura si aparece un producto que no estaba en la foto
/// (<see cref="AddedDuringCapture"/>, teórico 0). Queda <b>fija</b> desde que se abre salvo las columnas del cierre
/// (<see cref="MovementsAfterSnapshot"/>, <see cref="CountedQuantity"/>, <see cref="Difference"/>) y las de la ronda
/// (<see cref="RecountRequired"/>, <see cref="LastRound"/>), que sólo cambian por <see cref="MarcarRonda"/> y <see cref="Cerrar"/>.
/// Dos únicos filtrados por lote, como <c>INV_StockDetails</c>. (nuevo)
/// </summary>
public class CountSnapshotLine : AuditableEntity
{
    /// <summary>El conteo (<c>INV_Documents</c> de clase <c>PhysicalCount</c>).</summary>
    public int DocumentId { get; init; }

    public int ProductId { get; init; }

    public int LocationId { get; init; }

    /// <summary>Sin FK hasta I6 (data-model §3.0); siempre nulo en I1.</summary>
    public int? LotId { get; init; }

    /// <summary>La foto: la existencia de la combinación al abrir.</summary>
    public decimal TheoreticalQuantity { get; init; }

    /// <summary>El costo promedio del ámbito al abrir (informativo: el ajuste se valora en su propia fecha).</summary>
    public decimal SnapshotUnitCost { get; init; }

    /// <summary>Producto encontrado durante la captura que no estaba en la foto (teórico 0).</summary>
    public bool AddedDuringCapture { get; init; }

    /// <summary>Al cerrar, si el conteo admitía movimientos: lo movido después de la foto (se suma al teórico).</summary>
    public decimal? MovementsAfterSnapshot { get; private set; }

    /// <summary>Al cerrar: lo contado en la ronda que manda (la última).</summary>
    public decimal? CountedQuantity { get; private set; }

    /// <summary>Al cerrar: contado − (teórico + movimientos posteriores).</summary>
    public decimal? Difference { get; private set; }

    /// <summary>La diferencia de la ronda supera la tolerancia y falta el reconteo.</summary>
    public bool RecountRequired { get; private set; }

    /// <summary>La última ronda con capturas (0 = sin capturas).</summary>
    public byte LastRound { get; private set; }

    public ICollection<CountCapture> Captures { get; set; } = new List<CountCapture>();

    /// <summary>La revisión de una ronda: cuál es la última con capturas y si falta el reconteo.</summary>
    public void MarcarRonda(byte ultimaRonda, bool requiereReconteo)
    {
        LastRound = ultimaRonda;
        RecountRequired = requiereReconteo;
    }

    /// <summary>Las columnas del cierre (sólo <c>ClosePhysicalCountCommand</c>).</summary>
    public void Cerrar(decimal? movimientosPosteriores, decimal contado, decimal diferencia, byte ultimaRonda)
    {
        MovementsAfterSnapshot = movimientosPosteriores;
        CountedQuantity = contado;
        Difference = diferencia;
        LastRound = ultimaRonda;
        RecountRequired = false;
    }
}
