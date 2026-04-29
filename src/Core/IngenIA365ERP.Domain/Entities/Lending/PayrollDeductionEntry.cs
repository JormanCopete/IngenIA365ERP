using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PayrollDeductionEntries] (cop_nomnov).</summary>
public class PayrollDeductionEntry : AuditableEntityLong
{
    public int Period { get; set; }
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(2)]
    public string EntryType { get; set; } = string.Empty;
    [MaxLength(20)]
    public string ConceptCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string StartDate { get; set; } = string.Empty;
    [MaxLength(20)]
    public string EndDate { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AccumulatedAmount { get; set; }
    [MaxLength(5)]
    public string OrderNumber { get; set; } = string.Empty;
}
