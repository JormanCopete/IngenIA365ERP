using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Audit;

/// <summary>Maps to [dbo].[AUD_DefaultChanges] (cop_moraud).</summary>
public class DefaultChange : AuditableEntityLong
{
    [MaxLength(10)]
    public string Action { get; set; } = string.Empty;

    public DateTime ActionDate { get; set; }
    public int? UserId { get; set; }

    [MaxLength(100)]
    public string? UserName { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    public int? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? AdditionalInfo { get; set; }
}
