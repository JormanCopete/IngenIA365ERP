using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_PayslipDeliveries]. Cada intento de envío manual del comprobante
/// de pago por correo (FR-026): destinatario, quién lo pidió, resultado. No existe
/// envío automático.
/// </summary>
public class PayslipDelivery : AuditableEntity
{
    public int PayrollRunEmployeeId { get; set; }

    [MaxLength(256)]
    public string RecipientEmail { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }

    [MaxLength(100)]
    public string RequestedBy { get; set; } = string.Empty;

    public DateTime? SentAt { get; set; }
    public PayslipDeliveryStatus Status { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public int AttemptNumber { get; set; } = 1;

    public PayrollRunEmployee? RunEmployee { get; set; }
}
