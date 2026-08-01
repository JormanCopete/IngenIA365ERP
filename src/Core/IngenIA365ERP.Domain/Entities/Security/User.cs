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

    // T025 (Feature 002) — vínculo 1:1 con la identidad central en
    // ADM_CentralUsers (BD IngenIA365ERP_Admin). NO hay FK SQL formal
    // porque la tabla destino vive en otra BD; la integridad se garantiza
    // en TenantUserProvisioner (T056, US1). Los campos credencial/lockout
    // siguen declarados arriba mientras existan los handlers legacy de
    // Fase 0; US1/US2 los retirará junto con su mapeo EF.
    public Guid CentralUserId { get; set; }

    /// <summary>Email "actual" del usuario en la identidad central, replicado
    /// para listados/reportes del tenant sin cruzar BDs en cada query.</summary>
    public string? CentralUserPublicEmail { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<MfaBackupCode> MfaBackupCodes { get; set; } = [];
    public ICollection<PasswordHistory> PasswordHistory { get; set; } = [];
}
