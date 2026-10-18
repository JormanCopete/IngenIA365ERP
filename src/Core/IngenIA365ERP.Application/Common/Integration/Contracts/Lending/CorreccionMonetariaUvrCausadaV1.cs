namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CorreccionMonetariaUvrCausada</c> v1 (K6b, T1056; FR-024, D18; pregunta E12 con la propuesta por defecto; <c>CMU</c>): hecho masivo por partes
/// (T14), como <see cref="InteresCausadoV1"/>. La corrección monetaria de cada día de los créditos <b>en UVR</b>: lo que cambió el valor en pesos de lo
/// que se debe en UVR por el cambio de la UVR, sin lo causado ni lo movido (las filas <c>MonetaryCorrection</c> del libro). Origen la parte de la
/// corrida <c>DailyCycle</c> o de la puesta al día de una operación; clave <c>Confirmation:{parte:N}</c>. Admite proceso. Conceptos: los del saldo del
/// crédito —<c>Principal</c> sin cuota; los demás por cuota—, con signo: positivo si la UVR subió (el activo crece; un pasivo con el deudor, también).
/// Contabilidad lo asienta contra el <c>Ingreso</c> con el hecho fijado en la matriz (la cuenta de la corrección monetaria la elige la contadora).
/// (nuevo)
/// </summary>
public sealed record CorreccionMonetariaUvrCausadaV1
{
    public const string Type = "CorreccionMonetariaUvrCausada";

    /// <summary>Valor de <c>LendingProcessKind</c> (<c>DailyCycle</c>…); nulo en la puesta al día de una operación.</summary>
    public string? RunKind { get; init; }

    /// <summary>La corrida (<c>LND_ProcessRuns</c>); nula en la puesta al día.</summary>
    public Guid? RunPublicId { get; init; }

    /// <summary>El día causado.</summary>
    public DateOnly FactDate { get; init; }

    /// <summary>La tanda que la produjo (<c>1</c> en la puesta al día).</summary>
    public int BatchNumber { get; init; }

    /// <summary>Ordinal de la parte en la corrida.</summary>
    public int PartNumber { get; init; }

    /// <summary>Sólo en la puesta al día de una operación (C04).</summary>
    public CatchUpOperationV1? CatchUpOperation { get; init; }

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<LoanFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por crédito.</summary>
    public IReadOnlyList<LoanFactLineV1> Lines { get; init; } = [];

    public int LineCount { get; init; }

    /// <summary>Σ de los valores de las líneas, igual a Σ de los totales.</summary>
    public decimal ControlTotal { get; init; }
}
