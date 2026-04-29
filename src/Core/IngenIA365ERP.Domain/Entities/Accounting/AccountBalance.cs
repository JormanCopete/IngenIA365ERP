using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_AccountBalances] (cnt_salage — normalized).</summary>
public class AccountBalance : AuditableEntityLong
{
    public int AccountId { get; set; }
    public int PeriodYear { get; set; }
    public byte PeriodMonth { get; set; }
    public int BranchId { get; set; }
    public int CostCenterId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }

    // Navigation
    public ChartOfAccount Account { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public CostCenter CostCenter { get; set; } = null!;
}
