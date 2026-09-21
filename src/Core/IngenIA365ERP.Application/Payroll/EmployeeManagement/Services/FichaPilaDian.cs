using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Services;

/// <summary>
/// Cómo se escriben en la ficha los datos de PILA, DIAN, la etapa del aprendiz y el banco de
/// dispersión (feature 010, contracts/api.md §12), en un solo sitio para el alta
/// (<c>EmployeeRegistrar</c>) y la edición (<c>UpdateEmployeeCommand</c>). Un bloque que no
/// viene <b>no toca</b> lo que había: un cliente que sólo edita el salario no borra el DIVIPOLA.
/// </summary>
public static class FichaPilaDian
{
    public static readonly Error ApprenticeStageRequired = new("Payroll.Employee.ApprenticeStageRequired",
        "Un aprendiz o pasante necesita la etapa (lectiva o práctica): decide sus prestaciones y su tipo de cotizante en la PILA.");

    /// <summary>
    /// <c>SalaryType</c> heredado de SOLIDO como código PILA de tipo de salario: 0 fijo (F),
    /// 1 variable (V), 2 integral (X). La clase <c>IntegralSalary</c> manda sobre la columna.
    /// </summary>
    public static string SalaryTypeCode(Employee e) =>
        e.EmployeeClass == EmployeeClass.IntegralSalary ? "X" : e.SalaryType switch { 1 => "V", 2 => "X", _ => "F" };

    public static int SalaryTypeDesdeCodigo(string codigo) => codigo switch { "V" => 1, "X" => 2, _ => 0 };

    /// <summary>Departamento (2) y municipio (3) DIVIPOLA a partir del código DANE de cinco dígitos guardado.</summary>
    public static (string? Departamento, string? Municipio) Divipola(string? daneCode) =>
        string.IsNullOrWhiteSpace(daneCode) || daneCode.Length < 5 ? (null, null) : (daneCode[..2], daneCode[2..5]);

    /// <summary>
    /// Aplica los bloques que vengan y comprueba la regla del aprendiz. <paramref name="disbursementBankId"/>
    /// ya resuelto por quien llama (nulo = no se toca; 0 = se quita). <paramref name="clearApprenticeStage"/>
    /// deja la etapa en nulo (D-29): el nulo a secas no la toca, así que quitarla es una orden aparte.
    /// </summary>
    public static Error? Aplicar(Employee e, IFichaPilaDian input, int? disbursementBankId, bool clearApprenticeStage = false)
    {
        // La regla del aprendiz se mira antes de tocar la ficha: un comando rechazado no deja nada a medias
        // en la entidad seguida por el contexto (la etapa en nulo, por ejemplo, que otro guardado arrastraría).
        var etapa = clearApprenticeStage ? null : input.ApprenticeStage ?? e.ApprenticeStage;
        if (e.EmployeeClass is EmployeeClass.Apprentice or EmployeeClass.Intern && etapa is null)
            return ApprenticeStageRequired;

        if (input.Pila is { } pila)
        {
            e.PilaContributorType = Vacio(pila.ContributorType);
            e.PilaContributorSubType = Vacio(pila.ContributorSubtype);
            e.WorkMunicipalityDaneCode = string.IsNullOrEmpty(pila.DivipolaDepartment) || string.IsNullOrEmpty(pila.DivipolaMunicipality)
                ? null
                : pila.DivipolaDepartment.Trim() + pila.DivipolaMunicipality.Trim();
            e.EconomicActivityCode = Vacio(pila.EconomicActivityCode);
            e.WorkCenterCode = Vacio(pila.WorkCenter);
            if (!string.IsNullOrWhiteSpace(pila.SalaryTypeCode)) e.SalaryType = SalaryTypeDesdeCodigo(pila.SalaryTypeCode.Trim().ToUpperInvariant());
            e.ForeignNotRequiredToContributePension = pila.ForeignNotPensionObligated;
            e.ColombianAbroad = pila.ColombianAbroad;
            e.PensionTransitionRegime = pila.PensionTransitionRegime;
            e.HighRiskPension = pila.HighRiskPension;
        }

        if (input.Dian is { } dian)
        {
            // D-05: tipo y subtipo son las mismas columnas que el cotizante PILA; el bloque PILA manda si vino.
            if (input.Pila is null || string.IsNullOrEmpty(input.Pila.ContributorType)) e.PilaContributorType = Vacio(dian.WorkerType) ?? e.PilaContributorType;
            if (input.Pila is null || string.IsNullOrEmpty(input.Pila.ContributorSubtype)) e.PilaContributorSubType = Vacio(dian.WorkerSubtype) ?? e.PilaContributorSubType;
            e.DianContractType = dian.ContractTypeDian;
            e.DianPaymentMethodCode = Vacio(dian.PaymentMethodCode);
            e.WorkAddress = Vacio(dian.WorkAddress);
            if (input.Pila is null) e.HighRiskPension = dian.HighRiskPension;
        }

        e.ApprenticeStage = etapa;
        if (disbursementBankId is { } banco) e.DisbursementBankId = banco == 0 ? null : banco;
        return null;
    }

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
