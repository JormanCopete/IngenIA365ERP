using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Treasury;

/// <summary>Maps to [dbo].[TRS_Invoices] (TES_FACTURA).</summary>
public class TreasuryInvoice : AuditableEntityLong
{
    [MaxLength(5)]
    public string ConceptCode { get; set; } = string.Empty;

    public long ConsecutiveNumber { get; set; }
    public DateOnly EntryDate { get; set; }
    public int? PeriodCode { get; set; }

    [MaxLength(20)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateOnly InvoiceDate { get; set; }
    public int PersonId { get; set; }
    public DateOnly? DueDate { get; set; }

    [MaxLength(15)]
    public string? AccountCode { get; set; }

    [MaxLength(10)]
    public string? CostCenterId { get; set; }

    [MaxLength(10)]
    public string? BranchId { get; set; }

    [MaxLength(15)]
    public string? DocumentCode { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public decimal Amount { get; set; }
    public DateOnly? ScheduledDate { get; set; }
    public DateOnly? PaymentDate { get; set; }

    [MaxLength(5)]
    public string? VoucherCode { get; set; }

    public int? VoucherNumber { get; set; }

    [MaxLength(10)]
    public string? BankId { get; set; }

    public int? CheckNumber { get; set; }
    public decimal CheckAmount { get; set; }

    [MaxLength(2)]
    public string? Status { get; set; }

    [MaxLength(5)]
    public string? DocumentType { get; set; }

    [MaxLength(20)]
    public string? DocumentNumber { get; set; }

    [MaxLength(5)]
    public string? PaymentForm { get; set; }

    public bool? HasCommission { get; set; }
}
