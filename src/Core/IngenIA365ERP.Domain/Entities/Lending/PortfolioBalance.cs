using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PortfolioBalances] (cop_salmaecar).</summary>
public class PortfolioBalance : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int Period { get; set; }
    public decimal Balance { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public int OpenInstallments { get; set; }
    [MaxLength(3)]
    public string CifinStatus { get; set; } = string.Empty;
    [MaxLength(2)]
    public string InitialCategory { get; set; } = string.Empty;
    [MaxLength(2)]
    public string FinalCategory { get; set; } = string.Empty;
    public DateTime? InitialLastPaymentDate { get; set; }
    public DateTime? FinalLastPaymentDate { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal InterestRate { get; set; }
    [MaxLength(2)]
    public string? PaymentCycle { get; set; }
    [MaxLength(2)]
    public string? Periodicity { get; set; }
    [MaxLength(2)]
    public string? DeductionClass { get; set; }
    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
