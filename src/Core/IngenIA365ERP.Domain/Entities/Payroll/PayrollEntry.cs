using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_PayrollEntries] (nom_novedad).</summary>
public class PayrollEntry : AuditableEntityLong
{
    public int Cycle { get; set; }
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int EntityCode { get; set; }
    public decimal EntryType { get; set; }
    public DateTime StartDate { get; set; }
    public decimal AuthorizationNumber { get; set; }
    public decimal IncapacityAmount { get; set; }
    public decimal UpcAmount { get; set; }
    public int Days { get; set; }

    [MaxLength(10)]
    public string NewEntity { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserName { get; set; } = string.Empty;

    public DateTime ProcessDate { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
