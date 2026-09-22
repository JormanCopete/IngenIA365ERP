using FluentValidation;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;

/// <summary>
/// Datos laborales y de banca de nómina de un empleado (feature 008): la parte de
/// <c>RegisterEmployeeCommand</c> que no es la persona. Lo comparten el registro sobre una
/// persona existente y el alta en un paso (<c>RegisterEmployeeWithPersonCommand</c>).
/// Desde la feature 010 trae además los bloques <see cref="Pila"/> y <see cref="Dian"/>, la etapa
/// del aprendiz y el banco de dispersión (contracts/api.md §12); los cuatro son opcionales en el
/// alta y, si no vienen, la ficha nace con las derivaciones por defecto.
/// </summary>
public record EmployeeInput : IFichaPilaDian
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

    // Feature 010 (contracts/api.md §12)
    public PilaEmployeeInput? Pila { get; init; }
    public DianEmployeeInput? Dian { get; init; }
    /// <summary>Etapa del contrato de aprendizaje; obligatoria si la clase es aprendiz o pasante.</summary>
    public ApprenticeStage? ApprenticeStage { get; init; }
    /// <summary>Banco destino de la dispersión (<c>COR_Banks</c>, código ACH en <c>TransferCode</c>).</summary>
    public Guid? DisbursementBankPublicId { get; init; }
}

/// <summary>
/// Lo que la PILA necesita de la ficha (R9; data-model §1.4). El departamento (2 dígitos) y el
/// municipio (3) DIVIPOLA del lugar de trabajo se guardan juntos en
/// <c>WorkMunicipalityDaneCode</c>. <see cref="SalaryTypeCode"/> es <c>F</c> fijo, <c>V</c>
/// variable o <c>X</c> integral y se deriva de <c>SalaryType</c>.
/// </summary>
public sealed record PilaEmployeeInput(
    string? ContributorType = null,
    string? ContributorSubtype = null,
    string? DivipolaDepartment = null,
    string? DivipolaMunicipality = null,
    string? EconomicActivityCode = null,
    string? WorkCenter = null,
    string? SalaryTypeCode = null,
    bool ForeignNotPensionObligated = false,
    bool ColombianAbroad = false,
    PensionTransitionRegime PensionTransitionRegime = PensionTransitionRegime.Unknown,
    bool HighRiskPension = false);

/// <summary>
/// Lo que el documento de nómina electrónica necesita (R10). <see cref="WorkerType"/> y
/// <see cref="WorkerSubtype"/> son las mismas columnas que el tipo y subtipo de cotizante PILA
/// (D-05): si vienen en los dos bloques manda el de PILA. <see cref="PaymentMethodCode"/> vacío
/// se deriva de la forma de pago por la política <c>DianMedioPagoMapa</c>.
/// </summary>
public sealed record DianEmployeeInput(
    string? WorkerType = null,
    string? WorkerSubtype = null,
    DianContractType? ContractTypeDian = null,
    bool HighRiskPension = false,
    string? PaymentMethodCode = null,
    string? WorkAddress = null);

/// <summary>Los cuatro campos de la feature 010 que comparten el alta y la edición de la ficha.</summary>
public interface IFichaPilaDian
{
    PilaEmployeeInput? Pila { get; }
    DianEmployeeInput? Dian { get; }
    ApprenticeStage? ApprenticeStage { get; }
    Guid? DisbursementBankPublicId { get; }
}

