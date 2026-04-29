using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_InterestRates] (cop_claint).</summary>
public class InterestRate : AuditableEntity
{
    public int Period { get; set; }
    [MaxLength(5)]
    public string CreditLineCode { get; set; } = string.Empty;
    public decimal PortfolioBalance { get; set; }
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    public int? PortfolioClass { get; set; }
}
