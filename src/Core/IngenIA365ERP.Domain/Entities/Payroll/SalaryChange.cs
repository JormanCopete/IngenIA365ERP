using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_SalaryChanges] (nom_novsalario).</summary>
public class SalaryChange : AuditableEntityLong
{
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal NewSalary { get; set; }

    [MaxLength(20)]
    public string? UserName { get; set; }

    public DateTime? EntryDate { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
