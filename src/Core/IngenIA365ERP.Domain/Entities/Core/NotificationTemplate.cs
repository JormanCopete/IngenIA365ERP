using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_NotificationTemplates] (nueva — email/SMS/push templates).
/// </summary>
public class NotificationTemplate : AuditableEntity
{
    [MaxLength(200)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Channel { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Subject { get; set; }

    public string BodyTemplate { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Notification> Notifications { get; set; } = [];
}
