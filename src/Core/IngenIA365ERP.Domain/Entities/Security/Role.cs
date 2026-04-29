using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_Roles].</summary>
public class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<User> Users { get; set; } = [];
}
