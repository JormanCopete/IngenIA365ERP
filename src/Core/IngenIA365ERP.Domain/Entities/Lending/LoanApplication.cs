using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_LoanApplications] (cop_solcre).</summary>
public class LoanApplication : AuditableEntityLong
{
    public int ApplicationNumber { get; set; }
    public DateOnly ApplicationDate { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal InterestRate { get; set; }
    public int Term { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal MonthlyPayments { get; set; }
    public decimal OverdueBalance { get; set; }
    public decimal AvailableCredit { get; set; }
    public DateOnly? CoopEntryDate { get; set; }
    [MaxLength(50)]
    public string EmployerName { get; set; } = string.Empty;
    [MaxLength(60)]
    public string Position { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public decimal OtherIncome { get; set; }
    public DateOnly? EmployerEntryDate { get; set; }
    public decimal MonthlyDeductions { get; set; }
    public decimal MonthlyFixedExpenses { get; set; }
    public decimal AvailableMonthly { get; set; }
    [MaxLength(2)]
    public string ContractType { get; set; } = string.Empty;
    [MaxLength(3)]
    public string GuaranteeType { get; set; } = string.Empty;
    public string? GuaranteeDescription { get; set; }
    public decimal CommercialAppraisal { get; set; }
    public decimal CadastralAppraisal { get; set; }
    [MaxLength(2)]
    public string IsInsured { get; set; } = string.Empty;
    public decimal InsurancePercentage { get; set; }
    public DateOnly? InsuranceExpiryDate { get; set; }
    [MaxLength(20)]
    public string Codeudor1 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Codeudor2 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Codeudor3 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Codeudor4 { get; set; } = string.Empty;
    public decimal ApprovedAmount { get; set; }
    public DateOnly? ApprovalDate { get; set; }
    [MaxLength(20)]
    public string MinutesNumber { get; set; } = string.Empty;
    public DateOnly? MinutesDate { get; set; }
    public DateOnly? ScheduledDate { get; set; }
    public decimal AdditionalContribution { get; set; }
    public decimal DebtConsolidation { get; set; }
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? UserId { get; set; }
    public DateOnly RecordDate { get; set; }
    [MaxLength(2)]
    public string SpouseWorks { get; set; } = string.Empty;
    [MaxLength(50)]
    public string SpouseName { get; set; } = string.Empty;
    [MaxLength(50)]
    public string SpouseEmployer { get; set; } = string.Empty;
    public decimal SpouseSalary { get; set; }
    [MaxLength(20)]
    public string SpousePhone { get; set; } = string.Empty;
    [MaxLength(50)]
    public string SpouseEmployerAddress { get; set; } = string.Empty;
    [MaxLength(25)]
    public string SpouseEmployerCity { get; set; } = string.Empty;
    public int SpouseDependents { get; set; }
    public string? Remarks { get; set; }
    [MaxLength(25)]
    public string VehicleDescription { get; set; } = string.Empty;
    [MaxLength(2)]
    public string HasVehicle { get; set; } = string.Empty;
    [MaxLength(2)]
    public string OwnsHouse { get; set; } = string.Empty;
    public DateOnly? DisbursementDate { get; set; }
    [MaxLength(20)]
    public string IdentificationNumber { get; set; } = string.Empty;
    [MaxLength(2)]
    public string PaymentCycle { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    [MaxLength(2)]
    public string InstallmentType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string InterestType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string DeductionType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string ClosingInterestType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string CapitalizationType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AdminType { get; set; } = string.Empty;
    [MaxLength(3)]
    public string? InsuranceType { get; set; }
    [MaxLength(2)]
    public string OtherType { get; set; } = string.Empty;
    [MaxLength(3)]
    public string AdminForm { get; set; } = string.Empty;
    public int AdminConceptCode { get; set; }
    public int InsuranceConceptCode { get; set; }
    public int OtherConceptCode { get; set; }
    public decimal AdminRate { get; set; }
    public decimal InsuranceRate { get; set; }
    public decimal ConceptRate { get; set; }
    public decimal OtherRate { get; set; }
    public decimal ExtraPaymentAmount { get; set; }
    public decimal ContributionsAmount { get; set; }
    [MaxLength(5)]
    public string BranchId { get; set; } = string.Empty;
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    public decimal ExtraPercentage { get; set; }
    public decimal AdminInstallment { get; set; }
    public decimal InsuranceInstallment { get; set; }
    public decimal CapitalInstallment { get; set; }
    public decimal InterestInstallment { get; set; }
    public decimal OtherInstallment { get; set; }
    [MaxLength(2)]
    public string GracePeriodFlag { get; set; } = string.Empty;
    public int GracePeriodStart { get; set; }
    public DateOnly? GracePeriodStartDate { get; set; }
    [MaxLength(2)]
    public string GracePeriodInstallment { get; set; } = string.Empty;
    public int GracePeriodDays { get; set; }
    public DateOnly? GracePeriodEndDate { get; set; }
    public int GracePeriodCycle { get; set; }
    [MaxLength(2)]
    public string ExtraInMonth { get; set; } = string.Empty;
    [MaxLength(2)]
    public string ExtraInAdvance { get; set; } = string.Empty;
    [MaxLength(2)]
    public string FirstPaymentFlag { get; set; } = string.Empty;
    [MaxLength(2)]
    public string SecondPaymentType { get; set; } = string.Empty;
    public int PromissoryNumber { get; set; }
    public int PayrollDeductionNumber { get; set; }
    public int CdatNumber { get; set; }
    public decimal VariableIncome { get; set; }
    public decimal RentalIncome { get; set; }
    public decimal ThirdPartyDebts { get; set; }
    [MaxLength(2)]
    public string AuthorizedFlag { get; set; } = string.Empty;
    [MaxLength(5)]
    public string DiscountCompany { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? InsurerIdentification { get; set; }
    [MaxLength(50)]
    public string? InsurerName { get; set; }
    [MaxLength(20)]
    public string? PolicyNumber { get; set; }
    [MaxLength(25)]
    public string? RegistrationNumber { get; set; }
    public decimal PensionIncome { get; set; }
    public decimal PensionDeduction { get; set; }
    public decimal ParafiscalDeduction { get; set; }
    [MaxLength(2)]
    public string SentToPaymaster { get; set; } = string.Empty;
    public DateOnly? SentToPaymasterDate { get; set; }
    public DateOnly ReceivedFromPaymasterDate { get; set; }
    [MaxLength(20)]
    public string? AuthorizingUser { get; set; }
    public decimal SolicitedInterestRate { get; set; }
    public int SolicitedTerm { get; set; }
    [MaxLength(2)]
    public string SolicitedPeriodicity { get; set; } = string.Empty;
    [MaxLength(2)]
    public string SolicitedCycle { get; set; } = string.Empty;
    [MaxLength(2)]
    public string SolicitedDeductionType { get; set; } = string.Empty;
    [MaxLength(20)]
    public string EntryUserId { get; set; } = string.Empty;
    [MaxLength(20)]
    public string AuthorizingUserId { get; set; } = string.Empty;
    public decimal EmployerPayrollDeduction { get; set; }
    public decimal DisbursedAmount { get; set; }
    public decimal DtfRate { get; set; }
    public decimal SpreadPoints { get; set; }
    [MaxLength(3)]
    public string EntityType { get; set; } = string.Empty;
    public decimal SolicitedInstallment { get; set; }
    [MaxLength(2)]
    public string PaymentCapacityPct { get; set; } = string.Empty;
    [MaxLength(3)]
    public string PaymentCapacityDebtPickup { get; set; } = string.Empty;
    public decimal SpouseIncome { get; set; }
    public decimal PersonalExpenses { get; set; }
    public decimal AssetHousing { get; set; }
    public decimal AssetVehicle { get; set; }
    public decimal AssetOther { get; set; }
    public decimal AssetContributions { get; set; }
    public decimal AssetCashBank { get; set; }
    public decimal AssetReceivables { get; set; }
    public decimal AssetSavings { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal LiabilityDebts { get; set; }
    public decimal LiabilityOther { get; set; }
    public decimal LiabilityBankLoans { get; set; }
    public decimal LiabilityMortgage { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal Equity { get; set; }
    public decimal TotalLiabilitiesEquity { get; set; }
    public decimal PayrollCapacity { get; set; }
    public decimal PayrollPercentage { get; set; }
    public decimal PaymentCapacity { get; set; }
    public decimal CashPercentage { get; set; }
    public decimal PaymasterPercentage { get; set; }
    public decimal PayrollLabel { get; set; }
    public decimal CashLabel { get; set; }
    public decimal DiscoveredAmount { get; set; }
    public decimal IndebtednessLevel { get; set; }
    public decimal ContingencyLevel { get; set; }
    public decimal CapitalAtRisk { get; set; }
    public decimal DeductionCapacity { get; set; }
    public decimal DeductionPercentage { get; set; }
    [MaxLength(2)]
    public string? PaymasterDeductionType { get; set; }
    public decimal SolicitedDtfRate { get; set; }
    public decimal SolicitedSpreadPoints { get; set; }
    public int? LegacyNumero { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
