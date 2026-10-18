namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>RetencionPracticada</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>RTF</c>; entrega K8): acompañante del pago o abono de intereses, o línea de la parte de la liquidación (FR-063); débito al capital o al interés por pagar. Admite proceso.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record RetencionPracticadaV1
{
    public const string Type = "RetencionPracticada";

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

    /// <summary>El detalle por cuenta, en el orden de las líneas.</summary>
    public IReadOnlyList<WithholdingDetailV1> Withholdings { get; init; } = [];
}
