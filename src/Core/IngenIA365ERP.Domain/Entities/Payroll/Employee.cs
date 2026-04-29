using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_Employees] (nom_empleados).</summary>
public class Employee : AuditableEntity
{
    public int? PersonId { get; set; }
    public int PayrollCompanyId { get; set; }
    public string CostCenterId { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string IssuedAt { get; set; } = string.Empty;
    public int EmployeeClass { get; set; }
    public int Gender { get; set; }
    public string Address { get; set; } = string.Empty;
    public int CityId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BankId { get; set; } = string.Empty;
    public int AccountType { get; set; }
    public string BankAccountNumber { get; set; } = string.Empty;
    public int AcademicLevel { get; set; }
    public int PayrollClass { get; set; }
    public int PaymentMethod { get; set; }
    public int PositionId { get; set; }
    public decimal Salary { get; set; }
    public int SalaryType { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int ContractType { get; set; }
    public DateTime ContractEndDate { get; set; }
    public DateTime JoinDate { get; set; }
    public DateTime BirthDate { get; set; }
    public DateTime TerminationDate { get; set; }
    public string TerminationCause { get; set; } = string.Empty;
    public int Status { get; set; }
    public int EmployeeType { get; set; }
    public decimal? WithholdingTaxRate { get; set; }
    public int HealthInsuranceId { get; set; }
    public int PensionFundId { get; set; }
    public int WorkRiskId { get; set; }
    public int FamilySubsidyId { get; set; }

    // Navigation
    public Person? Person { get; set; }
}
