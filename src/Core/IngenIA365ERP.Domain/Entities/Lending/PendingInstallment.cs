using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PendingInstallments] (cop_cuopen).</summary>
public class PendingInstallment : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int AccrualPeriod { get; set; }
    public int AccountingPeriod { get; set; }
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Description { get; set; } = string.Empty;
    public int Cycle { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal AccruedCapital { get; set; }
    public decimal AccruedExtra { get; set; }
    public decimal PaidInterest { get; set; }
    public decimal PaidCapital { get; set; }
    public decimal PaidExtra { get; set; }
    public decimal BalanceInterest { get; set; }
    public decimal BalanceCapital { get; set; }
    public decimal BalanceExtra { get; set; }
    public decimal CreditBalance { get; set; }
    public int ExtraNumber { get; set; }
    public decimal ObligationInstallment { get; set; }
    public decimal TotalInstallment { get; set; }
    [MaxLength(2)]
    public string ExtraPaymentForm { get; set; } = string.Empty;
    public decimal DefaultInterest { get; set; }
    public decimal DefaultInterestBalance { get; set; }
    public DateOnly? TransactionDate { get; set; }
    [MaxLength(2)]
    public string DeductionType { get; set; } = string.Empty;
    public decimal PriorCapitalBalance { get; set; }
    public decimal PriorInterestBalance { get; set; }
    public decimal PriorExtraBalance { get; set; }
    public int DaysToMaturity { get; set; }
    [MaxLength(2)]
    public string IsAdvancePayment { get; set; } = string.Empty;
    public decimal DefaultInterestAccrued { get; set; }
    public decimal DefaultInterestPaid { get; set; }
    public DateOnly? LastDefaultDate { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal AdvanceCapital { get; set; }
    public decimal AdvanceInterest { get; set; }
    public decimal AdvanceExtra { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal DaysOverdue { get; set; }
    public decimal InterestRate { get; set; }
    [MaxLength(2)]
    public string EntryType { get; set; } = string.Empty;
    public decimal PriorInsuranceBalance { get; set; }
    public decimal PriorAdminBalance { get; set; }
    public decimal PriorOtherBalance { get; set; }
    public decimal AccruedInsurance { get; set; }
    public decimal AccruedAdmin { get; set; }
    public decimal AccruedOther { get; set; }
    public decimal PaidInsurance { get; set; }
    public decimal PaidAdmin { get; set; }
    public decimal PaidOther { get; set; }
    public decimal BalanceInsurance { get; set; }
    public decimal BalanceAdmin { get; set; }
    public decimal BalanceOther { get; set; }
    public DateOnly? ProcessDate { get; set; }
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;
    public DateOnly SystemDate { get; set; }
    [MaxLength(2)]
    public string DeductionClass { get; set; } = string.Empty;
    [MaxLength(3)]
    public string GuaranteeClass { get; set; } = string.Empty;
    [MaxLength(2)]
    public string EntryReason { get; set; } = string.Empty;
    public decimal DtfRate { get; set; }
    public decimal SpreadPoints { get; set; }
    [MaxLength(2)]
    public string AccruedAll { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AccruedNoExtra { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AccruedOnlyExtra { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
