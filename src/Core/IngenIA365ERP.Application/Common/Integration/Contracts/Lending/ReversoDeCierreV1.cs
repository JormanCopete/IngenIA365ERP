namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>ReversoDeCierre</c> v1 (§10.18; <c>RCI</c>): parte de la reapertura (corrida <c>CloseReopen</c>, C58). Clave
/// <c>Reopen:{versión}</c>, <c>related</c> = la parte del cierre que revierte; hereda su modo (C08). Admite proceso.
/// </summary>
public sealed record ReversoDeCierreV1
{
    public const string Type = "ReversoDeCierre";

    /// <summary>Valor de <c>LendingProcessKind</c>: <c>MonthlyClose</c> o <c>CloseReopen</c>.</summary>
    public string? RunKind { get; init; }

    /// <summary>La corrida (<c>LND_ProcessRuns</c>).</summary>
    public Guid? RunPublicId { get; init; }

    /// <summary>Nula en el cierre.</summary>
    public CatchUpOperationV1? CatchUpOperation { get; init; }

    /// <summary>El último día del mes cerrado.</summary>
    public DateOnly FactDate { get; init; }

    /// <summary>La tanda que la produjo.</summary>
    public int BatchNumber { get; init; }

    /// <summary>Ordinal de la parte en la corrida.</summary>
    public int PartNumber { get; init; }

    /// <summary>Versión del cierre.</summary>
    public int? CloseVersion { get; init; }

    /// <summary><c>LND_MonthlyCloseReopenings</c>.</summary>
    public Guid ReopeningPublicId { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>El mensaje y la parte del cierre que revierte.</summary>
    public OriginalFactRefV1 ReversedPart { get; init; } = new();

    /// <summary><c>CarteraReclasificadaPorCalificacion</c> o <c>DeterioroCalculado</c>.</summary>
    public string ReversedType { get; init; } = string.Empty;

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<LoanFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por crédito.</summary>
    public IReadOnlyList<LoanFactLineV1> Lines { get; init; } = [];

    public int LineCount { get; init; }

    /// <summary>Σ de los valores de las líneas, igual a Σ de los totales.</summary>
    public decimal ControlTotal { get; init; }

    /// <summary>
    /// US9 (bloque 12B): sólo al revertir un <c>DeterioroCalculado</c>, el general y el adicional de la parte revertida, con su movimiento, para que
    /// la reapertura deshaga la parte entera (antes sólo viajaban las líneas por crédito y el general quedaba contabilizado dos veces al volver a
    /// cerrar). Opcional: un reverso de una reclasificación no lo lleva.
    /// </summary>
    public IReadOnlyList<GeneralImpairmentLineV1>? GeneralImpairment { get; init; }
}
