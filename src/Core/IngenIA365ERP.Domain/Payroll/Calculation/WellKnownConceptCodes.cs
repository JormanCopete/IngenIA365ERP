namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// Códigos de los conceptos de la semilla que el motor necesita reconocer por lo que
/// SON, no por cómo se calculan: el salario básico (los días), el auxilio de
/// transporte (la elegibilidad por salario), la retención (procedimiento 1 ó 2), el
/// ajuste de redondeo y los descuentos que ya contabilizó Cartera. Son identificadores,
/// no valores: todo lo numérico sigue en parámetros con vigencia.
/// </summary>
public static class WellKnownConceptCodes
{
    public const string BasicSalary = "SALARIO";
    public const string TransportAllowance = "AUX_TRANSPORTE";
    public const string HealthEmployee = "SALUD_EMP";
    public const string PensionEmployee = "PENSION_EMP";
    public const string SolidarityFund = "FSP";
    public const string Withholding = "RETEFTE";
    public const string RoundingAdjustment = "AJUSTE_REDONDEO";
    public const string LoanDeduction = "DESC_CARTERA";
    public const string GeneralSickLeave = "INCAP_GENERAL";
    public const string HealthEmployer = "SALUD_EMPLEADOR";
    public const string PensionEmployer = "PENSION_EMPLEADOR";
    public const string WorkRisk = "ARL";
    public const string Sena = "SENA";
    public const string Icbf = "ICBF";
    public const string FamilyCompensation = "CAJA";

    /// <summary>Aportes del empleador exonerados por debajo del tope de la Ley 1607 (parámetro).</summary>
    public static readonly IReadOnlyList<string> EmployerExemptionApplies = [HealthEmployer, Sena, Icbf];

    /// <summary>Marcador dentro de un código de parámetro que el motor sustituye por la clase de riesgo ARL (I..V).</summary>
    public const string WorkRiskClassPlaceholder = "{CLASE}";
}
