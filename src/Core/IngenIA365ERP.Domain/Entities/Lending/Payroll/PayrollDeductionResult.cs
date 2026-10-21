using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollDeductionResults</c> (feature 013, K2; data-model §24.8; FR-114, FR-115): lo que Recaudo informó de una línea del archivo en
/// <c>ResultadoDeDescuentos</c>. Es la «línea descontado pendiente de giro» que el pago de la pagaduría referencia (FR-085, A9): mientras sea
/// <c>Deducted</c> o <c>PartiallyDeducted</c>, no esté reemplazada por un resultado posterior y ningún pago vigente la nombre
/// (<c>LND_LoanOperations.PayrollDeductionResultId</c>), está <b>pendiente de giro</b>. La ventana y el umbral se sellan al recibirla: un cambio
/// posterior del parámetro no la mueve. Hecho de sólo inserción (<see cref="IHechoInmutable"/>). (nuevo)
/// </summary>
public class PayrollDeductionResult : AuditableEntity, IHechoInmutable
{
    public int PayrollDeductionFileLineId { get; init; }

    public int PayrollDeductionFileId { get; init; }

    public int LoanId { get; init; }

    /// <summary>El recibo de <c>ResultadoDeDescuentos</c> en <c>LND_InboundMessages</c>; único con la línea.</summary>
    public int InboundMessageId { get; init; }

    public PayrollResultOutcome Outcome { get; init; }

    public decimal DeductedAmount { get; init; }

    public decimal NotDeductedAmount { get; init; }

    /// <summary>Fecha del descuento = fecha valor del pago que lo gire (FR-114, enmienda A9).</summary>
    public DateOnly? DeductionDate { get; init; }

    /// <summary>El código de la pagaduría, traducido por <c>LND_PayrollAgreementResultCodes</c>.</summary>
    public string? PaymasterResultCode { get; init; }

    public NonDeductionReason? NonDeductionReason { get; init; }

    /// <summary><see cref="DeductionDate"/> + la ventana de giro, sellada.</summary>
    public DateOnly? TransferDueDate { get; init; }

    /// <summary><c>Libranza.UmbralDeMoraDePagaduria</c> vigente al recibirla, sellado; nulo = sin valor (la mora de la pagaduría nunca se imputa).</summary>
    public int? PaymasterArrearsThresholdDays { get; init; }

    /// <summary>El resultado anterior de la misma línea que éste corrige.</summary>
    public int? SupersedesResultId { get; init; }

    /// <summary>¿Lo descontado queda para girar? <c>Deducted</c> o <c>PartiallyDeducted</c> con algo descontado.</summary>
    public bool DejaPendienteDeGiro => Outcome is PayrollResultOutcome.Deducted or PayrollResultOutcome.PartiallyDeducted && DeductedAmount > 0m;

    /// <summary>
    /// Sella la ventana de giro desde la fecha del descuento (FR-114): <c>DeductionDate + ventana</c>. Sin fecha del descuento, nula.
    /// </summary>
    public static DateOnly? VencimientoDelGiro(DateOnly? fechaDelDescuento, int ventanaDeGiroDias)
    {
        if (ventanaDeGiroDias < 0) throw new ArgumentOutOfRangeException(nameof(ventanaDeGiroDias), ventanaDeGiroDias, "La ventana de giro no es negativa.");
        return fechaDelDescuento?.AddDays(ventanaDeGiroDias);
    }
}
