namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// Códigos de los parámetros legales que el motor conoce. Sólo los códigos: los
/// valores (salario mínimo, porcentajes, tablas) son datos con vigencia en
/// <c>PAY_LegalParameters</c>, cargados por la semilla del año y editables por la
/// cooperativa (FR-010). Si alguno de <see cref="Required"/> no tiene vigencia a la
/// fecha de fin del período, el motor se niega a calcular y lo nombra (FR-011).
/// </summary>
public static class LegalParameterCodes
{
    public const string Smmlv = "SMMLV";
    public const string TransportAllowance = "AUX_TRANSPORTE";
    public const string TransportAllowanceCapSmmlv = "AUX_TRANSPORTE_TOPE_SMMLV";
    public const string Uvt = "UVT";
    public const string HealthEmployeePct = "SALUD_EMPLEADO_PCT";
    public const string PensionEmployeePct = "PENSION_EMPLEADO_PCT";
    public const string HealthEmployerPct = "SALUD_EMPLEADOR_PCT";
    public const string PensionEmployerPct = "PENSION_EMPLEADOR_PCT";
    public const string SolidarityFundTable = "FSP_TABLA";
    public const string WorkRiskClass1Pct = "ARL_CLASE_I_PCT";
    public const string WorkRiskClass2Pct = "ARL_CLASE_II_PCT";
    public const string WorkRiskClass3Pct = "ARL_CLASE_III_PCT";
    public const string WorkRiskClass4Pct = "ARL_CLASE_IV_PCT";
    public const string WorkRiskClass5Pct = "ARL_CLASE_V_PCT";
    public const string SenaPct = "SENA_PCT";
    public const string IcbfPct = "ICBF_PCT";
    public const string FamilyCompensationPct = "CAJA_PCT";
    public const string SeveranceProvisionPct = "PROV_CESANTIAS_PCT";
    public const string SeveranceInterestProvisionPct = "PROV_INT_CESANTIAS_PCT";
    public const string ServiceBonusProvisionPct = "PROV_PRIMA_PCT";
    public const string VacationProvisionPct = "PROV_VACACIONES_PCT";
    public const string WithholdingTableUvt = "RETEFTE_TABLA_UVT";
    public const string WithholdingExemptIncomePct = "RETEFTE_RENTA_EXENTA_PCT";
    public const string WithholdingExemptIncomeCapUvt = "RETEFTE_RENTA_EXENTA_TOPE_UVT";
    public const string WithholdingDeductionsCapPct = "RETEFTE_DEDUCCIONES_TOPE_PCT";
    public const string MaxDeductionOfSalaryPct = "MAX_DEDUCCION_SALARIO_PCT";
    public const string IntegralSalaryBasePct = "SALARIO_INTEGRAL_BASE_PCT";
    public const string ContributionBaseCapSmmlv = "IBC_TOPE_SMMLV";
    public const string SickLeaveEmployerDays = "INCAPACIDAD_EMPLEADOR_DIAS";
    public const string SickLeaveEmployerPct = "INCAPACIDAD_EMPLEADOR_PCT";
    public const string HoursPerMonth = "HORAS_MES";

    /// <summary>Tope (en SMMLV) por debajo del cual el empleador está exonerado de salud, SENA e ICBF (Ley 1607 de 2012).</summary>
    public const string PayrollExemptionThresholdSmmlv = "EXONERACION_PARAFISCALES_TOPE_SMMLV";

    /// <summary>Tope mensual en UVT de deducciones más rentas exentas de la base de retención.</summary>
    public const string WithholdingDeductionsCapUvt = "RETEFTE_DEDUCCIONES_TOPE_UVT";

    /// <summary>Sin vigencia de cualquiera de estos a la fecha del período, no hay cálculo.</summary>
    public static readonly IReadOnlyList<string> Required =
    [
        Smmlv, TransportAllowance, TransportAllowanceCapSmmlv, Uvt,
        HealthEmployeePct, PensionEmployeePct, HealthEmployerPct, PensionEmployerPct,
        SolidarityFundTable,
        WorkRiskClass1Pct, WorkRiskClass2Pct, WorkRiskClass3Pct, WorkRiskClass4Pct, WorkRiskClass5Pct,
        SenaPct, IcbfPct, FamilyCompensationPct,
        SeveranceProvisionPct, SeveranceInterestProvisionPct, ServiceBonusProvisionPct, VacationProvisionPct,
        WithholdingTableUvt, WithholdingExemptIncomePct, WithholdingExemptIncomeCapUvt,
        WithholdingDeductionsCapPct, WithholdingDeductionsCapUvt,
        MaxDeductionOfSalaryPct, IntegralSalaryBasePct, ContributionBaseCapSmmlv,
        SickLeaveEmployerDays, SickLeaveEmployerPct, HoursPerMonth,
        PayrollExemptionThresholdSmmlv,
    ];

    /// <summary>Código del porcentaje de ARL según la clase de riesgo (1..5).</summary>
    public static string WorkRiskPct(int riskClass) => riskClass switch
    {
        1 => WorkRiskClass1Pct,
        2 => WorkRiskClass2Pct,
        3 => WorkRiskClass3Pct,
        4 => WorkRiskClass4Pct,
        5 => WorkRiskClass5Pct,
        _ => throw new ArgumentOutOfRangeException(nameof(riskClass), riskClass, "La clase de riesgo ARL va de 1 a 5."),
    };
}
