using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PayrollDeductionConcepts] (cop_nomconce).</summary>
public class PayrollDeductionConcept : AuditableEntity
{
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string BranchId { get; set; } = string.Empty;
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    [MaxLength(8)]
    public string PayrollConceptCode { get; set; } = string.Empty;
    [MaxLength(8)]
    public string InterestConceptCode { get; set; } = string.Empty;
    [MaxLength(8)]
    public string ExtraConceptCode { get; set; } = string.Empty;

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
