using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PreviousInstallments].</summary>
public class PreviousInstallment : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int AccrualPeriod { get; set; }
    public int AccountingPeriod { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal ExtraAmount { get; set; }
    public decimal AdvanceCapital { get; set; }
    public decimal AdvanceInterest { get; set; }
    public decimal AdvanceExtra { get; set; }
    public decimal AdvanceInsurance { get; set; }
    public decimal AdvanceAdmin { get; set; }
    public decimal AdvanceOther { get; set; }
    public DateOnly AdvanceDate { get; set; }
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    public long TransactionSequence { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
