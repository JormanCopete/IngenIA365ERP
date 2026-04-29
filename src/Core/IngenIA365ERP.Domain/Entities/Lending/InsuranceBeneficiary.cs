using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_InsuranceBeneficiaries].</summary>
public class InsuranceBeneficiary : AuditableEntity
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    [MaxLength(15)]
    public string BeneficiaryId { get; set; } = string.Empty;
    [MaxLength(5)]
    public string RelationshipCode { get; set; } = string.Empty;
    [MaxLength(3)]
    public string DocumentType { get; set; } = string.Empty;
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public decimal InsurancePercentage { get; set; }
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;
    [MaxLength(60)]
    public string Address { get; set; } = string.Empty;

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
