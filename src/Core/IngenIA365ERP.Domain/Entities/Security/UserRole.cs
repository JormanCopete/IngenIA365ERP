using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_UserRoles].</summary>
public class UserRole : AuditableEntity
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedAt { get; set; }

    [MaxLength(100)]
    public string AssignedBy { get; set; } = string.Empty;

    // Navigation
    public User? User { get; set; }
    public Role? Role { get; set; }
}
