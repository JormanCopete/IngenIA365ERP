using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollDeductionUnmatchedLines</c> (feature 014, T091; D36; mensajes.md §5.7, Edge Cases): una fila de la respuesta de la pagaduría que la Cartera
/// no pudo aplicar —un código sin equivalencia en el archivo para ese empleado, un empleado que el archivo no tiene o lo descontado que sobra sobre lo
/// enviado—. Queda en «no aplicados» con su motivo y no bloquea lo demás del mensaje. Hecho de sólo inserción. (nuevo)
/// </summary>
public class PayrollDeductionUnmatchedLine : AuditableEntity, IHechoInmutable
{
    public int PayrollDeductionFileId { get; init; }

    /// <summary>El recibo del <c>ResultadoDeDescuentos</c> que la trajo.</summary>
    public int InboundMessageId { get; init; }

    public string PayerEmployeeId { get; init; } = string.Empty;

    public string PaymasterConceptCode { get; init; } = string.Empty;

    public decimal ExpectedAmount { get; init; }

    public decimal DeductedAmount { get; init; }

    /// <summary>
    /// <c>Lending.PayrollFile.ConceptWithoutEquivalence</c>, <c>.EmployeeWithoutEquivalence</c> o <c>.DeductedExceedsSent</c> (lo descontado que sobró).
    /// </summary>
    public string ReasonCode { get; init; } = string.Empty;

    public string? PaymasterResultCode { get; init; }

    public DateOnly? DeductionDate { get; init; }
}
