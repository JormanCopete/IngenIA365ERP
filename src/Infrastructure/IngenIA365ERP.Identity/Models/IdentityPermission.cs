namespace IngenIA365ERP.Identity.Models;

public class IdentityPermission
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string Module { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;
    public string? Description { get; set; }
}
