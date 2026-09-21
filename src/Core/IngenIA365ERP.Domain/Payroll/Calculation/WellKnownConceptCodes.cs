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

    // --- Feature 010: conceptos de las liquidaciones especiales (data-model §1.7). ---
    // Los pone el motor de liquidaciones (prima, cesantías, vacaciones, definitiva) por lo que
    // son; la nómina ordinaria no los evalúa aunque sean automáticos (SettlementOnly).

    public const string ServiceBonus = "PRIMA";
    public const string Severance = "CESANTIAS";
    public const string SeveranceInterest = "INT_CESANTIAS";
    public const string VacationPayout = "VACACIONES_LIQ";
    public const string VacationCompensation = "VACACIONES_COMP";
    public const string Indemnity = "INDEMNIZACION";
    public const string RetirementBonus = "BONIF_RETIRO";
    public const string PendingSalary = "SALARIO_PENDIENTE";
    public const string ServiceBonusProvisionAdjustment = "PRIMA_AJUSTE_PROV";
    public const string SeveranceProvisionAdjustment = "CESANTIAS_AJUSTE_PROV";
    public const string SeveranceInterestProvisionAdjustment = "INT_CESANTIAS_AJUSTE_PROV";
    public const string VacationProvisionAdjustment = "VACACIONES_AJUSTE_PROV";
    public const string WithholdingOnServiceBonus = "RETEFTE_PRIMA";
    public const string WithholdingOnSeverance = "RETEFTE_CESANTIAS";
    public const string WithholdingOnIndemnity = "RETEFTE_INDEMNIZACION";

    /// <summary>Provisiones de la nómina ordinaria que cada rubro de las liquidaciones cancela (research «Contabilización de las liquidaciones»).</summary>
    public const string ServiceBonusProvision = "PROV_PRIMA";
    public const string SeveranceProvision = "PROV_CESANTIAS";
    public const string SeveranceInterestProvision = "PROV_INT_CESANTIAS";
    public const string VacationProvision = "PROV_VACACIONES";

    /// <summary>
    /// Rubro liquidado → (provisión que cancela, concepto del ajuste). Es lo que el motor de
    /// liquidaciones y el contabilizador comparten: la prima cancela <c>PROV_PRIMA</c> y lleva la
    /// diferencia a <c>PRIMA_AJUSTE_PROV</c>; las vacaciones disfrutadas y las compensadas cancelan
    /// la misma provisión.
    /// </summary>
    public static (string Provision, string Adjustment)? ProvisionPairFor(string liquidatedCode) => liquidatedCode.ToUpperInvariant() switch
    {
        ServiceBonus => (ServiceBonusProvision, ServiceBonusProvisionAdjustment),
        Severance => (SeveranceProvision, SeveranceProvisionAdjustment),
        SeveranceInterest => (SeveranceInterestProvision, SeveranceInterestProvisionAdjustment),
        VacationPayout or VacationCompensation => (VacationProvision, VacationProvisionAdjustment),
        _ => null,
    };

    /// <summary>La novedad de ausencia que deja el disfrute de vacaciones ya pagado por la liquidación (D-01).</summary>
    public const string VacationLeave = "AUSENCIA_VACACIONES";

    /// <summary>La novedad de vacaciones de la semilla 005: la ordinaria paga los días cuando la política es «paga la nómina».</summary>
    public const string Vacation = "VACACIONES";

    /// <summary>
    /// Automáticos que sólo produce el motor de liquidaciones. La nómina ordinaria los salta en
    /// vez de evaluarlos en cero: sin esto cada empleado de cada corrida quedaría con quince
    /// «valor cero» en sus notas y la pantalla lo marcaría como empleado con observaciones.
    /// </summary>
    public static readonly IReadOnlyList<string> SettlementOnly =
    [
        ServiceBonus, Severance, SeveranceInterest, VacationPayout, VacationCompensation, Indemnity, PendingSalary,
        ServiceBonusProvisionAdjustment, SeveranceProvisionAdjustment, SeveranceInterestProvisionAdjustment, VacationProvisionAdjustment,
        WithholdingOnServiceBonus, WithholdingOnSeverance, WithholdingOnIndemnity,
    ];

    /// <summary>Marcador dentro de un código de parámetro que el motor sustituye por la clase de riesgo ARL (I..V).</summary>
    public const string WorkRiskClassPlaceholder = "{CLASE}";
}
