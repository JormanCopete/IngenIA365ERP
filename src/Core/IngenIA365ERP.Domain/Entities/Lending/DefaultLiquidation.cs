using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_DefaultLiquidations] (cop_liqmor).</summary>
public class DefaultLiquidation : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public DateTime LiquidationDate { get; set; }
    public int LiquidationBase { get; set; }
    public int LiquidationDays { get; set; }
    public int LiquidationAmount { get; set; }
    public DateTime LastLiquidationDate { get; set; }
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    public decimal LiquidationRate { get; set; }
    public int DeductionClass { get; set; }
    public int GraceDays { get; set; }
    public decimal UsuryRate { get; set; }
    public int LiquidationType { get; set; }
    public DateTime SystemDate { get; set; }
    public int AccrualPeriod { get; set; }
    [MaxLength(20)]
    public string? UserId { get; set; }
    [MaxLength(50)]
    public string UserFullName { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
