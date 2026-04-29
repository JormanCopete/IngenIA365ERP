using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_VoucherTypes] (sys_compro02).</summary>
public class VoucherType : AuditableEntity
{
    public string? LegacyCode { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? DocumentType { get; set; }
    public string? AccountingAccountCode { get; set; }
    public bool UpdatesAccounting { get; set; }
    public long NextSequenceNumber { get; set; }
    public string? EquivalentAccountCode { get; set; }
    public bool RequiresDetail { get; set; }
    public string? PrintFormat { get; set; }
    public string? CostCenterCode { get; set; }
    public bool ControlSequential { get; set; }
    public string? BankReconciliationCode { get; set; }
    public string? DebitCredit { get; set; }
    public bool HasValidator { get; set; }
    public string? ValidatorPort { get; set; }
    public string? AutomaticDetail { get; set; }
    public bool TreasuryRestriction { get; set; }
    public bool Affects3xMil { get; set; }
    public string? DocumentControlType { get; set; }
    public bool MoneyLaundering { get; set; }
    public string? ModuleCode { get; set; }

    // Additional legacy columns
    public string? EquivalentVoucherCode { get; set; }
    public string? EquivalentDocumentCode { get; set; }
    public string? AccountCode2 { get; set; }
    public string? Nature { get; set; }
    public short? DianReportFlag { get; set; }
    public short? SequentialFormat { get; set; }
    public string? ReceiptInvoice { get; set; }
    public string? InvoiceControlCode { get; set; }
    public string? ReturnOverdue { get; set; }
    public int? ConversionRate { get; set; }

    // Navigation
    public ICollection<AccountingDocument> Documents { get; set; } = [];
}
