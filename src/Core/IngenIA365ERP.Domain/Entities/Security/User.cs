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

    /// <summary>Operador global del SaaS (admin del producto, no de un tenant).</summary>
    public bool IsSaasOperator { get; set; }

    /// <summary>true si la última contraseña fue establecida por reset administrativo
    /// y debe ser cambiada en el siguiente login (FR-009).</summary>
    public bool MustChangePassword { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<MfaBackupCode> MfaBackupCodes { get; set; } = [];
    public ICollection<PasswordHistory> PasswordHistory { get; set; } = [];
}
