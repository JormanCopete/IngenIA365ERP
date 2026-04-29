using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_SubgroupNames] (cnt_nombresubgrupo).</summary>
public class SubgroupName : AuditableEntity
{
    public int SubgroupNumber { get; set; }
    public string Name { get; set; } = string.Empty;
}
