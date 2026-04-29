using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_Documents] (cnt_docmto).</summary>
public class AccountingDocument : AuditableEntityLong
{
    public string? LegacyCompronte { get; set; }
    public long? LegacyNumero { get; set; }
    public string VoucherTypeCode { get; set; } = string.Empty;
    public long DocumentNumber { get; set; }
    public string? Detail { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public DateOnly DocumentDate { get; set; }
    public bool IsClosed { get; set; }
    public bool IsVoided { get; set; }
    public int? BeneficiaryId { get; set; }
    public int? PeriodCode { get; set; }
    public string? CheckNumber { get; set; }
    public short? BankId { get; set; }
    public string? ModuleCode { get; set; }
    public string? PaymentMethod { get; set; }
    public long? InvoiceNumber { get; set; }

    // Navigation
    public VoucherType VoucherType { get; set; } = null!;
}
