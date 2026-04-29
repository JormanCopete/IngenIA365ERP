using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_Depreciations] (cnt_deprecia).</summary>
public class Depreciation : AuditableEntityLong
{
    public int PeriodYear { get; set; }
    public int AccountId { get; set; }
    public int BranchId { get; set; }
    public int CostCenterId { get; set; }
    public int? PersonId { get; set; }
    public int? CrossAccountId { get; set; }
    public string? CrossCostCenterCode { get; set; }
    public string? CrossPersonTaxId { get; set; }
    public decimal CrossInitialBalance { get; set; }
    public int? MovementAccountId { get; set; }
    public string? MovementCostCenterCode { get; set; }
    public string? MovementPersonTaxId { get; set; }
    public decimal MovementInitialBalance { get; set; }
    public decimal OriginalValue { get; set; }
    public decimal DepreciationRate { get; set; }
    public decimal MonthlyDepreciation { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetValue { get; set; }
    public int? UsefulLifeMonths { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? LastPeriod { get; set; }

    // Navigation
    public ChartOfAccount Account { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public CostCenter CostCenter { get; set; } = null!;
}
