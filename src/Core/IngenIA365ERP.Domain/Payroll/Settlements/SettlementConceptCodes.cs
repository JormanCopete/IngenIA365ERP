using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Domain.Payroll.Settlements;

/// <summary>
/// Códigos de los conceptos que el motor de liquidaciones reconoce por lo que SON
/// (data-model §1.7): el rubro que paga, la provisión que cancela y la retención que
/// aplica. Son identificadores, no valores. Los códigos de la nómina ordinaria que también
/// usa (salario, auxilio, aportes, retención, Cartera) están en
/// <see cref="WellKnownConceptCodes"/>; los nuevos viven aquí hasta que la semilla de la
/// feature 010 los lleve a ese mismo catálogo.
/// </summary>
public static class SettlementConceptCodes
{
    public const string ServiceBonus = "PRIMA";
    public const string Severance = "CESANTIAS";
    public const string SeveranceInterest = "INT_CESANTIAS";
    public const string VacationsEnjoyed = "VACACIONES_LIQ";
    public const string VacationsCompensated = "VACACIONES_COMP";
    public const string SeverancePay = "INDEMNIZACION";
    public const string RetirementBonus = "BONIF_RETIRO";
    public const string PendingSalary = "SALARIO_PENDIENTE";

    public const string ServiceBonusProvisionAdjustment = "PRIMA_AJUSTE_PROV";
    public const string SeveranceProvisionAdjustment = "CESANTIAS_AJUSTE_PROV";
    public const string SeveranceInterestProvisionAdjustment = "INT_CESANTIAS_AJUSTE_PROV";
    public const string VacationProvisionAdjustment = "VACACIONES_AJUSTE_PROV";

    public const string ServiceBonusWithholding = "RETEFTE_PRIMA";
    public const string SeveranceWithholding = "RETEFTE_CESANTIAS";
    public const string SeverancePayWithholding = "RETEFTE_INDEMNIZACION";

    /// <summary>Provisiones de la nómina ordinaria que cada rubro cancela.</summary>
    public const string ServiceBonusProvision = "PROV_PRIMA";
    public const string SeveranceProvision = "PROV_CESANTIAS";
    public const string SeveranceInterestProvision = "PROV_INT_CESANTIAS";
    public const string VacationProvision = "PROV_VACACIONES";

    /// <summary>Rubro liquidado → (provisión que cancela, concepto del ajuste).</summary>
    public static (string Provision, string Adjustment)? ProvisionPairFor(string liquidatedCode) => liquidatedCode.ToUpperInvariant() switch
    {
        ServiceBonus => (ServiceBonusProvision, ServiceBonusProvisionAdjustment),
        Severance => (SeveranceProvision, SeveranceProvisionAdjustment),
        SeveranceInterest => (SeveranceInterestProvision, SeveranceInterestProvisionAdjustment),
        VacationsEnjoyed or VacationsCompensated => (VacationProvision, VacationProvisionAdjustment),
        _ => null,
    };
}
