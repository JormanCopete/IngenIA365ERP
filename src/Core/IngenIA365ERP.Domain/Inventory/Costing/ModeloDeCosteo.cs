using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// Cómo se valora un movimiento (feature 012, T280; FR-042, FR-044; research R10). No se guarda: la aplicación la
/// deduce de la clase del documento y de la línea. (nuevo)
/// </summary>
public enum ValoracionDelMovimiento
{
    /// <summary>Salida al promedio del ámbito (venta, ajuste negativo, consumo, baja, despacho desde el origen); entrada
    /// al costo vigente (ajuste positivo sin costo indicado, sobrante de conteo). Con existencia cero, el último costo.</summary>
    AlCostoVigente = 1,

    /// <summary>Entrada al costo que trae la línea: compra neta (<see cref="CostoDeEntrada"/>), saldo inicial, ajuste
    /// positivo con costo indicado.</summary>
    AlCostoIndicado = 2,

    /// <summary>Al costo de la línea de origen, sin diferencia: devolución de cliente (al costo con que salió), anulación
    /// de una salida, entrada al tránsito y salida del tránsito (al costo de la línea de despacho).</summary>
    AlCostoDeOrigen = 3,

    /// <summary>Salida al costo con que entró la entrada devuelta o anulada (devolución a proveedor, anulación de una
    /// entrada); la diferencia contra el promedio vigente es una línea <c>VoidDifference</c>.</summary>
    DevolucionDeEntrada = 4,
}

/// <summary>Qué parte de un ajuste retroactivo quedó en la existencia y qué parte ya se vendió o consumió. (nuevo)</summary>
public enum PorcionDelAjuste { EnExistencia = 1, Vendida = 2 }

/// <summary>
/// Una línea del kardex ya escrita (<see cref="EntryId"/>) o una propuesta de este mismo cálculo (<see cref="Linea"/>),
/// para <c>AffectsEntryId</c> y <c>ReversesEntryId</c>. El registro del kardex resuelve la propuesta a su fila. (nuevo)
/// </summary>
public sealed record ReferenciaDeKardex(long? EntryId, LineaDeKardexPropuesta? Linea)
{
    public static ReferenciaDeKardex A(long entryId) => new(entryId, null);

    public static ReferenciaDeKardex A(LineaDeKardexPropuesta linea) => new(null, linea);

    /// <summary>El Id de la fila: el escrito, o el que el registro le asignó a la propuesta.</summary>
    public long? Id => EntryId ?? Linea?.Id;
}

/// <summary>
/// Una salida que dejó el ámbito en negativo al último costo y todavía espera su <c>NegativeRegularization</c>
/// (research R10). La aplicación la reconstruye del kardex; si no la trae, la regularización usa el costo del negativo
/// (<c>Value / Quantity</c>) sin <c>AffectsEntryId</c>. (nuevo)
/// </summary>
public sealed record SalidaEnNegativo(ReferenciaDeKardex Salida, decimal Cantidad, decimal CostoUnitario);

/// <summary>
/// El estado de costo de un ámbito —producto en la cooperativa o en una bodega— (data-model §3.4, <c>INV_CostStates</c>):
/// cantidad, valor, promedio (<c>Value / Quantity</c> con cantidad positiva; si no, el último costo) y último costo de
/// entrada, más las salidas en negativo pendientes. (nuevo)
/// </summary>
public sealed record EstadoDeCosto(decimal Quantity, decimal Value, decimal AverageCost, decimal LastUnitCost)
{
    public static EstadoDeCosto Vacio { get; } = new(0m, 0m, 0m, 0m);

    public IReadOnlyList<SalidaEnNegativo> SalidasEnNegativo { get; init; } = [];

    /// <summary>El costo al que sale o entra un movimiento al costo vigente: el promedio, o el último costo sin existencia.</summary>
    public decimal CostoVigente => Quantity > 0m ? AverageCost : LastUnitCost;

