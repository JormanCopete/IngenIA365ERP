namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>AjusteDeCausacionPorFechaValor</c> v1 (§10.5; <c>AFV</c>; C03): acompañante de una operación fechada atrás (clave
/// <c>Confirmation</c>) o una parte de la corrida <c>NationalValueCorrection</c> (clave <c>Confirmation:{parte:N}</c>). Forma masiva,
/// valores con signo. Admite proceso.
/// </summary>
public sealed record AjusteDeCausacionPorFechaValorV1
{
    public const string Type = "AjusteDeCausacionPorFechaValor";

    /// <summary>Valor de <c>LendingProcessKind</c> (<c>DailyCycle</c>, <c>NationalValueCorrection</c>…); nulo en la puesta al día de una operación.</summary>
    public string? RunKind { get; init; }

    /// <summary>La corrida (<c>LND_ProcessRuns</c>); nula en la puesta al día.</summary>
    public Guid? RunPublicId { get; init; }

    /// <summary>El día causado.</summary>
    public DateOnly FactDate { get; init; }

    /// <summary>La tanda que la produjo (<c>1</c> en la puesta al día).</summary>
    public int BatchNumber { get; init; }

    /// <summary>Ordinal de la parte en la corrida.</summary>
    public int PartNumber { get; init; }

    /// <summary>Versión del cierre; nula fuera del cierre.</summary>
    public int? CloseVersion { get; init; }

    /// <summary>Sólo en la puesta al día de una operación (C04).</summary>
    public CatchUpOperationV1? CatchUpOperation { get; init; }

    /// <summary><c>ValueDatedOperation</c> o <c>NationalValueCorrection</c>.</summary>
    public string Cause { get; init; } = string.Empty;

    /// <summary>El hecho principal de la operación que lo provocó.</summary>
    public OriginalFactRefV1? CauseFact { get; init; }

    /// <summary>En la corrección: el valor nacional corregido.</summary>
    public CorrectedNationalValueV1? CorrectedNationalValue { get; init; }

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<LoanFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por crédito.</summary>
    public IReadOnlyList<LoanFactLineV1> Lines { get; init; } = [];

    public int LineCount { get; init; }

    /// <summary>Σ de los valores de las líneas, igual a Σ de los totales.</summary>
    public decimal ControlTotal { get; init; }
}

/// <summary>El valor nacional corregido que provoca la corrida <c>NationalValueCorrection</c> (§10.5, <c>correctedNationalValue</c>).</summary>
public sealed record CorrectedNationalValueV1
{
    public string Code { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public DateOnly ValidFrom { get; init; }

    public Guid SourcePublicId { get; init; }

    public long SourceSequence { get; init; }
}
