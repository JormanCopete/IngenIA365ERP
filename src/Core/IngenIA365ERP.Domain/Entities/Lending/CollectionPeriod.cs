using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CollectionPeriods] (cop_gespara).</summary>
public class CollectionPeriod : AuditableEntity
{
    public int Period { get; set; }
    [MaxLength(15)]
    public string UserId { get; set; } = string.Empty;
    [MaxLength(20)]
    public string LastPersonCode { get; set; } = string.Empty;
    public int DaysFrom { get; set; }
    public int DaysTo { get; set; }
    public int CreditLineFrom { get; set; }
    public int CreditLineTo { get; set; }
    [MaxLength(2)]
    public string DeductionClass { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CompanyFrom { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CompanyTo { get; set; } = string.Empty;
    [MaxLength(2)]
    public string SortOrder { get; set; } = string.Empty;
    [MaxLength(2)]
    public string LegalCollection { get; set; } = string.Empty;
}
