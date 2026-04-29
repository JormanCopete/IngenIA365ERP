using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_PayrollPlanLiquidations] (nom_liqplan).</summary>
public class PayrollPlanLiquidation : AuditableEntityLong
{
    public int PayPeriodId { get; set; }
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public decimal ConceptId { get; set; }
    public decimal SequenceNumber { get; set; }
    public int Nature { get; set; }
    public int Days { get; set; }
    public decimal Time { get; set; }
    public decimal Amount { get; set; }
    public decimal PaymentMethod { get; set; }

    [MaxLength(8)]
    public string CostCenterCode { get; set; } = string.Empty;

    [MaxLength(14)]
    public string UserName { get; set; } = string.Empty;

    public DateTime SystemDate { get; set; }

    [MaxLength(2)]
    public string RecordType { get; set; } = string.Empty;

    // Navigation
    public Employee? Employee { get; set; }
}
