using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// El motor de costeo del inventario (feature 012, T280; FR-042 a FR-046; decisiones-transversales T18, T19; research
/// R10): puro, determinista y sin IO, en el molde de <c>PayrollCalculationEngine</c>. Recibe el estado de costo de UN
/// ámbito (producto en la cooperativa o en una bodega), UN movimiento ya en unidad base y los parámetros, y devuelve las
/// líneas de kardex propuestas —con su <c>Kind</c>, <c>Reason</c>, costo y <c>AffectsEntryId</c>—, el estado nuevo y
/// la explicación. No escribe nada: el único escritor del kardex es <c>RegistroDeKardex</c>, que le pasa el estado
/// bloqueado, escribe lo que devuelve y actualiza <c>INV_CostStates</c>.
///
/// <para>
/// Un traslado son dos movimientos por ámbito: la salida del origen al costo vigente y la entrada al tránsito
/// <see cref="ValoracionDelMovimiento.AlCostoDeOrigen"/> con el costo de esa salida; al recibir, la salida del tránsito y
/// la entrada al destino van también al costo de la línea de despacho. En ámbito cooperativa los dos quedan en el mismo
/// estado y el traslado es neutro; en ámbito bodega el tránsito queda en cero exacto al resolverse.
/// </para>
///
/// <para>
/// Los documentos con fecha anterior a movimientos ya registrados los valora <see cref="Retroactivo"/> sobre este
/// mismo motor. PEPS (<see cref="CostMethod.Fifo"/>) llega en I5.
/// </para>
/// </summary>
public static class MotorDeCosteo
{
    /// <summary>El código con que la aplicación publica un rechazo del motor (FR-004).</summary>
    public const string CodigoExistenciaInsuficiente = "Inventory.Stock.Insufficient";

    public static ResultadoDeCosteo Aplicar(EstadoDeCosto estado, MovimientoDeCosto movimiento, ParametrosDeCosteo parametros)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(movimiento);
        ArgumentNullException.ThrowIfNull(parametros);
        if (movimiento.QuantityBase == 0m)
            throw new ArgumentException("Un movimiento de kardex no puede tener cantidad cero.", nameof(movimiento));

        return parametros.Metodo switch
        {
            CostMethod.WeightedAverage => PromedioPonderado.Aplicar(estado, movimiento, parametros),
            CostMethod.Fifo => throw new NotSupportedException("El método PEPS llega en la entrega I5 (FR-043)."),
            _ => throw new ArgumentOutOfRangeException(nameof(parametros), parametros.Metodo, "Método de costeo desconocido."),
        };
    }
}
