using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_BankReconciliationMasters] (cnt_maeconcibanca).</summary>
public class BankReconciliationMaster : AuditableEntity
{
    public int AccountId { get; set; }
    public int BankId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    public decimal FinalBalance { get; set; }
    public int Status { get; set; }
    public bool IsClosed { get; set; }

    // Navigation
    public ChartOfAccount Account { get; set; } = null!;
    public Bank Bank { get; set; } = null!;
}
