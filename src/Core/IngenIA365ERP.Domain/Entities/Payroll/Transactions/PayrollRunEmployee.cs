using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Transactions;

/// <summary>
/// Maps to [dbo].[PAY_PayrollRunEmployees]. La liquidación de un empleado dentro de
/// una corrida: días, tramos de salario, totales, banderas de bloqueo. Inmutable
/// (Principio XI).
/// </summary>
public class PayrollRunEmployee : AuditableEntity
{
    public int PayrollRunId { get; set; }
    public int EmployeeId { get; set; }
    public int PayrollPlanId { get; set; }
    public int DaysWorked { get; set; }

    /// <summary>[{ from, to, days, salary }] tal como se liquidó.</summary>
    public string SalaryTranchesJson { get; set; } = "[]";

    public EmployeeClass EmployeeClass { get; set; }

    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public decimal TotalProvisions { get; set; }
    public decimal NetPay { get; set; }

    public RunEmployeeFlag Flags { get; set; }

    /// <summary>Cambió respecto del borrador anterior del mismo período (FR-014).</summary>
    public bool ChangedFromPreviousRun { get; set; }

    public PayrollRun? Run { get; set; }
    public Employee? Employee { get; set; }
    public ICollection<PayrollRunLine> Lines { get; set; } = [];

    public bool HasBlockers => Flags != RunEmployeeFlag.None;
}