public sealed class PilaEmployeeInputValidator : AbstractValidator<PilaEmployeeInput>
{
    public PilaEmployeeInputValidator()
    {
        RuleFor(x => x.ContributorType).Matches("^[0-9]{2}$").When(x => !string.IsNullOrEmpty(x.ContributorType)).WithMessage("El tipo de cotizante PILA son dos dígitos (01, 12, 19, 51…).");
        RuleFor(x => x.ContributorSubtype).Matches("^[0-9]{2}$").When(x => !string.IsNullOrEmpty(x.ContributorSubtype)).WithMessage("El subtipo de cotizante PILA son dos dígitos (00, 01…).");
        RuleFor(x => x.DivipolaDepartment).Matches("^[0-9]{2}$").When(x => !string.IsNullOrEmpty(x.DivipolaDepartment)).WithMessage("El departamento DIVIPOLA son dos dígitos.");
        RuleFor(x => x.DivipolaMunicipality).Matches("^[0-9]{3}$").When(x => !string.IsNullOrEmpty(x.DivipolaMunicipality)).WithMessage("El municipio DIVIPOLA son tres dígitos.");
        RuleFor(x => x).Must(x => string.IsNullOrEmpty(x.DivipolaDepartment) == string.IsNullOrEmpty(x.DivipolaMunicipality))
            .WithMessage("Departamento y municipio DIVIPOLA van juntos: los dos o ninguno.");
        RuleFor(x => x.EconomicActivityCode).Matches("^[0-9]{1,7}$").When(x => !string.IsNullOrEmpty(x.EconomicActivityCode)).WithMessage("La actividad económica son hasta siete dígitos (Decreto 768 de 2022).");
        RuleFor(x => x.WorkCenter).MaximumLength(9).WithMessage("El centro de trabajo admite hasta nueve caracteres.");
        RuleFor(x => x.SalaryTypeCode).Must(c => c is "F" or "V" or "X").When(x => !string.IsNullOrEmpty(x.SalaryTypeCode)).WithMessage("El tipo de salario es F (fijo), V (variable) o X (integral).");
        RuleFor(x => x.PensionTransitionRegime).IsInEnum();
    }
}

public sealed class DianEmployeeInputValidator : AbstractValidator<DianEmployeeInput>
{
    public DianEmployeeInputValidator()
    {
        RuleFor(x => x.WorkerType).Matches("^[0-9]{2}$").When(x => !string.IsNullOrEmpty(x.WorkerType)).WithMessage("El tipo de trabajador DIAN son dos dígitos (tabla 5.5.1).");
        RuleFor(x => x.WorkerSubtype).Matches("^[0-9]{2}$").When(x => !string.IsNullOrEmpty(x.WorkerSubtype)).WithMessage("El subtipo de trabajador DIAN son dos dígitos.");
        RuleFor(x => x.ContractTypeDian).IsInEnum().When(x => x.ContractTypeDian is not null).WithMessage("El tipo de contrato DIAN va de 1 (término fijo) a 5 (prácticas o pasantías).");
        RuleFor(x => x.PaymentMethodCode).Matches("^[0-9]{1,3}$").When(x => !string.IsNullOrEmpty(x.PaymentMethodCode)).WithMessage("El medio de pago DIAN son hasta tres dígitos (tabla 5.3.3.2).");
        RuleFor(x => x.WorkAddress).MaximumLength(120);
    }
}

public class EmployeeInputValidator : AbstractValidator<EmployeeInput>
{
    public EmployeeInputValidator()
    {
        RuleFor(x => x.BaseSalary).GreaterThan(0).WithMessage("Salario base debe ser mayor a 0.");
        RuleFor(x => x.HireDate).NotEmpty().WithMessage("Fecha de ingreso requerida.");
        RuleFor(x => x.ContractType).InclusiveBetween(0, 10).WithMessage("Tipo de contrato invalido.");
        RuleFor(x => x.PayrollBankAccountType).InclusiveBetween(0, 2);
        RuleFor(x => x.Pila!).SetValidator(new PilaEmployeeInputValidator()).When(x => x.Pila is not null);
        RuleFor(x => x.Dian!).SetValidator(new DianEmployeeInputValidator()).When(x => x.Dian is not null);
        RuleFor(x => x.ApprenticeStage).IsInEnum().When(x => x.ApprenticeStage is not null);
    }
}
