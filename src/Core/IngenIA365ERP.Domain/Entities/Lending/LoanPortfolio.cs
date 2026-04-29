using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_LoanPortfolios] (cop_maecar).</summary>
public class LoanPortfolio : AuditableEntity
{
    public int PersonId { get; set; }
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    [MaxLength(20)]
    public string IdentificationNumber { get; set; } = string.Empty;
    public DateOnly ApplicationDate { get; set; }
    public DateOnly? ApprovalDate { get; set; }
    public DateOnly DisbursementDate { get; set; }
    public DateOnly DiscountStartDate { get; set; }
    public DateOnly? LastAccrualDate { get; set; }
    public DateOnly? LastPaymentDate { get; set; }
    public DateOnly? LastDefaultDate { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public DateOnly? ClosingDate { get; set; }
    public int TermMonths { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal InterestRate { get; set; }
    [MaxLength(2)]
    public string PaymentCycle { get; set; } = string.Empty;
    [MaxLength(2)]
    public string PaymentPeriodicity { get; set; } = string.Empty;
    [MaxLength(2)]
    public string InstallmentType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string InterestType { get; set; } = string.Empty;
    [MaxLength(5)]
    public string GuaranteeType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string DeductionType { get; set; } = string.Empty;
    public int PaidInstallments { get; set; }
    public decimal AdminFeeRate { get; set; }
    public decimal InsuranceRate { get; set; }
    public decimal CapitalBalanceCurrent { get; set; }
    public decimal CapitalBalanceMonthly { get; set; }
    public decimal CapitalBalancePayment { get; set; }
    public decimal InterestBalanceCurrent { get; set; }
    public decimal InterestBalanceMonthly { get; set; }
    public decimal InterestBalancePayment { get; set; }
    public decimal DefaultBalanceCurrent { get; set; }
    public decimal DefaultBalanceMonthly { get; set; }
    public decimal DefaultBalancePayment { get; set; }
    public decimal AdminBalanceCurrent { get; set; }
    public decimal AdminBalanceMonthly { get; set; }
    public decimal AdminBalancePayment { get; set; }
    public decimal InsuranceBalanceCurrent { get; set; }
    public decimal InsuranceBalanceMonthly { get; set; }
    public decimal InsuranceBalancePayment { get; set; }
    [MaxLength(5)]
    public string DocumentType { get; set; } = string.Empty;
    public int DocumentNumber { get; set; }
    public decimal AdditionalCharges { get; set; }
    public decimal ExtraPaymentAmount { get; set; }
    public decimal CapitalAccrued { get; set; }
    public decimal InterestAccrued { get; set; }
    public decimal InsuranceAccrued { get; set; }
    public decimal AdminAccrued { get; set; }
    public decimal DefaultInterest { get; set; }
    public int DaysOverdue { get; set; }
    [MaxLength(2)]
    public string InitialGracePeriod { get; set; } = string.Empty;
    public DateOnly? GracePeriodStartDate { get; set; }
    [MaxLength(2)]
    public string GracePeriodInstallment { get; set; } = string.Empty;
    public int GracePeriodDays { get; set; }
    [MaxLength(20)]
    public string Codeudor1 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Codeudor2 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Codeudor3 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Codeudor4 { get; set; } = string.Empty;
    public decimal GuaranteeValue { get; set; }
    public decimal ContributionsAmount { get; set; }
    [MaxLength(5)]
    public string BranchId { get; set; } = string.Empty;
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Category { get; set; } = string.Empty;
    public decimal ExtraPercentage { get; set; }
    public decimal PaidInstallmentsAgency { get; set; }
    public decimal PendingInstallmentCount { get; set; }
    [MaxLength(10)]
    public string SearchNumber { get; set; } = string.Empty;
    public decimal ProvisionRate { get; set; }
    public decimal ProvisionAmount { get; set; }
    public decimal OrderInterest { get; set; }
    public decimal CxcInterest { get; set; }
    public decimal LocalClearing { get; set; }
    public decimal OtherClearing { get; set; }
    [MaxLength(2)]
    public string LegalCollection { get; set; } = string.Empty;
    [MaxLength(5)]
    public string LawyerCode { get; set; } = string.Empty;
    public decimal AdminInstallment { get; set; }
    public decimal InsuranceInstallment { get; set; }
    public decimal CapitalInstallment { get; set; }
    public decimal InterestInstallment { get; set; }
    public decimal OtherInstallment { get; set; }
    public int ApplicationNumber { get; set; }
    [MaxLength(2)]
    public string ExtraInMonth { get; set; } = string.Empty;
    [MaxLength(2)]
    public string ExtraInAdvance { get; set; } = string.Empty;
    [MaxLength(2)]
    public string FirstPaymentFlag { get; set; } = string.Empty;
    [MaxLength(2)]
    public string SecondPaymentType { get; set; } = string.Empty;
    public decimal InterestInstallmentAmt { get; set; }
    public decimal LessAmount { get; set; }
    [MaxLength(20)]
    public string CardNumber { get; set; } = string.Empty;
    public decimal CapitalAppliedPayroll { get; set; }
    public decimal InterestAppliedPayroll { get; set; }
    public decimal DefaultAppliedPayroll { get; set; }
    public decimal InsuranceAppliedPayroll { get; set; }
    public decimal AdminAppliedPayroll { get; set; }
    [MaxLength(8)]
    public string PayrollCode { get; set; } = string.Empty;
    [MaxLength(3)]
    public string InsuranceForm { get; set; } = string.Empty;
    [MaxLength(2)]
    public string TotalPriorInterest { get; set; } = string.Empty;
    [MaxLength(5)]
    public string DiscountCompany { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IncludeAutoDebit { get; set; } = string.Empty;
    public DateOnly? CifinStartDate { get; set; }
    public DateOnly? CifinEndDate { get; set; }
    public int CifinDaysOverdue { get; set; }
    public decimal CifinOverdueBalance { get; set; }
    public DateOnly? RestructureDate { get; set; }
    [MaxLength(2)]
    public string RestructureCategory { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IsRestructured { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AuthCreditBureau { get; set; } = string.Empty;
    public int CifinOverdueInstallments { get; set; }
    [MaxLength(2)]
    public string IsWrittenOff { get; set; } = string.Empty;
    [MaxLength(15)]
    public string TransactionId { get; set; } = string.Empty;
    public decimal DtfRate { get; set; }
    public decimal SpreadPoints { get; set; }
    [MaxLength(2)]
    public string HasLawsuit { get; set; } = string.Empty;
    public decimal WriteOffCapital { get; set; }
    public decimal WriteOffInterest { get; set; }
    public DateTime? WriteOffDate { get; set; }
    [MaxLength(30)]
    public string WriteOffMinutes { get; set; } = string.Empty;
    [MaxLength(250)]
    public string LawyerNotes { get; set; } = string.Empty;
    public DateTime? WriteOffApprovalDate { get; set; }
    [MaxLength(2)]
    public string TransferConcept { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IsFopep { get; set; } = string.Empty;
    public DateTime? ProposedInterestDate { get; set; }
    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }
    public int? LegacyLinCred { get; set; }
    public long? LegacyNumero { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public CreditLineParameter? CreditLine { get; set; }
    public ICollection<LendingTransaction> Transactions { get; set; } = [];
    public ICollection<PendingInstallment> PendingInstallments { get; set; } = [];
    public ICollection<ExtraPayment> ExtraPayments { get; set; } = [];
    public ICollection<Guarantee> Guarantees { get; set; } = [];
    public ICollection<DefaultRecord> DefaultRecords { get; set; } = [];
}
