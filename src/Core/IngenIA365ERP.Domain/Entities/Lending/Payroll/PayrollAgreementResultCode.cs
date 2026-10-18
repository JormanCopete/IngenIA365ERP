using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollAgreementResultCodes</c> (feature 013, K2; data-model §24.4; FR-115): la traducción de un código de respuesta de la pagaduría
/// al resultado y al motivo de Cartera, por versión del convenio. Con <c>NotDeducted</c> o <c>PartiallyDeducted</c> el motivo es obligatorio
/// (<see cref="Valido"/>). (nuevo)
/// </summary>
public class PayrollAgreementResultCode : AuditableEntity
{
    public int PayrollAgreementVersionId { get; set; }

    /// <summary>El código tal como lo escribe la pagaduría; único por versión.</summary>
    public string PaymasterCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public PayrollResultOutcome Outcome { get; set; }

    /// <summary>Obligatorio con <c>NotDeducted</c> y <c>PartiallyDeducted</c>; nulo con <c>Deducted</c>.</summary>
    public NonDeductionReason? NonDeductionReason { get; set; }

    /// <summary>¿El motivo acompaña al resultado como pide data-model §24.4?</summary>
    public bool Valido => Outcome == PayrollResultOutcome.Deducted ? NonDeductionReason is null : NonDeductionReason is not null;
}
