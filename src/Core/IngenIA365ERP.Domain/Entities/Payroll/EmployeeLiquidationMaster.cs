using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_EmployeeLiquidationMasters] (nom_maeliqemp).</summary>
public class EmployeeLiquidationMaster : AuditableEntityLong
{
    public int PlanId { get; set; }
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public decimal Salary { get; set; }
    public DateTime JoinDate { get; set; }
    public int ContractType { get; set; }
    public DateTime ContractEndDate { get; set; }

    [MaxLength(1)]
    public string SpecialRegime { get; set; } = string.Empty;

    public DateTime LiquidationDate { get; set; }
    public int TerminationCause { get; set; }
    public int? SeveranceUnpaidDays { get; set; }
    public int? BonusUnpaidDays { get; set; }
    public int? VacationUnpaidDays { get; set; }
    public decimal PreviousSeveranceAmount { get; set; }
    public decimal CurrentSeveranceAmount { get; set; }
    public DateTime LastVacationPayDate { get; set; }
    public DateTime LastBonusPayDate { get; set; }
    public decimal SeveranceBase { get; set; }
    public decimal BonusBase { get; set; }
    public decimal VacationBase { get; set; }
    public decimal IndemnityBase { get; set; }
    public decimal SeveranceDays { get; set; }
    public decimal BonusDays { get; set; }
    public decimal VacationDays { get; set; }
    public decimal IndemnityDays { get; set; }

    [MaxLength(1)]
    public string IsAccountingPosted { get; set; } = string.Empty;

    public DateTime SystemDate { get; set; }

    [MaxLength(14)]
    public string UserName { get; set; } = string.Empty;

    public DateTime LastSeverancePayDate { get; set; }
    public DateTime LastSeveranceInterestDate { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
