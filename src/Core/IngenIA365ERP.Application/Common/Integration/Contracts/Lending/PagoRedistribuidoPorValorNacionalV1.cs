namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>PagoRedistribuidoPorValorNacional</c> v1 (K6b, decisión del dueño del 2026-10-17; <c>PRN</c>; mensajes.md §10.26): hecho masivo por partes
/// (T14), como <see cref="AjusteDeCausacionPorFechaValorV1"/>. Cuando se corrige la <c>UVR</c> de un día en que se aplicó un pago a un crédito en UVR,
/// lo recibido en pesos no cambia pero el pago es otra cantidad de UVR y cubre en pesos otra mezcla de conceptos: este hecho lleva esa
/// <b>redistribución</b> por concepto y cuota, con el <b>signo del libro</b> (reconstrucción − libro: un concepto que el pago cubre de más sale
/// negativo; un anticipo o saldo a favor que el pago deja de más, positivo) y suma cero contra los pasivos con el deudor. Origen la parte de la
/// corrida <c>NationalValueCorrection</c>; clave <c>Confirmation:{parte:N}</c>; una línea por crédito y día valor del pago
/// (<see cref="LoanFactLineV1.ValueDate"/>). Admite proceso. Contabilidad lo asienta entre los roles de saldo (y el ingreso del suspenso o de la
/// recuperación), sin el puente del recaudo: el dinero ya entró con el pago. Ninguna cuenta. (nuevo)
/// </summary>
public sealed record PagoRedistribuidoPorValorNacionalV1
{
    public const string Type = "PagoRedistribuidoPorValorNacional";

    /// <summary>Valor de <c>LendingProcessKind</c>: <c>NationalValueCorrection</c>.</summary>
    public string? RunKind { get; init; }

    /// <summary>La corrida (<c>LND_ProcessRuns</c>).</summary>
    public Guid? RunPublicId { get; init; }

    /// <summary>El último día corregido de la parte (los días valor de los pagos van en cada línea).</summary>
    public DateOnly FactDate { get; init; }

    /// <summary>La tanda que la produjo.</summary>
    public int BatchNumber { get; init; }

    /// <summary>Ordinal de la parte en la corrida.</summary>
    public int PartNumber { get; init; }

    /// <summary>El valor nacional corregido que provoca la corrida.</summary>
    public CorrectedNationalValueV1? CorrectedNationalValue { get; init; }

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<LoanFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por crédito y día valor del pago redistribuido.</summary>
    public IReadOnlyList<LoanFactLineV1> Lines { get; init; } = [];

    public int LineCount { get; init; }

    /// <summary>Σ de los valores de las líneas, igual a Σ de los totales (cero si sólo se redistribuye entre lo que se debe).</summary>
    public decimal ControlTotal { get; init; }
}
