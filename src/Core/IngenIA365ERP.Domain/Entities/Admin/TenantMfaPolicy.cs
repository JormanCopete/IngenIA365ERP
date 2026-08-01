using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Política "MFA obligatorio" por tenant (FR-003a a FR-003d).
/// Maps to [dbo].[ADM_TenantMfaPolicies]. Único por TenantId.
/// </summary>
public class TenantMfaPolicy : AuditableEntity
{
    public Guid TenantId { get; private set; }

    public bool IsRequired { get; private set; }

    public DateTime? ActivatedAt { get; private set; }
    public Guid? ActivatedByUserId { get; private set; }

    public DateTime? DeactivatedAt { get; private set; }
    public Guid? DeactivatedByUserId { get; private set; }

    // EF Core
    private TenantMfaPolicy() { }

    /// <summary>Crea la política inicial para un tenant (por defecto deshabilitada).</summary>
    public static TenantMfaPolicy CreateForTenant(Guid tenantId) =>
        new() { TenantId = tenantId, IsRequired = false };

    /// <summary>Activa la política. Idempotente: si ya está activa, no hace nada.</summary>
    public void Enable(Guid byUserId, DateTime now)
    {
        if (IsRequired) return;

        IsRequired = true;
        ActivatedAt = now;
        ActivatedByUserId = byUserId;
        DeactivatedAt = null;
        DeactivatedByUserId = null;
    }

    /// <summary>Desactiva la política. Idempotente.</summary>
    public void Disable(Guid byUserId, DateTime now)
    {
        if (!IsRequired) return;

        IsRequired = false;
        DeactivatedAt = now;
        DeactivatedByUserId = byUserId;
    }
}
