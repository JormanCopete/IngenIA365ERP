namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>SeguroCausado</c> v1 (§10.4; <c>SCA</c>): hecho masivo por partes (T14). Origen la parte (<c>AccountingFactPart</c>) de la corrida
/// <c>DailyCycle</c> o de la puesta al día de una operación (C04); clave <c>Confirmation:{parte:N}</c>. Admite proceso. Conceptos:
/// <c>Insurance</c> por cuenta del asociado, con la aseguradora en <c>counterpartyPersonPublicId</c>.
/// </summary>
public sealed record SeguroCausadoV1
{
    public const string Type = "SeguroCausado";

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

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<LoanFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por crédito.</summary>
    public IReadOnlyList<LoanFactLineV1> Lines { get; init; } = [];

    public int LineCount { get; init; }

    /// <summary>Σ de los valores de las líneas, igual a Σ de los totales.</summary>
    public decimal ControlTotal { get; init; }
}
