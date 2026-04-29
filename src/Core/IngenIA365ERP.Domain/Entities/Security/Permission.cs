using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_Permissions].</summary>
public class Permission : AuditableEntity
{
    [MaxLength(100)]
    public string Resource { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
