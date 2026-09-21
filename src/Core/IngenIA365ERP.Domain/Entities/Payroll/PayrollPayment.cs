using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_PayrollPayments]. Marca de «pagado» de un empleado en una
/// corrida aprobada (FR-040). Cualquier pago vigente bloquea la reversión del período.
/// Retirar la marca exige permiso y motivo, y queda en auditoría.
/// </summary>
public class PayrollPayment : AuditableEntity
{
    public int PayrollRunEmployeeId { get; set; }
    public DateTime PaidAt { get; set; }
    public PayrollPaymentMethod PaymentMethod { get; set; }

    [MaxLength(60)]
    public string? Reference { get; set; }

    [MaxLength(100)]
    public string PaidBy { get; set; } = string.Empty;

    public bool IsReverted { get; set; }
    public DateTime? RevertedAt { get; set; }

    [MaxLength(100)]
    public string? RevertedBy { get; set; }

    [MaxLength(300)]
    public string? RevertReason { get; set; }

    /// <summary>Feature 010 (US8): el archivo de dispersión cuyo «marcar enviado» dejó esta marca; nulo si fue manual.</summary>
    public int? BankDisbursementFileId { get; set; }

    public PayrollRunEmployee? RunEmployee { get; set; }
}
