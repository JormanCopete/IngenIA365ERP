namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CarteraReclasificadaPorCalificacion</c> v1 (§10.17; <c>RCL</c>): parte del cierre mensual, en la transacción final del cierre (T47). Origen la parte,
/// clave <c>Close:{versión}</c>. Admite proceso. La reclasificación se emite antes que el deterioro (T48).
/// </summary>
public sealed record CarteraReclasificadaPorCalificacionV1
{
    public const string Type = "CarteraReclasificadaPorCalificacion";

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

    public int Year { get; init; }

    public int Month { get; init; }

    /// <summary>R sellado del cierre.</summary>
    public DateTimeOffset InformationCutoffAt { get; init; }

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<LoanFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por crédito.</summary>
    public IReadOnlyList<LoanFactLineV1> Lines { get; init; } = [];

    public int LineCount { get; init; }

    /// <summary>Σ de los valores de las líneas, igual a Σ de los totales.</summary>
    public decimal ControlTotal { get; init; }

    /// <summary>Sólo en el deterioro: el general y el adicional, en delta.</summary>
    public IReadOnlyList<GeneralImpairmentLineV1>? GeneralImpairment { get; init; }
}
