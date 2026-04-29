using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_AccountSubgroups] (cnt_subgrupocuenta).</summary>
public class AccountSubgroup : AuditableEntity
{
    public int? GroupId { get; set; }
    public int SubgroupNumber { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? ReportOrder { get; set; }
    public int? Level { get; set; }
    public int? ParentSubgroupId { get; set; }
    public string? FinancialStatementCode { get; set; }
    public string? SpecialCode { get; set; }

    // Navigation
    public AccountGroup? Group { get; set; }
    public AccountSubgroup? ParentSubgroup { get; set; }
    public ICollection<AccountSubgroup> ChildSubgroups { get; set; } = [];
}
