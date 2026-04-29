using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_HousingParameters].</summary>
public class HousingParameter : AuditableEntity
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int HousingClass { get; set; }
    public int HousingType { get; set; }
    [MaxLength(2)]
    public string SocialInterest { get; set; } = string.Empty;
    [MaxLength(2)]
    public string HasSubsidy { get; set; } = string.Empty;
    public int NetworkEntity { get; set; }
    public long NetworkValue { get; set; }
    public int DisbursementType { get; set; }
    public int CurrencyType { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
