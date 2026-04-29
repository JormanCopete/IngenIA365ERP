using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_MinuteAttendees].</summary>
public class MinuteAttendee : AuditableEntityLong
{
    [MaxLength(2)]
    public string MinutesType { get; set; } = string.Empty;
    [MaxLength(20)]
    public string MinutesNumber { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public DateTime SystemDate { get; set; }
}
