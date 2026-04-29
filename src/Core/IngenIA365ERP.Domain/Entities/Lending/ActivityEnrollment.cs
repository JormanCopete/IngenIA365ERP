using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ActivityEnrollments].</summary>
public class ActivityEnrollment : AuditableEntityLong
{
    [MaxLength(20)]
    public string ActivityCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryId { get; set; } = string.Empty;
    public DateTime RegistrationDate { get; set; }
    [MaxLength(2)]
    public string EntryType { get; set; } = string.Empty;
}
