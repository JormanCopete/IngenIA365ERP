using Microsoft.AspNetCore.Identity;

namespace IngenIA365ERP.Identity.Models;

public class ApplicationUser : IdentityUser<int>
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public bool MustChangePassword { get; set; }
    public bool MfaEnabled { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? LastLoginIp { get; set; }
    public string? LastLoginUserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
