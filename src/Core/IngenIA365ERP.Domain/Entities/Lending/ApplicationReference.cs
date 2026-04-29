using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ApplicationReferences] (cop_solreferencia).</summary>
public class ApplicationReference : AuditableEntityLong
{
    public int ApplicationNumber { get; set; }
    [MaxLength(2)]
    public string ReferenceType { get; set; } = string.Empty;
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(80)]
    public string? Address { get; set; }
    public int CityCode { get; set; }
    [MaxLength(25)]
    public string? Phone { get; set; }
}
