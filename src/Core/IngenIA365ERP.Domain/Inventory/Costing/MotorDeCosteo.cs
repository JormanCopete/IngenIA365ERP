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
/// mismo motor (sólo en promedio ponderado, D6) y <see cref="SimularImpacto"/> muestra antes de confirmar lo que escribirá;
/// los costos adicionales, <see cref="Costing.Prorrateo"/>. PEPS (<see cref="CostMethod.Fifo"/>, I5) es <see cref="Peps"/>:
/// capas en <see cref="EstadoDeCosto.Capas"/> y consumos en <see cref="ResultadoDeCosteo.Consumos"/>; el paso de un método a
/// otro, <see cref="CambiarMetodo"/>.
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
            CostMethod.Fifo => Peps.Aplicar(estado, movimiento, parametros),
            _ => throw new ArgumentOutOfRangeException(nameof(parametros), parametros.Metodo, "Método de costeo desconocido."),
        };
    }

    /// <summary>
    /// El ajuste de costo <c>PriceDifference</c> de una factura o nota del proveedor sobre una entrada (US9, T341): partido
    /// entre existencia y vendido (<see cref="Costing.DiferenciaDePrecio"/>).
    /// </summary>
    public static ResultadoDeCosteo DiferenciaDePrecio(EstadoDeCosto estado, PedidoDeDiferenciaDePrecio pedido, RedondeoDeMontos montos) =>
        Costing.DiferenciaDePrecio.Aplicar(estado, pedido, montos);

    /// <summary>
    /// El ajuste de costo <c>LandedCost</c> de un documento de costos adicionales sobre la entrada de una línea de recepción (US13,
    /// T780; FR-046): la porción que le tocó en <see cref="Costing.Prorrateo"/>, partida en existencia y vendido, con
    /// <c>AffectsEntryId</c> de esa entrada.
    /// </summary>
    /// <summary>
    /// El cambio de método de un ámbito (US16, T821; FR-043): la línea <c>MethodChange</c> y, a PEPS, la capa única con la
    /// existencia al promedio (<see cref="Peps.CambioDeMetodo"/>). Lo invoca el documento <c>CostAdjustment</c> que genera el
    /// sistema al registrar <c>Costeo.Metodo</c>; un cambio de <c>Costeo.Ambito</c> rehace los estados con el mismo método.
    /// </summary>
    public static ResultadoDeCosteo CambiarMetodo(EstadoDeCosto estado, CostMethod nuevo, RedondeoDeMontos montos, DateOnly? fecha) =>
        Peps.CambioDeMetodo(estado, nuevo, montos, fecha);

    /// <summary>
    /// El impacto de un documento con fecha anterior antes de confirmarlo (US16, T830; FR-045; api.md §9.3): corre
    /// <see cref="Retroactivo.Insertar"/> —el mismo cálculo que la confirmación— sobre el borrador y devuelve si es retroactivo,
    /// los documentos afectados con su porción en existencia y vendida, el total y el resultado completo, sin nada persistible
    /// distinto de lo que produciría confirmar.
    /// </summary>
    public static ImpactoEnCostos SimularImpacto(PedidoRetroactivo pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.Nuevos.Count == 0) throw new ArgumentException("El impacto necesita al menos un movimiento.", nameof(pedido));

        var fecha = pedido.Nuevos.Min(n => n.OperationDate);
        var retroactivo = Retroactivo.PrimerMovimientoPosterior(pedido.Historia, fecha) is not null;
        var resultado = Retroactivo.Insertar(pedido);
        return new ImpactoEnCostos(retroactivo, resultado.PorDocumento, resultado.Ajustes.Sum(a => a.TotalCost), resultado, resultado.Rechazo);
    }

    public static ResultadoDeCosteo CostoAdicional(EstadoDeCosto estado, ReferenciaDeKardex entrada, RepartoDeLinea reparto) =>
        Costing.Prorrateo.AlKardex(estado, entrada, reparto);
}
