using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_Budgets] (cnt_presupto).</summary>
public class Budget : AuditableEntity
{
    public int PeriodYear { get; set; }
    public int AccountId { get; set; }
    public int BranchId { get; set; }
    public int CostCenterId { get; set; }
    public decimal? JanBudget { get; set; }
    public decimal? FebBudget { get; set; }
    public decimal? MarBudget { get; set; }
    public decimal? AprBudget { get; set; }
    public decimal? MayBudget { get; set; }
    public decimal? JunBudget { get; set; }
    public decimal? JulBudget { get; set; }
    public decimal? AugBudget { get; set; }
    public decimal? SepBudget { get; set; }
    public decimal? OctBudget { get; set; }
    public decimal? NovBudget { get; set; }
    public decimal? DecBudget { get; set; }
    public decimal? TotalBudget { get; set; }

    // Navigation
    public ChartOfAccount Account { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public CostCenter CostCenter { get; set; } = null!;
}
