using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Courses] (sys_curso).
/// </summary>
public class Course : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? ShortName { get; set; }

    public int Duration { get; set; }

    public int? TeachingEntityId { get; set; }

    public int EducationType { get; set; }

    public decimal Percentage { get; set; }

    public decimal Amount { get; set; }

    public int? CommitteeId { get; set; }

    public int? ActivityProgramId { get; set; }

    // Navigation properties
    public ExternalEntity? TeachingEntity { get; set; }
    public Committee? Committee { get; set; }
    public ActivityProgram? ActivityProgram { get; set; }
}
