using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PayrollDeductions] (cop_nomdes).</summary>
public class PayrollDeduction : AuditableEntityLong
{
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string BranchId { get; set; } = string.Empty;
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    public int Period { get; set; }
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IsAdditional { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    [MaxLength(50)]
    public string Description { get; set; } = string.Empty;
    [MaxLength(2)]
    public string PaymentCycle { get; set; } = string.Empty;
    public decimal ContributionAmount { get; set; }
    public decimal LoanAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal ExtraAmount { get; set; }
    public decimal DefaultAmount { get; set; }
    public decimal InsuranceAmount { get; set; }
    public decimal AdminAmount { get; set; }
    public decimal OtherAmount { get; set; }
    public decimal ContributionApplied { get; set; }
    public decimal LoanApplied { get; set; }
    public decimal InterestApplied { get; set; }
    public decimal ExtraApplied { get; set; }
    public decimal DefaultApplied { get; set; }
    public decimal InsuranceApplied { get; set; }
    public decimal AdminApplied { get; set; }
    public decimal OtherApplied { get; set; }
    [MaxLength(20)]
    public string CodeudorCode { get; set; } = string.Empty;

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
