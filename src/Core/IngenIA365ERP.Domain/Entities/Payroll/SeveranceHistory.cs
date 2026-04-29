using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_SeveranceHistory] (nom_antcesantia).</summary>
public class SeveranceHistory : AuditableEntityLong
{
    public int PayrollCompanyId { get; set; }
    public int PlanId { get; set; }
    public int EmployeeId { get; set; }
    public DateTime? CauseStartDate { get; set; }
    public DateTime? CauseEndDate { get; set; }
    public DateTime? CutoffDate { get; set; }
    public decimal? SalaryBase { get; set; }
    public int? DaysWorked { get; set; }
    public decimal? AdvanceAmount { get; set; }
    public decimal? InterestAmount { get; set; }

    [MaxLength(30)]
    public string? Resolution { get; set; }

    public DateTime? ResolutionDate { get; set; }

    [MaxLength(100)]
    public string? Destination { get; set; }

    public int? AdvanceConceptId { get; set; }
    public int? InterestConceptId { get; set; }

    [MaxLength(14)]
    public string? UserName { get; set; }

    public DateTime? SystemDate { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
