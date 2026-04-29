using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_SubZones].</summary>
public class SubZone : AuditableEntity
{
    public int Code { get; set; }
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(50)]
    public string ShortName { get; set; } = string.Empty;
    [MaxLength(120)]
    public string Address { get; set; } = string.Empty;
    [MaxLength(120)]
    public string Phone { get; set; } = string.Empty;
}
