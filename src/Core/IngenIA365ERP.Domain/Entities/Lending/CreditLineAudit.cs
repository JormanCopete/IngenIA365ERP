using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CreditLineAudit].</summary>
public class CreditLineAudit : AuditableEntityLong
{
    [MaxLength(2)]
    public string Action { get; set; } = string.Empty;
    public int? CreditLineId { get; set; }
    [MaxLength(40)]
    public string? Description_Old { get; set; }
    [MaxLength(40)]
    public string? Description_New { get; set; }
    [MaxLength(2)]
    public string? AllowExtension_Old { get; set; }
    [MaxLength(2)]
    public string? AllowExtension_New { get; set; }
    [MaxLength(2)]
    public string? DefaultInterest_Old { get; set; }
    [MaxLength(2)]
    public string? DefaultInterest_New { get; set; }
    public decimal? InterestRate_Old { get; set; }
    public decimal? InterestRate_New { get; set; }
    [MaxLength(2)]
    public string? InterestType_Old { get; set; }
    [MaxLength(2)]
    public string? InterestType_New { get; set; }
    public int? MaxTerm_Old { get; set; }
    public int? MaxTerm_New { get; set; }
    public decimal? CreditLimit_Old { get; set; }
    public decimal? CreditLimit_New { get; set; }
    public decimal? ExtraRate_Old { get; set; }
    public decimal? ExtraRate_New { get; set; }
    [MaxLength(2)]
    public string? FinancialInterest_Old { get; set; }
    [MaxLength(2)]
    public string? FinancialInterest_New { get; set; }
    [MaxLength(2)]
    public string? GuaranteeClass_Old { get; set; }
    [MaxLength(2)]
    public string? GuaranteeClass_New { get; set; }
    public decimal? MaxAmount_Old { get; set; }
    public decimal? MaxAmount_New { get; set; }
    [MaxLength(2)]
    public string? AffectsFlag_Old { get; set; }
    [MaxLength(2)]
    public string? AffectsFlag_New { get; set; }
    public decimal? AccrualRate_Old { get; set; }
    public decimal? AccrualRate_New { get; set; }
    [MaxLength(2)]
    public string? CapitalForm_Old { get; set; }
    [MaxLength(2)]
    public string? CapitalForm_New { get; set; }
    public decimal? AdminRate_Old { get; set; }
    public decimal? AdminRate_New { get; set; }
    [MaxLength(2)]
    public string? InstallmentType_Old { get; set; }
    [MaxLength(2)]
    public string? InstallmentType_New { get; set; }
    [MaxLength(2)]
    public string? DebitCreditFlag_Old { get; set; }
    [MaxLength(2)]
    public string? DebitCreditFlag_New { get; set; }
    public decimal? InsuranceRate_Old { get; set; }
    public decimal? InsuranceRate_New { get; set; }
    public decimal? MinContribution_Old { get; set; }
    public decimal? MinContribution_New { get; set; }
    [MaxLength(15)]
    public string? AccountCode_Old { get; set; }
    [MaxLength(15)]
    public string? AccountCode_New { get; set; }
    [MaxLength(2)]
    public string? GracePeriod_Old { get; set; }
    [MaxLength(2)]
    public string? GracePeriod_New { get; set; }
    public decimal? AdminMin_Old { get; set; }
    public decimal? AdminMin_New { get; set; }
    public decimal? AdminMax_Old { get; set; }
    public decimal? AdminMax_New { get; set; }
    [MaxLength(30)]
    public string? ShortName_Old { get; set; }
    [MaxLength(30)]
    public string? ShortName_New { get; set; }
    public decimal? TaxRate_Old { get; set; }
    public decimal? TaxRate_New { get; set; }
    public decimal? InsMin_Old { get; set; }
    public decimal? InsMin_New { get; set; }
    public decimal? InsMax_Old { get; set; }
    public decimal? InsMax_New { get; set; }
    [MaxLength(20)]
    public string? UserId_Old { get; set; }
    [MaxLength(20)]
    public string? UserId_New { get; set; }
    [MaxLength(50)]
    public string? UserName_Old { get; set; }
    [MaxLength(50)]
    public string? UserName_New { get; set; }
    public DateTime SystemDate { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
