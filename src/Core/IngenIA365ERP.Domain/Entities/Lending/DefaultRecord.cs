using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_DefaultRecords] (cop_copmora).</summary>
public class DefaultRecord : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int AccrualPeriod { get; set; }
    public int AccountingPeriod { get; set; }
    public int DaysOverdue { get; set; }
    public decimal CapitalBalance { get; set; }
    public decimal ExtraBalance { get; set; }
    public decimal InterestBalance { get; set; }
    public decimal DefaultBalance { get; set; }
    public decimal InsuranceBalance { get; set; }
    public decimal AdminBalance { get; set; }
    public decimal OtherBalance { get; set; }
    public decimal PriorCapitalBalance { get; set; }
    public decimal PriorExtraBalance { get; set; }
    public decimal PriorInterestBalance { get; set; }
    public decimal PriorDefaultBalance { get; set; }
    public decimal PriorInsuranceBalance { get; set; }
    public decimal PriorAdminBalance { get; set; }
    public decimal PriorOtherBalance { get; set; }
    public decimal AccruedCapital { get; set; }
    public decimal AccruedExtra { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal AccruedDefault { get; set; }
    public decimal AccruedInsurance { get; set; }
    public decimal AccruedAdmin { get; set; }
    public decimal AccruedOther { get; set; }
    public decimal PaidCapital { get; set; }
    public decimal PaidExtra { get; set; }
    public decimal PaidInterest { get; set; }
    public decimal PaidDefault { get; set; }
    public decimal PaidInsurance { get; set; }
    public decimal PaidAdmin { get; set; }
    public decimal PaidOther { get; set; }
    public DateOnly? LastLiquidationDate { get; set; }
    public int ExtraNumber { get; set; }
    public int CxcFlag { get; set; }
    [MaxLength(2)]
    public string IsManaged { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
