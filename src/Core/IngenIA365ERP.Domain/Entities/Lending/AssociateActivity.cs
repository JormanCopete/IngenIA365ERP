using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_AssociateActivities].</summary>
public class AssociateActivity : AuditableEntity
{
    [MaxLength(2)]
    public string ActivityType { get; set; } = string.Empty;
    [MaxLength(20)]
    public string ActivityCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryId { get; set; } = string.Empty;
    public DateOnly EnrollmentDate { get; set; }
    [MaxLength(120)]
    public string? Remarks { get; set; }
    [MaxLength(2)]
    public string? Attended { get; set; }
    public DateTime? AttendanceDate { get; set; }
    public DateTime? ActivityStartDate { get; set; }
    public DateTime? ActivityEndDate { get; set; }
}
