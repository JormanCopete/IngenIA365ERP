using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_JournalEntries] (cnt_movimto).</summary>
public class JournalEntry : AuditableEntityLong
{
    public long? LegacySequence { get; set; }
    public string VoucherTypeCode { get; set; } = string.Empty;
    public long DocumentNumber { get; set; }
    public int AccountId { get; set; }
    public int? PersonId { get; set; }
    public int BranchId { get; set; }
    public int CostCenterId { get; set; }
    public string? PeriodCode { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? Description { get; set; }
    public string? AuxiliaryDocument { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal BaseAmount { get; set; }
    public int Status { get; set; }
    public string? InvoiceNumber { get; set; }
    public int? AuxiliaryRequestId { get; set; }
    public string? UserName { get; set; }
    public string? DocumentType { get; set; }

    // Navigation
    public ChartOfAccount Account { get; set; } = null!;
    public Person? Person { get; set; }
    public Branch Branch { get; set; } = null!;
    public CostCenter CostCenter { get; set; } = null!;
    public ICollection<JournalEntryItem> Items { get; set; } = [];
}
