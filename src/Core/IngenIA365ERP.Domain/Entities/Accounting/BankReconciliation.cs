using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_BankReconciliations] (cnt_concibanca).</summary>
public class BankReconciliation : AuditableEntityLong
{
    public int AccountId { get; set; }
    public int BankId { get; set; }
    public int? PeriodCode { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Description { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public bool IsReconciled { get; set; }
    public DateOnly? ReconciliationDate { get; set; }
    public int Status { get; set; }
    public bool IsAdditional { get; set; }
    public bool IsClosed { get; set; }
    public int? MovementSequence { get; set; }
    public string? ModuleCode { get; set; }

    // Navigation
    public ChartOfAccount Account { get; set; } = null!;
    public Bank Bank { get; set; } = null!;
}
