using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CreditLineParameters] (cop_concar12).</summary>
public class CreditLineParameter : AuditableEntity
{
    public int CreditLineId { get; set; }
    [MaxLength(40)]
    public string Description { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AllowExtension { get; set; } = string.Empty;
    [MaxLength(2)]
    public string DefaultInterestFlag { get; set; } = string.Empty;
    public decimal InterestRate { get; set; }
    [MaxLength(2)]
    public string InterestType { get; set; } = string.Empty;
    public int MaxTerm { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal ExtraRate { get; set; }
    [MaxLength(2)]
    public string FinancialInterest { get; set; } = string.Empty;
    [MaxLength(2)]
    public string GuaranteeClass { get; set; } = string.Empty;
    public decimal MaxAmount { get; set; }
    [MaxLength(2)]
    public string AffectsFlag { get; set; } = string.Empty;
    public decimal AccrualRate { get; set; }
    [MaxLength(2)]
    public string CapitalForm { get; set; } = string.Empty;
    public decimal AdminRate { get; set; }
    [MaxLength(2)]
    public string InstallmentType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string DebitCreditFlag { get; set; } = string.Empty;
    [MaxLength(2)]
    public string SumGuarantee { get; set; } = string.Empty;
    [MaxLength(2)]
    public string TotalPriorInterest { get; set; } = string.Empty;
    public int MonthsInAdvance { get; set; }
    [MaxLength(2)]
    public string AccountStatement { get; set; } = string.Empty;
    [MaxLength(2)]
    public string ClosingInterest { get; set; } = string.Empty;
    [MaxLength(2)]
    public string PrimaryVoucher { get; set; } = string.Empty;
    [MaxLength(2)]
    public string HousingLoan { get; set; } = string.Empty;
    public decimal InsuranceRate { get; set; }
    [MaxLength(2)]
    public string BalanceConsult { get; set; } = string.Empty;
    public decimal MinContribution { get; set; }
    [MaxLength(3)]
    public string PendingContribution { get; set; } = string.Empty;
    [MaxLength(15)]
    public string AccountCode { get; set; } = string.Empty;
    [MaxLength(2)]
    public string GracePeriodFlag { get; set; } = string.Empty;
    public int GracePeriodMonths { get; set; }
    [MaxLength(2)]
    public string ShowBalance { get; set; } = string.Empty;
    [MaxLength(10)]
    public string SecondaryCostCenter { get; set; } = string.Empty;
    public decimal AdminValueMin { get; set; }
    public decimal AdminValueMax { get; set; }
    [MaxLength(2)]
    public string IncomeTaxFlag { get; set; } = string.Empty;
    [MaxLength(3)]
    public string AdminForm { get; set; } = string.Empty;
    [MaxLength(3)]
    public string Priority { get; set; } = string.Empty;
    [MaxLength(2)]
    public string SavingsCode { get; set; } = string.Empty;
    [MaxLength(2)]
    public string GuarantorRequired { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AdminClass { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CategoryA { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CategoryB { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CategoryC { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CategoryD { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CategoryE { get; set; } = string.Empty;
    [MaxLength(30)]
    public string ShortName { get; set; } = string.Empty;
    [MaxLength(5)]
    public string ConsecutiveCode { get; set; } = string.Empty;
    public decimal CdatInterestRate { get; set; }
    public int AdminConceptCode { get; set; }
    public int InsuranceConceptCode { get; set; }
    public int InterestConceptCode { get; set; }
    [MaxLength(2)]
    public string EquivalentRate { get; set; } = string.Empty;
    [MaxLength(15)]
    public string AccountInterestIncome { get; set; } = string.Empty;
    [MaxLength(15)]
    public string AccountInterestCxC { get; set; } = string.Empty;
    [MaxLength(15)]
    public string AccountInterestDefault { get; set; } = string.Empty;
    [MaxLength(15)]
    public string AccountInterestAdvance { get; set; } = string.Empty;
    [MaxLength(15)]
    public string AccountInterestOrderDebit { get; set; } = string.Empty;
    [MaxLength(15)]
    public string AccountInterestOrderCredit { get; set; } = string.Empty;
    [MaxLength(3)]
    public string FogaContribution { get; set; } = string.Empty;
    [MaxLength(2)]
    public string FogaClass { get; set; } = string.Empty;
    [MaxLength(5)]
    public string PayrollCompanyCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string PayrollConceptCode { get; set; } = string.Empty;
    public int ColumnCount { get; set; }
    [MaxLength(20)]
    public string ColumnTitle { get; set; } = string.Empty;
    public int AdditionalChargesConcept { get; set; }
    public decimal TaxRate { get; set; }
    [MaxLength(2)]
    public string InternetEnabled { get; set; } = string.Empty;
    [MaxLength(2)]
    public string MaturityBehavior { get; set; } = string.Empty;
    public decimal InsuranceValueMin { get; set; }
    public decimal InsuranceValueMax { get; set; }
    public int ProjectionItem { get; set; }
    public int FormatId { get; set; }
    public int ConceptId { get; set; }
    public int SourceId { get; set; }
    [MaxLength(5)]
    public string CapitalizationConcept { get; set; } = string.Empty;
    public int SuperintendencyEquivalent { get; set; }
    [MaxLength(2)]
    public string ExportCifin { get; set; } = string.Empty;
    [MaxLength(2)]
    public string LiquidateDefaultDays { get; set; } = string.Empty;
    [MaxLength(2)]
    public string? ModifyInstallmentType { get; set; }
    public int InterestTypeCode { get; set; }
    public decimal DtfRate { get; set; }
    [MaxLength(2)]
    public string BlockProjectionDate { get; set; } = string.Empty;
    public int BlockOverdueAssociate { get; set; }
    [MaxLength(2)]
    public string ExtraPaymentApply { get; set; } = string.Empty;
    [MaxLength(15)]
    public string VatAccount { get; set; } = string.Empty;
    [MaxLength(2)]
    public string CalculateVat { get; set; } = string.Empty;
    public int CreditLimitType { get; set; }
    [MaxLength(3)]
    public string CreditLimitCalcMethod { get; set; } = string.Empty;
    public decimal CreditLimitValue { get; set; }
    public decimal CreditLimitAvailable { get; set; }
    [MaxLength(2)]
    public string CapitalAtRisk { get; set; } = string.Empty;
    [MaxLength(2)]
    public string InvoiceEnabled { get; set; } = string.Empty;
    public decimal VatPercentage { get; set; }
    public int VatLineId { get; set; }
    [MaxLength(2)]
    public string IsVatLine { get; set; } = string.Empty;
    [MaxLength(3)]
    public string InvoiceGroup { get; set; } = string.Empty;
    [MaxLength(3)]
    public string CalculationBase { get; set; } = string.Empty;
    public decimal BasePercentage { get; set; }
    public int DiscountConceptId { get; set; }
    public int PeaceSalvoSeniority { get; set; }
    public int? LegacyLinCred { get; set; }

    // Navigation
    public ICollection<LoanPortfolio> LoanPortfolios { get; set; } = [];
}
