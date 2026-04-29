using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CollectionCases] (cop_maegescob).</summary>
public class CollectionCase : AuditableEntityLong
{
    public int Period { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;
    public DateOnly? PromiseDate { get; set; }
    public DateOnly ManagementDate { get; set; }
    public DateOnly? ManagementStartDate { get; set; }
    public string Description { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    public decimal TotalOverdueAmount { get; set; }
    [MaxLength(2)]
    public string? EmailSent { get; set; }
    public int CreditLineFrom { get; set; }
    public int CreditLineTo { get; set; }
    public int DaysFrom { get; set; }
    public int DaysTo { get; set; }
    [MaxLength(2)]
    public string DeductionClass { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CompanyFrom { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CompanyTo { get; set; } = string.Empty;
    [MaxLength(2)]
    public string LegalCollection { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IsManaged { get; set; } = string.Empty;
    [MaxLength(2)]
    public string? IsCumulative { get; set; }
}
