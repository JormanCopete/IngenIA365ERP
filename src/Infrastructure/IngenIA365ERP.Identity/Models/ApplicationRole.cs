using Microsoft.AspNetCore.Identity;

namespace IngenIA365ERP.Identity.Models;

public class ApplicationRole : IdentityRole<int>
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
}
