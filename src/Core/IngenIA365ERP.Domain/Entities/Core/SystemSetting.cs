using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_SystemSettings] (nueva — key/value config store).
/// Replaces scattered config in multiple legacy tables.
/// </summary>
public class SystemSetting : AuditableEntity
{
    [MaxLength(200)]
    public string SettingKey { get; set; } = string.Empty;

    public string? SettingValue { get; set; }

    [MaxLength(20)]
    public string ValueType { get; set; } = "String";

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(10)]
    public string? ModulePrefix { get; set; }
}
