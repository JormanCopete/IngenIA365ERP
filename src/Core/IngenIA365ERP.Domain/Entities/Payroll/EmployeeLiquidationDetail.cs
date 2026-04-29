using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_EmployeeLiquidationDetails] (nom_detliqemp).</summary>
public class EmployeeLiquidationDetail : AuditableEntityLong
{
    public int PlanId { get; set; }
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public decimal ConceptId { get; set; }
    public decimal SequenceNumber { get; set; }
    public decimal Time { get; set; }
    public decimal Amount { get; set; }
    public int Nature { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
