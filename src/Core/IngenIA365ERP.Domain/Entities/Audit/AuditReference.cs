using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Audit;

/// <summary>Maps to [dbo].[AUD_AuditReferences].</summary>
public class AuditReference : AuditableEntityLong
{
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    [MaxLength(10)]
    public string Action { get; set; } = string.Empty;

    public int? UserId { get; set; }

    [MaxLength(100)]
    public string? UserName { get; set; }

    public DateTime Timestamp { get; set; }

    [MaxLength(100)]
    public string? ExternalDocumentId { get; set; }

    [MaxLength(500)]
    public string? Summary { get; set; }
}
