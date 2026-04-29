using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Companies] (sys_compania).
/// The company configuration table with ~140 legacy fields.
/// </summary>
public class Company : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    // === BASIC INFO ===
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? ShortName { get; set; }

    [MaxLength(20)]
    public string TaxId { get; set; } = string.Empty;

    [MaxLength(2)]
    public string? TaxIdCheckDigit { get; set; }

    [MaxLength(80)]
    public string? Address { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(40)]
    public string? City { get; set; }

    [MaxLength(40)]
    public string? Department { get; set; }

    [MaxLength(10)]
    public string? Activity { get; set; }

    [MaxLength(20)]
    public string? PersonCode { get; set; }

    // === DIAN / INVOICE RESOLUTION ===
    [MaxLength(6)]
    public string? DianCode { get; set; }

    [MaxLength(40)]
    public string? DianCodeDescription { get; set; }

    [MaxLength(40)]
    public string? DianResolutionNumber { get; set; }

    public DateOnly? DianResolutionDate { get; set; }

    public int DianInvoiceStart { get; set; }
    public int DianInvoiceEnd { get; set; }

    // === WAGE CONFIGURATION ===
    public decimal LegalMinimumWage { get; set; }
    public decimal CompanyMinimumWage { get; set; }
    public decimal MinimumWage { get; set; }

    // === PORTFOLIO / COLLECTION CONFIG ===
    public bool ChargesDefaultInterest { get; set; }
    public bool LatePaymentControl { get; set; }
    public bool PaymentControl { get; set; }

    [MaxLength(10)]
    public string? CollectionPeriod { get; set; }

    public short InitialDays { get; set; }
    public short FinalDays { get; set; }
    public decimal? DefaultRate { get; set; }
    public short GraceDays { get; set; }
    public decimal? UsuryRate { get; set; }
    public bool EffectiveRate { get; set; }

    [MaxLength(2)]
    public string? LiquidationBase { get; set; }

    [MaxLength(2)]
    public string? LiquidationType { get; set; }

    [MaxLength(2)]
    public string? DiscountClass { get; set; }

    [MaxLength(2)]
    public string? LiquidationClass { get; set; }

    [MaxLength(2)]
    public string? QuotaType { get; set; }

    public decimal Quota { get; set; }

    [MaxLength(2)]
    public string? ReportClass { get; set; }

    [MaxLength(40)]
    public string? FileName { get; set; }

    public long CreditSequenceNum { get; set; }
    public decimal CreditSequenceCtrl { get; set; }
    public int NumCodeudores { get; set; }

    // === DUE DATE RANGES ===
    public short DueRangeStart01 { get; set; }
    public short DueRangeEnd01 { get; set; }
    public short DueRangeStart02 { get; set; }
    public short DueRangeEnd02 { get; set; }
    public short DueRangeStart03 { get; set; }
    public short DueRangeEnd03 { get; set; }
    public short DueRangeStart04 { get; set; }
    public short DueRangeEnd04 { get; set; }
    public short DueRangeStart05 { get; set; }
    public short DueRangeEnd05 { get; set; }

    // === CONCEPT CODES (current) ===
    [MaxLength(10)]
    public string? ConceptCapital { get; set; }

    [MaxLength(10)]
    public string? ConceptInterest { get; set; }

    [MaxLength(10)]
    public string? ConceptAdmin { get; set; }

    [MaxLength(10)]
    public string? ConceptInsurance { get; set; }

    [MaxLength(10)]
    public string? ConceptContributions { get; set; }

    [MaxLength(10)]
    public string? ConceptSavings { get; set; }

    [MaxLength(10)]
    public string? ConceptAffiliation { get; set; }

    [MaxLength(10)]
    public string? ConceptExtra { get; set; }

    [MaxLength(10)]
    public string? ConceptImmovable { get; set; }

    [MaxLength(10)]
    public string? ConceptService { get; set; }

    [MaxLength(10)]
    public string? ConceptOther1 { get; set; }

    [MaxLength(10)]
    public string? ConceptOther2 { get; set; }

    [MaxLength(10)]
    public string? ConceptRevaluation { get; set; }

    [MaxLength(10)]
    public string? ConceptContribDisp { get; set; }

    [MaxLength(10)]
    public string? Concept4Mil { get; set; }

    [MaxLength(10)]
    public string? ConceptWithholdingLate { get; set; }

    [MaxLength(10)]
    public string? ConceptCdtInterest { get; set; }

    [MaxLength(10)]
    public string? ConceptWithholding { get; set; }

    [MaxLength(10)]
    public string? ConceptCdt { get; set; }

    [MaxLength(10)]
    public string? ConceptSurplus { get; set; }

    // === CONCEPT CODES (late/arrears) ===
    [MaxLength(10)]
    public string? ConceptCapitalLate { get; set; }

    [MaxLength(10)]
    public string? ConceptInterestLate { get; set; }

    [MaxLength(10)]
    public string? ConceptAdminLate { get; set; }

    [MaxLength(10)]
    public string? ConceptInsuranceLate { get; set; }

    [MaxLength(10)]
    public string? ConceptContribLate { get; set; }

    [MaxLength(10)]
    public string? ConceptSavingsLate { get; set; }

    [MaxLength(10)]
    public string? ConceptAffiliationLate { get; set; }

    // === PRIORITY ORDER ===
    [MaxLength(4)]
    public string? PriorityCapital { get; set; }

    [MaxLength(4)]
    public string? PriorityInterest { get; set; }

    [MaxLength(4)]
    public string? PriorityServices { get; set; }

    [MaxLength(4)]
    public string? PriorityDefault { get; set; }

    [MaxLength(4)]
    public string? PriorityAdmin { get; set; }

    [MaxLength(4)]
    public string? PriorityInsurance { get; set; }

    [MaxLength(4)]
    public string? PriorityContributions { get; set; }

    [MaxLength(4)]
    public string? PrioritySavings { get; set; }

    [MaxLength(4)]
    public string? PriorityAffiliation { get; set; }

    // === ANTI-MONEY LAUNDERING ===
    public decimal DailyAmlLimit { get; set; }
    public decimal MonthlyAmlLimit { get; set; }
    public long AmlSequence { get; set; }
    public bool CausesLegalCollection { get; set; }

    // === ACCOUNTING ACCOUNTS ===
    [MaxLength(20)]
    public string? AccountingAccount1 { get; set; }

    [MaxLength(20)]
    public string? AccountingAccount2 { get; set; }

    [MaxLength(20)]
    public string? AccountingAccount3 { get; set; }

    [MaxLength(20)]
    public string? AccountingAccount4 { get; set; }

    // === ADJUSTMENT ACCOUNTS ===
    [MaxLength(20)]
    public string? AdjInterest { get; set; }

    [MaxLength(20)]
    public string? AdjOrderAccounts { get; set; }

    [MaxLength(20)]
    public string? AdjPortfolioProvision { get; set; }

    [MaxLength(20)]
    public string? AdjInterestProvision { get; set; }

    [MaxLength(20)]
    public string? AdjProvisionPayroll { get; set; }

    [MaxLength(20)]
    public string? AdjProvisionCash { get; set; }

    [MaxLength(10)]
    public string? AdjCashContrib { get; set; }

    [MaxLength(10)]
    public string? AdjCashCapital { get; set; }

    [MaxLength(10)]
    public string? AdjCashInterest { get; set; }

    [MaxLength(10)]
    public string? AdjCashDefaultInt { get; set; }

    [MaxLength(10)]
    public string? AdjCashInsurance { get; set; }

    [MaxLength(10)]
    public string? AdjCashService { get; set; }

    [MaxLength(10)]
    public string? AdjCashSavings { get; set; }

    [MaxLength(10)]
    public string? AdjCashExtra { get; set; }

    [MaxLength(10)]
    public string? AdjCashAdmin { get; set; }

    // === SIGNATURES ===
    [MaxLength(80)]
    public string? RepresentativeName { get; set; }

    [MaxLength(80)]
    public string? AuditorName { get; set; }

    [MaxLength(30)]
    public string? AuditorLicense { get; set; }

    [MaxLength(80)]
    public string? AccountantName { get; set; }

    [MaxLength(30)]
    public string? AccountantLicense { get; set; }

    [MaxLength(80)]
    public string? CollectionManager { get; set; }

    [MaxLength(80)]
    public string? OtherSignerName { get; set; }

    [MaxLength(80)]
    public string? OtherSignerPosition { get; set; }

    // Signature images
    public byte[]? RepresentativeSign { get; set; }
    public byte[]? AccountantSign { get; set; }
    public byte[]? AuditorSign { get; set; }
    public byte[]? CollectionSign { get; set; }
    public byte[]? OtherSign { get; set; }

    // === AUTO-CREATE FLAGS ===
    public bool AutoCreateAccount { get; set; }
    public bool AutoCreateTaxId { get; set; }
    public bool AutoCreateBranch { get; set; }
    public bool AutoCreateCostCenter { get; set; }

    // === SEQUENCE COUNTERS ===
    public decimal DepositSequence { get; set; }
    public decimal CdtSequence { get; set; }
    public short SequenceControl { get; set; }
    public bool ControlDepositSeq { get; set; }

    // === ADVANCE PAYMENT CONFIG ===
    public short AdvanceConceptType { get; set; }

    [MaxLength(10)]
    public string? AdvanceVoucherCode { get; set; }

    [MaxLength(10)]
    public string? FavorVoucherCode { get; set; }

    public int DebtRecoveryOption { get; set; }

    // === ONLINE QUERY CONFIG ===
    public bool GenerateQueryCharge { get; set; }
    public decimal QueryChargeAmount { get; set; }

    [MaxLength(10)]
    public string? QueryVoucherCode { get; set; }

    // === PAYROLL & APPLICATION MODE ===
    [MaxLength(2)]
    public string? PayrollClass { get; set; }

    public bool SignatureModule { get; set; }
    public bool CalculateBalance { get; set; }

    [MaxLength(2)]
    public string? PayrollApplicationMode { get; set; }

    [MaxLength(2)]
    public string? CashApplicationMode { get; set; }

    public bool ChargesCodebtor { get; set; }

    // === OVERRIDE / RESTRICTION FLAGS ===
    public bool OverrideExtra { get; set; }
    public bool OverrideQuota { get; set; }
    public bool OverrideTerm { get; set; }
    public bool OverrideRate { get; set; }
    public bool UnifiedNetwork { get; set; }
    public bool RequiresStudy { get; set; }
    public bool DateRestrictionRC { get; set; }
    public bool AllowCreditQuotaMod { get; set; }
    public bool BankReconciliation { get; set; }
    public bool CalcContribProvision { get; set; }
    public bool ApplyDefaultSuspension { get; set; }
    public int DefaultSuspensionDays { get; set; }
    public bool DefaultForWithdrawn { get; set; }
    public bool InvoiceSequenceCtrl { get; set; }
    public bool AccrualParam { get; set; }
    public bool DisableCdtRate { get; set; }

    // === EMAIL CONFIGURATION ===
    [MaxLength(100)]
    public string? SmtpServer { get; set; }

    [MaxLength(120)]
    public string? SmtpSenderEmail { get; set; }

    [MaxLength(100)]
    public string? SmtpPassword { get; set; }

    public int? SmtpPort { get; set; }
    public bool SmtpEnableSsl { get; set; }

    [MaxLength(200)]
    public string? WebServiceUrl { get; set; }

    // === PROMISSORY NOTE CONFIG ===
    public decimal PromissoryNumber { get; set; }

    [MaxLength(2)]
    public string? PromissoryFormat { get; set; }

    public bool PromissoryNotes { get; set; }

    // === LEGAL ENTITY ===
    [MaxLength(20)]
    public string? LegalEntityNumber { get; set; }

    public DateOnly? LegalEntityDate { get; set; }

    // === WEB SYNC ===
    public int? UploadAssociateWeb { get; set; }
    public int? UploadMovementWeb { get; set; }
    public bool DownloadWeb { get; set; }

    // === WITHHOLDING AUX ===
    public bool WithholdingAux { get; set; }
    public decimal? WithholdingAuxAmount { get; set; }
    public decimal? WithholdingAuxPct { get; set; }

    [MaxLength(20)]
    public string? WithholdingAuxAccount { get; set; }

    // === DATA PACKAGE / MISC ===
    public int DataPackageSize { get; set; }
    public int AgreementType { get; set; }

    [MaxLength(10)]
    public string? FeecCode { get; set; }

    [MaxLength(4)]
    public string? LicenseType { get; set; }

    public DateOnly? LicenseExpiryDate { get; set; }
    public DateOnly? RegistrationExpiryDate { get; set; }
    public int NoticeDays { get; set; }

    [MaxLength(4)]
    public string? ReportType { get; set; }

    // === LEGACY AUDIT ===
    [MaxLength(20)]
    public string? LegacyUser { get; set; }

    [MaxLength(80)]
    public string? LegacyUserName { get; set; }

    public DateTime? LegacySystemDate { get; set; }
}
