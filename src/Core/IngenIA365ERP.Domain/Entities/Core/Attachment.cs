using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Attachments] (nueva — file attachment metadata, BIGINT PK).
/// </summary>
public class Attachment : AuditableEntityLong
{
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    [MaxLength(2000)]
    public string StoragePath { get; set; } = string.Empty;

    [MaxLength(50)]
    public string StorageProvider { get; set; } = "Local";
}
