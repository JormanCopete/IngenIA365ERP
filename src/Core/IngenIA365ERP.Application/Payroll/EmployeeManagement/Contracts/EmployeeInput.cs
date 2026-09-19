using FluentValidation;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;

/// <summary>
/// Datos laborales y de banca de nómina de un empleado (feature 008): la parte de
/// <c>RegisterEmployeeCommand</c> que no es la persona. Lo comparten el registro sobre una
/// persona existente y el alta en un paso (<c>RegisterEmployeeWithPersonCommand</c>).
/// </summary>
public record EmployeeInput
{
    // Datos laborales
    public decimal BaseSalary { get; init; }
    public int ContractType { get; init; }
    public DateTime HireDate { get; init; }

    // Lookups (resueltos a Id en EmployeeRegistrar)
    public Guid? HealthInsurancePublicId { get; init; }
    public Guid? PensionProviderPublicId { get; init; }
    public Guid? WorkRiskProviderPublicId { get; init; }

    /// <summary>
    /// Clase de riesgo ARL (fila de <c>PAY_WorkRiskRates</c>). Sin ella el motor no
    /// puede calcular el aporte a riesgos laborales y la liquidación queda bloqueada
    /// con «Sin clase de riesgo ARL registrada en la ficha».
    /// </summary>
    public Guid? WorkRiskRatePublicId { get; init; }
    /// <summary>Fondo de cesantías (fila de <c>PAY_SeveranceProviders</c>). No afecta la liquidación mensual; importa para la consignación anual y los reportes.</summary>
    public Guid? SeveranceProviderPublicId { get; init; }
    /// <summary>Caja de compensación familiar (fila de <c>PAY_FamilyCompensationFunds</c>).</summary>
    public Guid? FamilyCompensationFundPublicId { get; init; }
    /// <summary>Plan de nómina al que entra. Nulo = el plan por defecto de la cooperativa.</summary>
    public Guid? PayrollPlanPublicId { get; init; }

    // Banca nomina
    public Guid? PayrollBankPublicId { get; init; }
    public string? PayrollBankAccountNumber { get; init; }
    public int PayrollBankAccountType { get; init; }
}

public class EmployeeInputValidator : AbstractValidator<EmployeeInput>
{
    public EmployeeInputValidator()
    {
        RuleFor(x => x.BaseSalary).GreaterThan(0).WithMessage("Salario base debe ser mayor a 0.");
        RuleFor(x => x.HireDate).NotEmpty().WithMessage("Fecha de ingreso requerida.");
        RuleFor(x => x.ContractType).InclusiveBetween(0, 10).WithMessage("Tipo de contrato invalido.");
        RuleFor(x => x.PayrollBankAccountType).InclusiveBetween(0, 2);
    }
}
