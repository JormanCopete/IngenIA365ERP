using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Transactions;

/// <summary>
/// <c>PAY_PayrollAccountDeductions</c> (feature 014, US3, T089; E-02; contrato-de-cuentas §6.1): por corrida, empleado y cuenta, lo que la nómina descontó
/// de la cuota de aportes (<c>DESC_APORTES</c>) o de ahorro (<c>DESC_AHORROS</c>) que la Cartera informó exigible, lo que no descontó y por qué, y lo que
/// la Cartera tomó al aprobar por el contrato de cuentas (con su comprobante) o al reversar. Gemela de <c>PAY_PayrollLoanDeductions</c>. Nómina nunca
/// nombra tablas de la Cartera: la cuenta va por su <c>PublicId</c>, sin FK (FR-008). (nuevo)
/// </summary>
public class PayrollAccountDeduction : AuditableEntity
{
    public int PayrollRunId { get; set; }

    public int PayrollRunEmployeeId { get; set; }

    /// <summary>La línea <c>DESC_APORTES</c>/<c>DESC_AHORROS</c> del empleado (nula si no se descontó nada).</summary>
    public int? PayrollRunLineId { get; set; }

    public Guid AccountPublicId { get; set; }

    [MaxLength(20)]
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary><c>DESC_APORTES</c> o <c>DESC_AHORROS</c>.</summary>
    [MaxLength(20)]
    public string ConceptCode { get; set; } = string.Empty;

    public decimal ExpectedAmount { get; set; }

    public decimal DeductedAmount { get; set; }

    public decimal NotDeductedAmount { get; set; }

    /// <summary>El motivo del no descuento (<c>LoanNonDeductionReasons</c>: <c>DeductionCap</c>, <c>InsufficientNet</c>).</summary>
    [MaxLength(20)]
    public string? NotDeductedReason { get; set; }

    /// <summary>Lo que la Cartera tomó al aprobar.</summary>
    public decimal? AppliedAmount { get; set; }

    public PayrollLoanDeductionStatus Status { get; set; } = PayrollLoanDeductionStatus.Calculated;

    /// <summary>La operación de la cuenta (el comprobante de la consignación).</summary>
    public Guid? CarteraReceiptPublicId { get; set; }

    /// <summary>La operación del reverso, si la corrida se reversó.</summary>
    public Guid? CarteraReversalPublicId { get; set; }

    [MaxLength(80)]
    public string? ResultCode { get; set; }

    [MaxLength(500)]
    public string? ResultMessage { get; set; }

    public PayrollRun? PayrollRun { get; set; }
    public PayrollRunEmployee? PayrollRunEmployee { get; set; }
    public PayrollRunLine? PayrollRunLine { get; set; }
}
