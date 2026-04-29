using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_DeductionValues] (cop_valdesc).</summary>
public class DeductionValue : AuditableEntityLong
{
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string BranchId { get; set; } = string.Empty;
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    public int Period { get; set; }
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IsAdditional { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(8)]
    public string ConceptCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
