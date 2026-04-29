using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_InvoiceMasters].</summary>
public class InvoiceMaster : AuditableEntity
{
    [MaxLength(20)]
    public string? PersonCode { get; set; }
    public int? AccountingPeriod { get; set; }
    public int? Cycle { get; set; }
    public decimal? InvoicedAmount { get; set; }
    public DateOnly? PaymentDeadline { get; set; }
    [MaxLength(120)]
    public string? Barcode { get; set; }
    [MaxLength(20)]
    public string? UserId { get; set; }
    public DateTime? SystemDate { get; set; }
    [MaxLength(5)]
    public string? InvoiceCode { get; set; }
    [MaxLength(200)]
    public string? EncodedData { get; set; }
    public decimal LastPaymentAmount { get; set; }
    [MaxLength(20)]
    public string LastPaymentDate { get; set; } = string.Empty;
    [MaxLength(3)]
    public string PaymentType { get; set; } = string.Empty;
    public long InvoiceConsecutive { get; set; }
}
