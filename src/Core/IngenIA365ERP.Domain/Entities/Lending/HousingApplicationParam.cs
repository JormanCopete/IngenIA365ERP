using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_HousingApplicationParams] (cop_solparviv).</summary>
public class HousingApplicationParam : AuditableEntity
{
    public int ApplicationNumber { get; set; }
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
}
