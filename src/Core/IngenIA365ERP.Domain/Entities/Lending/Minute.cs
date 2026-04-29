using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_Minutes].</summary>
public class Minute : AuditableEntity
{
    [MaxLength(2)]
    public string MinutesType { get; set; } = string.Empty;
    [MaxLength(20)]
    public string MinutesNumber { get; set; } = string.Empty;
    public DateTime OpeningDate { get; set; }
    public DateTime? ClosingDate { get; set; }
    public string? Description { get; set; }
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;
    public DateTime SystemDate { get; set; }
    [MaxLength(2)]
    public string EntityType { get; set; } = string.Empty;
}
