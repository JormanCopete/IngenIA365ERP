using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_AccountGroups] (cnt_grupocuenta).</summary>
public class AccountGroup : AuditableEntity
{
    public int? GroupNumber { get; set; }
    public int? GroupType { get; set; }
    public string? AccountCode { get; set; }
    public string? Description { get; set; }
    public int? ReportOrder { get; set; }
    public int? Level { get; set; }
    public int? ParentGroupId { get; set; }
    public string? FinancialStatementCode { get; set; }
    public string? SpecialCode { get; set; }

    // Navigation
    public AccountGroup? ParentGroup { get; set; }
    public ICollection<AccountGroup> ChildGroups { get; set; } = [];
    public ICollection<AccountSubgroup> Subgroups { get; set; } = [];
}
