namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CuotaReconocida</c> v1 (<c>CUR</c>; 2026-11-16, pedido del dueño, feature 024, fase 4): el <b>reconocimiento al inicio del ciclo</b> de la cuota del
/// período —lo que la cuota va a causar hasta su vencimiento: interés corriente, seguro y administración; el capital no tiene asiento—. Hecho masivo por
/// partes (T14), como <see cref="InteresCausadoV1"/>: origen la parte (<c>AccountingFactPart</c>) de la corrida <c>DailyCycle</c> (concepto en
/// «Automatica»), de la puesta al día de una operación (C04) o del proceso de causación <c>PeriodRecognition</c> (concepto en «Manual», corrida
/// <c>PeriodRecognition</c>); clave <c>Confirmation:{parte:N}</c>. Admite proceso. Conceptos: los reconocidos (<c>RecognizedInterest</c>,
/// <c>RecognizedInsurance</c>, <c>RecognizedAdministration</c> contra el ingreso diferido; sus variantes <c>…Direct</c> contra lo mismo que su causación),
/// positivos: la cuenta por cobrar de su rubro contra el ingreso diferido o el ingreso. Ninguna cuenta viaja en el hecho (FR-126). (nuevo)
/// </summary>
public sealed record CuotaReconocidaV1
{
    public const string Type = "CuotaReconocida";

    /// <summary>Valor de <c>LendingProcessKind</c> (<c>DailyCycle</c>, <c>PeriodRecognition</c>…); nulo en la puesta al día de una operación.</summary>
    public string? RunKind { get; init; }

    /// <summary>La corrida (<c>LND_ProcessRuns</c>); nula en la puesta al día.</summary>
    public Guid? RunPublicId { get; init; }

    /// <summary>El día del reconocimiento (desde el que cubre la causación de la cuota).</summary>
    public DateOnly FactDate { get; init; }

    /// <summary>La tanda que la produjo (<c>1</c> en la puesta al día y en el proceso).</summary>
    public int BatchNumber { get; init; }

    /// <summary>Ordinal de la parte en la corrida.</summary>
    public int PartNumber { get; init; }

    /// <summary>Sólo en la puesta al día de una operación (C04).</summary>
    public CatchUpOperationV1? CatchUpOperation { get; init; }

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<LoanFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por crédito, con lo reconocido por concepto y cuota.</summary>
    public IReadOnlyList<LoanFactLineV1> Lines { get; init; } = [];

    public int LineCount { get; init; }

    /// <summary>Σ de los valores de las líneas, igual a Σ de los totales.</summary>
    public decimal ControlTotal { get; init; }
}
