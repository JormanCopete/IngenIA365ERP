using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Transactions;

/// <summary>
/// Maps to [dbo].[PAY_PayrollLoanDeductions] (feature 013, T0155; data-model parte 3 §7.1, DE-07, FR-112). El descuento de Cartera
/// de la nómina ordinaria por empleado y obligación: lo escribe <c>CalculatePayrollRunCommand</c> (<see cref="PayrollLoanDeductionStatus.Calculated"/>,
/// con lo esperado, lo descontado, lo no descontado y el motivo) y lo completa <c>ApprovePayrollRunCommand</c> con la respuesta
/// del contrato de obligaciones (<c>RecaudoPorNomina</c>), en la misma transacción que el NM; la reversión de la nómina la deja
/// <see cref="PayrollLoanDeductionStatus.Reversed"/>. La obligación es <see cref="ObligationPublicId"/>, sin FK a Cartera (FR-008):
/// ninguna tabla <c>PAY_</c> apunta a <c>LND_</c>. (nuevo)
/// </summary>
public class PayrollLoanDeduction : AuditableEntity
{
    /// <summary>La versión de la corrida que lo calculó.</summary>
    public int PayrollRunId { get; set; }

    public int PayrollRunEmployeeId { get; set; }

    /// <summary>La línea <c>DESC_CARTERA</c>; nula si no se descontó nada.</summary>
    public int? PayrollRunLineId { get; set; }

    public Guid ObligationPublicId { get; set; }

    [MaxLength(30)]
    public string ObligationNumber { get; set; } = string.Empty;

    /// <summary>La cuota del período según <c>CuotasDeNominaAsync</c>.</summary>
    public decimal ExpectedAmount { get; set; }

    /// <summary>Lo que el cálculo descontó: la cuota va primera entre las deducciones autorizadas, sujeta al tope.</summary>
    public decimal DeductedAmount { get; set; }

    /// <summary><see cref="ExpectedAmount"/> − <see cref="DeductedAmount"/>.</summary>
    public decimal NotDeductedAmount { get; set; }

    /// <summary>Texto de <c>MotivoDeNoDescuento</c> (<c>DeductionCap</c>, <c>InsufficientNet</c>…; C56).</summary>
    [MaxLength(20)]
    public string? NotDeductedReason { get; set; }

    /// <summary>Lo que Cartera aplicó al aprobar; con rechazo o excedente la línea baja a este valor y el neto sube.</summary>
    public decimal? AppliedAmount { get; set; }

    public PayrollLoanDeductionStatus Status { get; set; } = PayrollLoanDeductionStatus.Calculated;

    /// <summary>El comprobante de aplicación (la operación de pago de Cartera).</summary>
    public Guid? CarteraReceiptPublicId { get; set; }

    /// <summary>La operación de reverso, al reversar la ordinaria.</summary>
    public Guid? CarteraReversalPublicId { get; set; }

    /// <summary>Resultado del contrato (<c>Applied</c>, <c>AlreadyApplied</c>, <c>Rejected</c>) o el código del rechazo.</summary>
    [MaxLength(80)]
    public string? ResultCode { get; set; }

    [MaxLength(500)]
    public string? ResultMessage { get; set; }

    public PayrollRun? PayrollRun { get; set; }
    public PayrollRunEmployee? PayrollRunEmployee { get; set; }
    public PayrollRunLine? PayrollRunLine { get; set; }
}
