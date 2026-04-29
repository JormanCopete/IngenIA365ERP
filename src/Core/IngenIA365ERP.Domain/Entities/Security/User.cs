using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_Users] (sys_sasusu).</summary>
public class User : AuditableEntity
{
    public int? PersonId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? PasswordSalt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; }
    public bool IsMfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastPasswordChangeAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEndAt { get; set; }
    public string? LegacyLogin { get; set; }
    public decimal CanApproveLoansMin { get; set; }
    public decimal CanApproveLoansMax { get; set; }
    public bool CanOverrideLimits { get; set; }
    public string? IdentificationNumber { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public ICollection<Role> Roles { get; set; } = [];
}
