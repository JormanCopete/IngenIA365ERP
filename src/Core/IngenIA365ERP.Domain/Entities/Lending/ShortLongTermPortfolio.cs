using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ShortLongTermPortfolio].</summary>
public class ShortLongTermPortfolio : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(60)]
    public string? PersonName { get; set; }
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    [MaxLength(15)]
    public string AccountCode { get; set; } = string.Empty;
    public decimal? Balance { get; set; }
    public decimal? Month1 { get; set; }
    public decimal? Month2 { get; set; }
    public decimal? Month3 { get; set; }
    public decimal? Month4 { get; set; }
    public decimal? Month5 { get; set; }
    public decimal? Month6 { get; set; }
    public decimal? Month7 { get; set; }
    public decimal? Month8 { get; set; }
    public decimal? Month9 { get; set; }
    public decimal? Month10 { get; set; }
    public decimal? Month11 { get; set; }
    public decimal? Month12 { get; set; }
    public decimal? MoreThan12 { get; set; }
    public decimal? Total { get; set; }
    [MaxLength(60)]
    public string? Description { get; set; }
    [MaxLength(20)]
    public string? AffectsFlag { get; set; }
    [MaxLength(3)]
    public string? FogaClass { get; set; }
    [MaxLength(60)]
    public string? CompanyName { get; set; }
    [MaxLength(60)]
    public string? CostCenterName { get; set; }
    public int Period { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
