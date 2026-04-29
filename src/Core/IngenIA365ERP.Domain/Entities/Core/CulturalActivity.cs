using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_CulturalActivities] (sys_cultura54).
/// </summary>
public class CulturalActivity : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? ShortName { get; set; }

    public int? CommitteeId { get; set; }

    // Navigation properties
    public Committee? Committee { get; set; }
}
