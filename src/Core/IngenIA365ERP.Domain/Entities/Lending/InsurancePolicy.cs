using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_InsurancePolicies].</summary>
public class InsurancePolicy : AuditableEntity
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public decimal Balance { get; set; }
    public decimal InsuranceAmount { get; set; }
    public DateOnly InsuranceDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public DateTime SystemDate { get; set; }
    [MaxLength(20)]
    public string? UserId { get; set; }
    [MaxLength(50)]
    public string UserFullName { get; set; } = string.Empty;
    public bool IsHistorical { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
