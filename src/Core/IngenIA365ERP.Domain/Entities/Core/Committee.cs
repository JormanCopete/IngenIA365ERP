using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Committees] (cop_comite).
/// </summary>
public class Committee : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? ShortName { get; set; }

    [MaxLength(2)]
    public string? CommitteeType { get; set; }

    // Navigation properties
    public ICollection<CommitteeMember> Members { get; set; } = [];
    public ICollection<CulturalActivity> CulturalActivities { get; set; } = [];
    public ICollection<Sport> Sports { get; set; } = [];
    public ICollection<Course> Courses { get; set; } = [];
    public ICollection<RecreationalEvent> RecreationalEvents { get; set; } = [];
}
