using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_PayPeriods] (nom_perpagos).</summary>
public class PayPeriod : AuditableEntity
{
    public int PlanId { get; set; }
    public int PayrollCompanyId { get; set; }

    [MaxLength(100)]
    public string? Description { get; set; }

    [MaxLength(40)]
    public string? PayDate { get; set; }

    [MaxLength(4)]
    public string? LiquidationCompanyId { get; set; }

    public int? CycleMonth { get; set; }
    public int? CycleHours { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? Periodicity { get; set; }
    public int? AdditionalConcept1 { get; set; }
    public int? AdditionalConcept2 { get; set; }
    public int? AdditionalConcept3 { get; set; }
    public int? AdditionalConcept4 { get; set; }

    [MaxLength(1)]
    public string? OnlyEntries { get; set; }

    [MaxLength(1)]
    public string? NoAutoSalaryLiq { get; set; }

    [MaxLength(1)]
    public string? NoAbsenceLiq { get; set; }

    [MaxLength(1)]
    public string? NoDirectDebitLiq { get; set; }

    public int Status { get; set; }

    [MaxLength(100)]
    public string StatusMessage { get; set; } = string.Empty;

    public int PeriodId { get; set; }

    [MaxLength(1)]
    public string AdvanceLiquidation { get; set; } = string.Empty;

    [MaxLength(1)]
    public string AdvanceCrossing { get; set; } = string.Empty;
}
