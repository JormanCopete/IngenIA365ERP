using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_RecreationalEvents] (sys_recreacion).
/// </summary>
public class RecreationalEvent : AuditableEntity
{
    [MaxLength(20)]
    public string? LegacyCode { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2)]
    public string? ActivityType { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal Percentage { get; set; }

    public decimal Amount { get; set; }

    public int? CommitteeId { get; set; }

    [MaxLength(60)]
    public string? ActivitySubtype { get; set; }

    public int? Capacity { get; set; }

    public bool ControlNovelty { get; set; }

    public int? ActivityProgramId { get; set; }

    // Navigation properties
    public Committee? Committee { get; set; }
    public ActivityProgram? ActivityProgram { get; set; }
}
