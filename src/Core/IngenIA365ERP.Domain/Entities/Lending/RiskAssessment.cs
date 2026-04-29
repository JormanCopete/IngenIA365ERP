using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_RiskAssessments].</summary>
public class RiskAssessment : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    [MaxLength(20)]
    public string PortfolioNumber { get; set; } = string.Empty;
    public int InstallmentNumber { get; set; }
    [MaxLength(8)]
    public string Period { get; set; } = string.Empty;
    public decimal InstallmentAmount { get; set; }
    public decimal ExtraInstallment { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal InsuranceAmount { get; set; }
    public decimal AdminAmount { get; set; }
    public decimal CapitalPayment { get; set; }
    public decimal Balance { get; set; }
    public decimal TotalBalance { get; set; }
    public int PortfolioClass { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
