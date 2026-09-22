using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Domain.Payroll.Settlements;

/// <summary>
/// Códigos de los parámetros legales que necesitan las liquidaciones especiales (prima,
/// cesantías e intereses, vacaciones, definitiva). Sólo códigos: los valores —días de
/// prima por año, porcentaje de intereses, tabla de indemnización, topes en UVT— son
/// datos con vigencia en <c>PAY_LegalParameters</c> (feature 010, FR-003).
///
/// <para>
/// La lista <see cref="Required"/> es <b>propia de este proceso</b> y no entra en
/// <see cref="LegalParameterCodes.Required"/> a propósito (research R4): esa lista la
/// exige la nómina ordinaria, que ya corre en producción, y sumarle códigos nuevos negaría
/// la quincena de una cooperativa a la que todavía no se le sembraron los parámetros de
/// prestaciones. Cada proceso pregunta por lo suyo con <c>ParameterSet.Missing</c>.
/// </para>
/// </summary>
public static class SettlementParameterCodes
{
    // --- Prima de servicios (CST art. 306) ---
    public const string ServiceBonusDaysPerYear = "PRIMA_DIAS_ANIO";

    // --- Cesantías e intereses (CST arts. 249 y 253; Ley 50/1990 art. 99; Ley 52/1975) ---
    public const string SeveranceDaysPerYear = "CESANTIAS_DIAS_ANIO";

    /// <summary>Meses hacia atrás en los que un cambio de salario obliga a promediar el año (CST art. 253).</summary>
    public const string SeveranceStabilityWindowMonths = "CESANTIAS_VENTANA_ESTABILIDAD_MESES";

    public const string SeveranceInterestPct = "INT_CESANTIAS_PCT";

    // --- Vacaciones (CST arts. 186 y 189) ---
    public const string VacationDaysPerYear = "VACACIONES_DIAS_ANIO";
    public const string VacationCompensablePct = "VACACIONES_COMPENSABLE_PCT";

    // --- Indemnización por despido sin justa causa (CST art. 64) ---

    /// <summary>Tabla en SMMLV, no marginal: <c>FixedValue</c> = días del primer año, <c>Rate</c> = días por año adicional (D-08).</summary>
    public const string SeverancePayTable = "INDEMNIZACION_TABLA";

    public const string SeverancePayThresholdSmmlv = "INDEMNIZACION_UMBRAL_SMMLV";

    /// <summary>Mínimo de días de la indemnización en contrato por obra o labor.</summary>
    public const string SeverancePayWorkContractMinimumDays = "INDEMNIZACION_OBRA_MINIMO_DIAS";

    // --- Retención en la fuente de las liquidaciones (research R5, FR-006a) ---

    /// <summary>Ingreso mensual promedio (UVT) hasta el cual las cesantías y sus intereses son exentos (ET art. 206 num. 4).</summary>
    public const string SeveranceExemptionCapUvt = "CESANTIAS_EXENCION_TOPE_UVT";

    /// <summary>Tabla en UVT, no marginal: <c>Rate</c> = porcentaje NO gravado según el ingreso promedio de seis meses.</summary>
    public const string SeveranceTaxableTableUvt = "CESANTIAS_GRAVADA_TABLA_UVT";

    /// <summary>Tarifa de retención de la indemnización (ET art. 401-3).</summary>
    public const string SeverancePayWithholdingPct = "INDEMNIZACION_RETEFTE_PCT";

    /// <summary>Ingreso mensual (UVT) por encima del cual la indemnización se retiene (ET art. 401-3).</summary>
    public const string SeverancePayWithholdingCapUvt = "INDEMNIZACION_RETEFTE_TOPE_UVT";

    /// <summary>Cupo anual (UVT) de la renta exenta del 25 % (ET art. 206 num. 10).</summary>
    public const string WithholdingExemptIncomeAnnualCapUvt = "RETEFTE_RENTA_EXENTA_TOPE_ANUAL_UVT";

    /// <summary>Cupo anual (UVT) de deducciones más rentas exentas (ET art. 388).</summary>
    public const string WithholdingDeductionsAnnualCapUvt = "RETEFTE_DEDUCCIONES_TOPE_ANUAL_UVT";

    // --- Avisos de calendario (D-07): MMDD en Value, sólo para la pantalla ---
    public const string ServiceBonusDeadlineFirstSemester = "PRIMA_FECHA_LIMITE_S1";
    public const string ServiceBonusDeadlineSecondSemester = "PRIMA_FECHA_LIMITE_S2";
    public const string SeveranceDepositDeadline = "CESANTIAS_FECHA_LIMITE_CONSIGNACION";
    public const string SeveranceInterestDeadline = "INT_CESANTIAS_FECHA_LIMITE";

    /// <summary>
    /// Sin vigencia de cualquiera de estos a la fecha de corte no hay liquidación especial. Incluye
    /// los de la nómina ordinaria que estas liquidaciones también leen (SMMLV, auxilio, UVT, tabla
    /// de retención, aportes del empleado) para que el aviso «faltan parámetros» sea completo.
    /// </summary>
    public static readonly IReadOnlyList<string> Required =
    [
        ServiceBonusDaysPerYear,
        SeveranceDaysPerYear, SeveranceStabilityWindowMonths, SeveranceInterestPct,
        VacationDaysPerYear, VacationCompensablePct,
        SeverancePayTable, SeverancePayThresholdSmmlv, SeverancePayWorkContractMinimumDays,
        SeveranceExemptionCapUvt, SeveranceTaxableTableUvt,
        SeverancePayWithholdingPct, SeverancePayWithholdingCapUvt,
        WithholdingExemptIncomeAnnualCapUvt, WithholdingDeductionsAnnualCapUvt,
        LegalParameterCodes.Smmlv, LegalParameterCodes.TransportAllowance, LegalParameterCodes.TransportAllowanceCapSmmlv,
        LegalParameterCodes.Uvt, LegalParameterCodes.WithholdingTableUvt,
        LegalParameterCodes.WithholdingExemptIncomePct, LegalParameterCodes.WithholdingExemptIncomeCapUvt,
        LegalParameterCodes.WithholdingDeductionsCapPct, LegalParameterCodes.WithholdingDeductionsCapUvt,
        LegalParameterCodes.HealthEmployeePct, LegalParameterCodes.PensionEmployeePct, LegalParameterCodes.SolidarityFundTable,
        LegalParameterCodes.IntegralSalaryBasePct, LegalParameterCodes.ContributionBaseCapSmmlv,
    ];

    /// <summary>Fechas límite legales: alimentan avisos en pantalla, nunca un cálculo. Su ausencia no niega nada.</summary>
    public static readonly IReadOnlyList<string> Avisos =
    [
        ServiceBonusDeadlineFirstSemester, ServiceBonusDeadlineSecondSemester,
        SeveranceDepositDeadline, SeveranceInterestDeadline,
    ];
}
