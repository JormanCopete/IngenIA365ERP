using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_ActivityPrograms] (cop_progactividad).
/// </summary>
public class ActivityProgram : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? ShortName { get; set; }

    // Navigation properties
    public ICollection<Course> Courses { get; set; } = [];
    public ICollection<RecreationalEvent> RecreationalEvents { get; set; } = [];
}
