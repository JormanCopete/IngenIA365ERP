using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_RolePermissions].</summary>
public class RolePermission : AuditableEntity
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }

    // Navigation
    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}
