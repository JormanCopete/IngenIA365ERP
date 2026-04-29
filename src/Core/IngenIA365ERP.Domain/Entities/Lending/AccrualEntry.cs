using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_AccrualEntries] (cop_caunov).</summary>
public class AccrualEntry : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int AccrualPeriod { get; set; }
    [MaxLength(2)]
    public string EntryType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Reason { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    public int Installments { get; set; }
    [MaxLength(2)]
    public string AffectsCapital { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AffectsInterest { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AffectsExtras { get; set; } = string.Empty;
    [MaxLength(2)]
    public string FixedInstallments { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Authorization { get; set; } = string.Empty;
    public DateOnly EntryDate { get; set; }
    public string? Remarks { get; set; }
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;
    public DateOnly SystemDate { get; set; }
    [MaxLength(2)]
    public string? Status { get; set; }
    public DateOnly ExpirationDate { get; set; }
    [MaxLength(50)]
    public string UserFullName { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AppliesExtras { get; set; } = string.Empty;
    [MaxLength(2)]
    public string? AppliesToSavings { get; set; }
    [MaxLength(2)]
    public string? AppliesToServices { get; set; }
    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
