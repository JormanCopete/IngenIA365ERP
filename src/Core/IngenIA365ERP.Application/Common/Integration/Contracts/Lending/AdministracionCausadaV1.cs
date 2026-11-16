namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>AdministracionCausada</c> v1 (<c>ACA</c>; 2026-11-16, pedido del dueño, feature 024, fase 2): la cuota de <b>administración</b> causada, aparte del
/// seguro. Hecho masivo por partes (T14), como <see cref="SeguroCausadoV1"/>: origen la parte (<c>AccountingFactPart</c>) de la corrida
/// <c>DailyCycle</c> o de la puesta al día de una operación (C04); clave <c>Confirmation:{parte:N}</c>. Admite proceso. Conceptos:
/// <c>Administration</c>, ingreso de la cooperativa (cargo por cobrar contra ingreso); sin contraparte. (nuevo)
/// </summary>
public sealed record AdministracionCausadaV1
{
    public const string Type = "AdministracionCausada";

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
