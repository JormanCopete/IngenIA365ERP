using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_EmployerCompanies] (cop_empresa13 + nom_empresas).
/// Merged fields from both legacy tables.
/// </summary>
public class EmployerCompany : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    public int? LegacyPayrollId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? ShortName { get; set; }

    [MaxLength(20)]
    public string? TaxId { get; set; }

    [MaxLength(60)]
    public string? PayerName { get; set; }

    [MaxLength(120)]
    public string? Address { get; set; }

    [MaxLength(40)]
    public string? City { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(40)]
    public string? Fax { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    // Cutoff dates config
    public DateOnly? CutoffDate1 { get; set; }
    public short CutoffDay1 { get; set; }
    public DateOnly? CutoffDate2 { get; set; }
    public short CutoffDay2 { get; set; }
    public DateOnly? CutoffDate3 { get; set; }
    public short CutoffDay3 { get; set; }
    public short Term { get; set; }

    [MaxLength(10)]
    public string? PayrollConceptCode { get; set; }

    [MaxLength(30)]
    public string? SubmissionFormat { get; set; }

    public decimal DiscountPercentage { get; set; }

    [MaxLength(2)]
    public string? DiscountType { get; set; }

    public bool IsBlocked { get; set; }
    public decimal GroceryPercentage { get; set; }

    // Payroll config from nom_empresas
    public decimal ArpRate { get; set; }
    public int AccountType { get; set; }
    public int CompanyType { get; set; }

    [MaxLength(60)]
    public string? FundSourceAccount { get; set; }

    public decimal FixedProvisionAmount { get; set; }
    public decimal MinimumWageAmount { get; set; }

    // Payroll concept IDs
    public int? BasicSalaryConceptId { get; set; }
    public int? TransportConceptId { get; set; }
    public int? SeveranceConceptId { get; set; }
    public int? SeveranceInterestId { get; set; }
    public int? ServiceBonusId { get; set; }
    public int? VacationConceptId { get; set; }
    public int? IntegralSalaryId { get; set; }
    public int? SolidarityFundId { get; set; }
    public int? IndemnityConceptId { get; set; }
    public int? SocialContrib1Id { get; set; }
    public int? SocialContrib2Id { get; set; }
    public int? SocialContrib3Id { get; set; }
    public int? ArpAdminConceptId { get; set; }
    public int? PriorSeveranceId { get; set; }
    public int? WithholdingTaxId { get; set; }
    public int AdminLiquidationType { get; set; }
    public int? SenaConceptId { get; set; }
    public int? IcbfConceptId { get; set; }
    public int? NightSurchargeId { get; set; }
    public int? VacationAbsenceId { get; set; }
    public int? ServiceBonus1Id { get; set; }
    public int? ServiceBonus2Id { get; set; }
    public int? ServiceBonus3Id { get; set; }
    public int? ConsolidatedVacId { get; set; }
    public int? TransportDeductionId { get; set; }
    public decimal MaxDeductionPct { get; set; }
    public int IncludeProvision { get; set; }
    public bool EmitsInvoice { get; set; }
    public int? AnnualCompId { get; set; }
    public int? SemiannualCompId { get; set; }
    public int? DiscountCompId { get; set; }
    public int ThirdPartyTransfer { get; set; }

    [MaxLength(10)]
    public string? AccountingVoucherId { get; set; }

    [MaxLength(20)]
    public string? OffsettingAccount { get; set; }

    public int AccountingUpdateType { get; set; }
    public decimal BaseSalary { get; set; }

    // Solidarity fund brackets
    public decimal SolidarityBracket1 { get; set; }
    public decimal SolidarityBracket2 { get; set; }
    public decimal SolidarityBracket3 { get; set; }
    public decimal SolidarityBracket4 { get; set; }
    public decimal SolidarityBracket5 { get; set; }
    public decimal SolidarityRate1 { get; set; }
    public decimal SolidarityRate2 { get; set; }
    public decimal SolidarityRate3 { get; set; }
    public decimal SolidarityRate4 { get; set; }
    public decimal SolidarityRate5 { get; set; }

    // Additional payroll config
    public int? SenaApprenticeId { get; set; }
    public int MaxDaysCap { get; set; }
    public int DisabilityCxcConcept { get; set; }
    public int ProbationDays { get; set; }
    public int ConsolidatedVacId2 { get; set; }
    public decimal DisabilityFactor { get; set; }
    public decimal UvtValue { get; set; }
    public decimal IncomePercentage { get; set; }
    public decimal DeductionPercentage { get; set; }
}
