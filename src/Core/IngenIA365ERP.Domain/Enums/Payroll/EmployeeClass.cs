namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Clase de empleado. Decide qué conceptos aplican (máscara en la definición del
/// concepto), nunca casos escritos en el motor.
/// </summary>
public enum EmployeeClass
{
    Standard = 0,
    IntegralSalary = 1,
    Apprentice = 2,
    Intern = 3,
    Pensioner = 4,
}
