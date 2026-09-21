using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Domain.Payroll.Pila;

/// <summary>
/// Códigos de los parámetros legales que necesita la generación de la PILA (Resolución
/// 2388/2016, feature 010 US5). Sólo códigos; los valores y sus vigencias viven en
/// <c>PAY_LegalParameters</c>. Lista propia del proceso (research R4): no se suma a
/// <see cref="LegalParameterCodes.Required"/> para no negar la nómina ordinaria de las
/// cooperativas que ya están en producción.
/// </summary>
public static class PilaParameterCodes
{
    // --- Fondo de solidaridad pensional (Ley 100/1993 art. 27; Ley 797/2003 art. 8; Ley 2381/2024 art. 20) ---
    public const string SolidarityFundThresholdSmmlv = "FSP_UMBRAL_SMMLV";
    public const string SolidarityFundTable = LegalParameterCodes.SolidarityFundTable;

    // --- Ingreso base de cotización ---
    public const string ContributionBaseMinimumSmmlv = "IBC_MINIMO_SMMLV";
    public const string ContributionBaseCapSmmlv = LegalParameterCodes.ContributionBaseCapSmmlv;

    /// <summary>Múltiplo al que se aproxima el IBC hacia arriba (Decreto 780/2016 art. 3.2.1.5): 1 = al peso.</summary>
    public const string ContributionBaseRounding = "PILA_IBC_REDONDEO";

    /// <summary>Múltiplo al que se aproxima cada aporte hacia arriba (Decreto 780/2016 art. 3.2.1.5).</summary>
    public const string ContributionRoundingMultiple = "PILA_APORTE_REDONDEO_MULTIPLO";

    /// <summary>Tabla: dos últimos dígitos del NIT → día hábil de pago (Decreto 780/2016 art. 3.2.2.1). Sólo para el aviso.</summary>
    public const string PaymentDeadlineByNitTable = "PILA_PLAZO_PAGO_POR_NIT";

    // --- Tarifas (las mismas de la nómina ordinaria) ---
    public const string HealthEmployeePct = LegalParameterCodes.HealthEmployeePct;
    public const string HealthEmployerPct = LegalParameterCodes.HealthEmployerPct;
    public const string HealthApprenticePct = LegalParameterCodes.HealthApprenticePct;
    public const string PensionEmployeePct = LegalParameterCodes.PensionEmployeePct;
    public const string PensionEmployerPct = LegalParameterCodes.PensionEmployerPct;
    public const string WorkRiskClass1Pct = LegalParameterCodes.WorkRiskClass1Pct;
    public const string WorkRiskClass2Pct = LegalParameterCodes.WorkRiskClass2Pct;
    public const string WorkRiskClass3Pct = LegalParameterCodes.WorkRiskClass3Pct;
    public const string WorkRiskClass4Pct = LegalParameterCodes.WorkRiskClass4Pct;
    public const string WorkRiskClass5Pct = LegalParameterCodes.WorkRiskClass5Pct;
    public const string FamilyCompensationPct = LegalParameterCodes.FamilyCompensationPct;
    public const string SenaPct = LegalParameterCodes.SenaPct;
    public const string IcbfPct = LegalParameterCodes.IcbfPct;
    public const string PayrollExemptionThresholdSmmlv = LegalParameterCodes.PayrollExemptionThresholdSmmlv;
    public const string HoursPerMonth = LegalParameterCodes.HoursPerMonth;
    public const string Smmlv = LegalParameterCodes.Smmlv;
    public const string IntegralSalaryBasePct = LegalParameterCodes.IntegralSalaryBasePct;

    /// <summary>Sin vigencia de cualquiera de estos al primer día del mes, no se genera la planilla.</summary>
    public static readonly IReadOnlyList<string> Required =
    [
        SolidarityFundThresholdSmmlv, SolidarityFundTable,
        ContributionBaseMinimumSmmlv, ContributionBaseCapSmmlv, ContributionBaseRounding, ContributionRoundingMultiple,
        HealthEmployeePct, HealthEmployerPct, PensionEmployeePct, PensionEmployerPct,
        WorkRiskClass1Pct, WorkRiskClass2Pct, WorkRiskClass3Pct, WorkRiskClass4Pct, WorkRiskClass5Pct,
        FamilyCompensationPct, SenaPct, IcbfPct, PayrollExemptionThresholdSmmlv,
        HoursPerMonth, Smmlv, IntegralSalaryBasePct,
    ];

    /// <summary>Alimentan el aviso de plazo de pago; su ausencia no niega la generación.</summary>
    public static readonly IReadOnlyList<string> Avisos = [PaymentDeadlineByNitTable];
}
