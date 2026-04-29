using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_JournalEntryItems] (cnt_movitem).</summary>
public class JournalEntryItem : AuditableEntityLong
{
    public long? JournalEntryId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string? PersonTaxId { get; set; }
    public string? BranchCode { get; set; }
    public string? CostCenterCode { get; set; }
    public DateOnly TransactionDate { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
    public decimal BaseAmount { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Period { get; set; }
    public string? VoucherTypeCode { get; set; }
    public int? VoucherNumber { get; set; }

    // Navigation
    public JournalEntry? JournalEntry { get; set; }
}
