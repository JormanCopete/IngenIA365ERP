using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PayrollDeductionPeriods] (cop_nompla).</summary>
public class PayrollDeductionPeriod : AuditableEntity
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
    [MaxLength(50)]
    public string Description { get; set; } = string.Empty;
    [MaxLength(2)]
    public string DeductionClass { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    [MaxLength(2)]
    public string PaymentCycle { get; set; } = string.Empty;
    public decimal ContributionAmount { get; set; }
    public decimal LoanAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal ExtraAmount { get; set; }
    public decimal DefaultAmount { get; set; }
    public decimal InsuranceAmount { get; set; }
    public decimal AdminAmount { get; set; }
    public decimal OtherAmount { get; set; }
    public int ArrearsFrom { get; set; }
    public int ArrearsTo { get; set; }
    [MaxLength(2)]
    public string ArrearsExtras { get; set; } = string.Empty;
}
