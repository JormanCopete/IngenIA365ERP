using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PortfolioAccounts].</summary>
public class PortfolioAccount : AuditableEntity
{
    [MaxLength(15)]
    public string AccountCode { get; set; } = string.Empty;
    [MaxLength(3)]
    public string RecordClass { get; set; } = string.Empty;
    public int Category { get; set; }
    public int GuaranteeType { get; set; }
    public int DeductionClass { get; set; }
    [MaxLength(2)]
    public string RiskLevel { get; set; } = string.Empty;
}
