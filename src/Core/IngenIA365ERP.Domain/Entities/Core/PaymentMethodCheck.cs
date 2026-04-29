using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_PaymentMethodChecks] (sys_forpago_cheq).
/// </summary>
public class PaymentMethodCheck : AuditableEntity
{
    [MaxLength(10)]
    public string VoucherTypeCode { get; set; } = string.Empty;

    public long DocumentNumber { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(30)]
    public string CheckNumber { get; set; } = string.Empty;

    [MaxLength(10)]
    public string BankCode { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? AccountNumber { get; set; }

    [MaxLength(30)]
    public string? LegacyUser { get; set; }
}
