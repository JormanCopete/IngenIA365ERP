using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_Guarantees] (cop_garantia).</summary>
public class Guarantee : AuditableEntity
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    [MaxLength(25)]
    public string RegistrationNumber { get; set; } = string.Empty;
    [MaxLength(3)]
    public string GuaranteeType { get; set; } = string.Empty;
    public string GuaranteeDescription { get; set; } = string.Empty;
    public decimal CadastralAppraisal { get; set; }
    public decimal CommercialAppraisal { get; set; }
    [MaxLength(2)]
    public string HasInsurance { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PolicyNumber { get; set; } = string.Empty;
    public DateOnly? GuaranteeStartDate { get; set; }
    public DateOnly? GuaranteeCancelDate { get; set; }
    public DateOnly? MaturityDate { get; set; }
    [MaxLength(20)]
    public string InsurerIdentification { get; set; } = string.Empty;
    [MaxLength(50)]
    public string InsurerName { get; set; } = string.Empty;
    [MaxLength(2)]
    public string GuaranteeStatus { get; set; } = string.Empty;
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;
    public decimal? InsuredPercentage { get; set; }
    [MaxLength(20)]
    public string Guarantor1 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Guarantor2 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Guarantor3 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Guarantor4 { get; set; } = string.Empty;
    public int Term { get; set; }
    public decimal Balance { get; set; }
    public int CdatNumber { get; set; }
    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
