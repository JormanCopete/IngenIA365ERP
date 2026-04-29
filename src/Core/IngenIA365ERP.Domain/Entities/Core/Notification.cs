using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Notifications] (nueva — notification log, BIGINT PK).
/// </summary>
public class Notification : AuditableEntityLong
{
    public int? TemplateId { get; set; }

    public int? RecipientPersonId { get; set; }

    [MaxLength(10)]
    public string Channel { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Subject { get; set; }

    public string? Body { get; set; }

    public DateTime? SentAt { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public NotificationTemplate? Template { get; set; }
    public Person? RecipientPerson { get; set; }
}