    /// <summary>
    /// El estado que corresponde a una cantidad y un valor, con el promedio recalculado. Un costo nunca es negativo: si
    /// el redondeo (al peso, con cantidades fraccionarias y el negativo permitido) deja un valor negativo con cantidad
    /// positiva, el promedio es cero y el residuo sale cuando la cantidad llegue a cero.
    /// </summary>
    public static EstadoDeCosto Con(decimal cantidad, decimal valor, decimal ultimoCosto, IReadOnlyList<SalidaEnNegativo>? pendientes = null) =>
        new(cantidad, valor, cantidad > 0m ? Math.Max(0m, Redondeo.CostoUnitario(valor / cantidad)) : ultimoCosto, ultimoCosto)
        {
            SalidasEnNegativo = pendientes ?? [],
        };
}

/// <summary>
/// El movimiento que se valora: cantidad en unidad base con signo (+ entrada, − salida), cómo se valora, el costo
/// unitario cuando lo trae (entrada al costo indicado, costo de la línea de origen o de la entrada devuelta), la línea
/// de origen y si es la anulación de esa línea (<c>ReversesEntryId</c>). (nuevo)
/// </summary>
public sealed record MovimientoDeCosto(
    decimal QuantityBase,
    ValoracionDelMovimiento Valoracion,
    decimal? CostoUnitario = null,
    ReferenciaDeKardex? Origen = null,
    bool EsAnulacion = false)
{
    public bool EsEntrada => QuantityBase > 0m;
}

/// <summary>Método, redondeo y si el ámbito puede quedar en negativo (<c>Existencias.StockNegativoPermitido</c>). (nuevo)</summary>
public sealed record ParametrosDeCosteo(
    CostMethod Metodo = CostMethod.WeightedAverage,
    RedondeoDeMontos Montos = RedondeoDeMontos.Centavo,
    bool NegativoPermitido = false);

/// <summary>
/// Una línea de kardex propuesta (data-model §3.1). La aplicación la escribe tal cual y le asigna <see cref="Id"/>; en un
/// ajuste retroactivo trae su propia <see cref="OperationDate"/> (la de la salida afectada), su
/// <see cref="Porcion"/> y el documento afectado. (nuevo)
/// </summary>
public sealed class LineaDeKardexPropuesta
{
    public required KardexEntryKind Kind { get; init; }
    public required KardexReason Reason { get; init; }
    public required decimal QuantityBase { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
    public ReferenciaDeKardex? AffectsEntry { get; init; }
    public ReferenciaDeKardex? ReversesEntry { get; init; }

    /// <summary>Sólo en los ajustes retroactivos: la fecha de la salida afectada. Nula = la del documento.</summary>
    public DateOnly? OperationDate { get; init; }

    /// <summary>Sólo en los ajustes retroactivos.</summary>
    public PorcionDelAjuste? Porcion { get; init; }

    /// <summary>Sólo en los ajustes retroactivos: el documento afectado (uno <c>AjusteDeCostoReconocido</c> por cada uno).</summary>
    public long? AffectedDocumentId { get; init; }

    /// <summary>El Id que le asignó el registro del kardex al escribirla.</summary>
    public long? Id { get; set; }
}

/// <summary>Por qué no se puede valorar: la existencia del ámbito no alcanza con el negativo prohibido. (nuevo)</summary>
public sealed record RechazoDeCosteo(string Codigo, string Mensaje, decimal Disponible, decimal Pedido, long? MovimientoEntryId = null);

/// <summary>Lo que devuelve el motor para un movimiento: sus líneas, el estado nuevo y la explicación, o el rechazo. (nuevo)</summary>
public sealed record ResultadoDeCosteo(
    IReadOnlyList<LineaDeKardexPropuesta> Lineas,
    EstadoDeCosto Estado,
    ExplicacionDeCosto Explicacion,
    RechazoDeCosteo? Rechazo = null)
{
    public bool Admitido => Rechazo is null;

    /// <summary>La primera línea de entrada o salida: la que nombran las anulaciones, las devoluciones y los retroactivos.</summary>
    public LineaDeKardexPropuesta? Principal => Lineas.FirstOrDefault(l => l.Kind != KardexEntryKind.CostAdjustment);

    /// <summary>Σ <c>TotalCost</c> de sus líneas: lo que el movimiento cambió el valor del ámbito.</summary>
    public decimal Valor => Lineas.Sum(l => l.TotalCost);
}
