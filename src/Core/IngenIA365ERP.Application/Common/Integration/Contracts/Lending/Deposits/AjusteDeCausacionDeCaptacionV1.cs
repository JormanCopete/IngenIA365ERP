namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>AjusteDeCausacionDeCaptacion</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>AJC</c>; entrega K8): acompañante de la operación fechada atrás o de la redención (una línea), o parte de una corrección del proceso (rótulo <c>AJC-</c>); signo mixto. Admite proceso.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record AjusteDeCausacionDeCaptacionV1
{
    public const string Type = "AjusteDeCausacionDeCaptacion";

    /// <summary>Valor de <c>LendingProcessKind</c> (<c>DailyCycle</c>); nulo en la puesta al día de una operación.</summary>
    public string? RunKind { get; init; }

    /// <summary>La corrida (<c>LND_ProcessRuns</c>); nula en la puesta al día o en la emisión de una operación.</summary>
    public Guid? RunPublicId { get; init; }

    /// <summary>Sólo en la puesta al día de una operación (rótulo <c>ALD-</c>, la 013 C04).</summary>
    public CatchUpOperationV1? CatchUpOperation { get; init; }

    /// <summary>El día del hecho.</summary>
    public DateOnly FactDate { get; init; }

    /// <summary>La tanda que la produjo (<c>1</c> fuera del proceso).</summary>
    public int BatchNumber { get; init; }

    /// <summary>Ordinal de la parte en la corrida.</summary>
    public int PartNumber { get; init; }

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<DepositFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por cuenta.</summary>
    public IReadOnlyList<DepositFactLineV1> Lines { get; init; } = [];

    public int LineCount { get; init; }

    /// <summary>Σ de los valores de las líneas, igual a Σ de los totales.</summary>
    public decimal ControlTotal { get; init; }

    /// <summary><c>ValueDatedOperation</c>, <c>Redemption</c>, <c>RateCorrection</c> o <c>ContractualBreach</c>.</summary>
    public string Cause { get; init; } = string.Empty;

    /// <summary>El hecho principal de la operación que lo provocó.</summary>
    public OriginalFactRefV1? CauseFact { get; init; }

    /// <summary>Lo causado antes y lo recalculado, por día.</summary>
    public IReadOnlyList<AccrualDayV1> Days { get; init; } = [];
}

/// <summary>Un día de un ajuste de causación: lo causado antes y lo recalculado (6 decimales).</summary>
public sealed record AccrualDayV1
{
    public DateOnly Date { get; init; }

    public decimal Previous { get; init; }

    public decimal Recalculated { get; init; }
}
