using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_Zones].</summary>
public class Zone : AuditableEntity
{
    public int ZoneId { get; set; }
    public int Code { get; set; }
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(50)]
    public string ShortName { get; set; } = string.Empty;
    [MaxLength(120)]
    public string Address { get; set; } = string.Empty;
    [MaxLength(120)]
    public string Phone { get; set; } = string.Empty;
    public int SubZoneId { get; set; }
}
