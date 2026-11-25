namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>InteresDelCorte</c> v1 (<c>IDC</c>; 2026-11-24, feature 025, fase 3, HU1; D1): el <b>interés del período al corte</b> de los créditos cargados —lo que
/// la primera cuota posterior al corte dejó por fuera, cargado en la primera cuota sin vencer—, con los roles de la causación de interés corriente
/// (<c>InteresCausado</c>): la cuenta por cobrar del interés contra el ingreso. Hecho masivo por partes (T14), como <see cref="InteresCausadoV1"/>: origen
/// la parte (<c>AccountingFactPart</c>) de la corrida <c>CutoverRecalculation</c> del proceso de causación; clave <c>Confirmation:{parte:N}</c>. Admite
/// proceso. Concepto: <c>CurrentInterest</c>, con la cuota destino; sin contraparte. Comprobante <c>CA</c>. (nuevo)
/// </summary>
public sealed record InteresDelCorteV1
{
    public const string Type = "InteresDelCorte";

    /// <summary>Valor de <c>LendingProcessKind</c> (<c>CutoverRecalculation</c>; en la fase 4, la activación de una carga).</summary>
    public string? RunKind { get; init; }

    /// <summary>La corrida (<c>LND_ProcessRuns</c>).</summary>
    public Guid? RunPublicId { get; init; }

    /// <summary>El día de la firma (la fecha valor del ajuste).</summary>
    public DateOnly FactDate { get; init; }

    /// <summary>La tanda que la produjo.</summary>
    public int BatchNumber { get; init; }

    /// <summary>Ordinal de la parte en la corrida.</summary>
    public int PartNumber { get; init; }

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<LoanFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por crédito.</summary>
    public IReadOnlyList<LoanFactLineV1> Lines { get; init; } = [];

    public int LineCount { get; init; }

    /// <summary>Σ de los valores de las líneas, igual a Σ de los totales.</summary>
    public decimal ControlTotal { get; init; }
}
