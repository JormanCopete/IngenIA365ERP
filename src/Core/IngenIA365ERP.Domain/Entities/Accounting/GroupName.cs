using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_GroupNames] (cnt_nombregrupo).</summary>
public class GroupName : AuditableEntity
{
    public int GroupNumber { get; set; }
    public string Name { get; set; } = string.Empty;
}
