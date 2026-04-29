using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_BankReconciliationFlats] (cnt_concibancaplano).</summary>
public class BankReconciliationFlat : AuditableEntityLong
{
    public int? PeriodCode { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountType { get; set; }
    public string? TransactionCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? TransactionDate { get; set; }
    public string? DocumentNumber { get; set; }
    public decimal? Amount { get; set; }
    public string? TransactionType { get; set; }
    public string? UserName { get; set; }
    public DateTime? SystemDate { get; set; }
    public bool IsProcessed { get; set; }
    public string? Description { get; set; }
}
