using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>Maps to [dbo].[ADM_TenantSettings].</summary>
public class TenantSetting : AuditableEntity
{
    public int TenantId { get; set; }

    [MaxLength(200)]
    public string SettingKey { get; set; } = string.Empty;

    public string? SettingValue { get; set; }

    [MaxLength(30)]
    public string ValueType { get; set; } = "String";

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(5)]
    public string? ModulePrefix { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
}
