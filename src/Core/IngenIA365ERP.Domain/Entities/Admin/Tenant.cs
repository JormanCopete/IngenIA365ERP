using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>Maps to [dbo].[ADM_Tenants].</summary>
public class Tenant : AuditableEntity
{
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SchemaName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Subdomain { get; set; }

    [MaxLength(50)]
    public string PlanType { get; set; } = "Basic";

    public bool IsActive { get; set; } = true;
    public int MaxUsers { get; set; } = 10;
    public long StorageLimitMb { get; set; } = 5120;

    [MaxLength(100)]
    public string? DatabaseName { get; set; }

    [MaxLength(200)]
    public string ContactEmail { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? ContactPhone { get; set; }

    public DateTime? ActivatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }

    // Navigation
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<TenantSetting> Settings { get; set; } = [];
}
