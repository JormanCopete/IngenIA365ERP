using Microsoft.AspNetCore.Identity;

namespace IngenIA365ERP.Identity.Models;

public class ApplicationRole : IdentityRole<int>
{
    public Guid PublicId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tenant scope (legacy ASP.NET Identity). Nullable porque entornos
    /// previos mezclaban roles globales (TenantId NULL) con tenant-scoped.
    /// El modelo nuevo (<c>Role</c> en <c>SEC_Roles</c>) reemplaza a esta
    /// entidad — solo se mantiene por compat de seeder legacy.
    /// </summary>
    public string? TenantId { get; set; }

    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
}
