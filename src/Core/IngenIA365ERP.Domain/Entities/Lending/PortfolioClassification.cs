using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PortfolioClassifications] (cop_copclas).</summary>
public class PortfolioClassification : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CreditLineCode { get; set; } = string.Empty;
    public int PortfolioNumber { get; set; }
    public int AccountingPeriod { get; set; }
    public int CreditClass { get; set; }
    public int GuaranteeClass { get; set; }
    public int DeductionClass { get; set; }
    [MaxLength(2)]
    public string Category { get; set; } = string.Empty;
    public int ConceptCode { get; set; }
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    [MaxLength(50)]
    public string PersonName { get; set; } = string.Empty;
    public decimal TotalBalance { get; set; }
    public decimal InterestBalance { get; set; }
    public decimal DefaultBalance { get; set; }
    public decimal OrderBalance { get; set; }
    public decimal ProvisionBalance { get; set; }
    public decimal Rate { get; set; }
    public int DaysOverdue { get; set; }
    public decimal ContributionAmount { get; set; }
    public decimal InterestProvision { get; set; }
    public decimal DefaultOrderBalance { get; set; }
    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }
}
