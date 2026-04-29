using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_Amortizations] (cnt_amortiza).</summary>
public class Amortization : AuditableEntityLong
{
    public int PeriodYear { get; set; }
    public int AccountId { get; set; }
    public int BranchId { get; set; }
    public int CostCenterId { get; set; }
    public int? PersonId { get; set; }
    public string? DocumentCode { get; set; }
    public int? CrossAccountId { get; set; }
    public string? CrossCostCenterCode { get; set; }
    public string? CrossPersonTaxId { get; set; }
    public decimal CrossInitialBalance { get; set; }
    public int? MovementAccountId { get; set; }
    public string? MovementCostCenterCode { get; set; }
    public string? MovementPersonTaxId { get; set; }
    public decimal MovementInitialBalance { get; set; }
    public DateOnly? AmortizationDate { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal MonthlyAmount { get; set; }
    public int? TermMonths { get; set; }
    public decimal RemainingBalance { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? LastPeriod { get; set; }
    public decimal Rate { get; set; }
    public int Status { get; set; }

    // Navigation
    public ChartOfAccount Account { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public CostCenter CostCenter { get; set; } = null!;
}
