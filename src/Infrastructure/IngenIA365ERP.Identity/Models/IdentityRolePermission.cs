namespace IngenIA365ERP.Identity.Models;

public class IdentityRolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public ApplicationRole Role { get; set; } = null!;
    public IdentityPermission Permission { get; set; } = null!;
}
