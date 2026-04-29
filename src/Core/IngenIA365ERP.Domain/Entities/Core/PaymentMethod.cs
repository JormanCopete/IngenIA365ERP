using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_PaymentMethods] (sys_forpago).
/// Composite PK converted to surrogate INT Id.
/// </summary>
public class PaymentMethod : AuditableEntity
{
    // Legacy composite key fields
    [MaxLength(10)]
    public string VoucherTypeCode { get; set; } = string.Empty;

    public long DocumentNumber { get; set; }

    // Payment breakdown
    public decimal Cash { get; set; }
    public decimal Check { get; set; }

    [MaxLength(10)]
    public string? BankCode { get; set; }

    [MaxLength(30)]
    public string? CheckNumber { get; set; }

    [MaxLength(30)]
    public string? AccountNumber { get; set; }

    public decimal DebitCard { get; set; }

    [MaxLength(30)]
    public string? DebitCardNumber { get; set; }

    public decimal CreditCard { get; set; }

    [MaxLength(30)]
    public string? CreditCardNumber { get; set; }

    public decimal OtherPayment { get; set; }

    [MaxLength(30)]
    public string? OtherPaymentNumber { get; set; }

    public int TitleAmount { get; set; }

    [MaxLength(60)]
    public string? TitleNumber { get; set; }

    [MaxLength(4)]
    public string? PaymentReason { get; set; }

    public int CashReceiptCount { get; set; }
    public int CheckReceiptCount { get; set; }
}
