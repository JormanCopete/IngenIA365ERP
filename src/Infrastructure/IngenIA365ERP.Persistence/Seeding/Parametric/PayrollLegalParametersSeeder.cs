using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 005 (FR-010, FR-011): los parámetros legales del año con su vigencia y sus
/// tablas por rangos. El motor sólo conoce los códigos de
/// <see cref="LegalParameterCodes"/> y de las listas por proceso de la feature 010; los
/// valores viven aquí como DATOS de la semilla del año y la cooperativa los mantiene desde
/// la pantalla de parámetros legales.
///
/// <para>
/// Idempotente por <c>(Code, ValidFrom)</c>: nunca reemplaza un valor existente. Una
/// vigencia nueva (el año siguiente) es una fila nueva, no una edición. Lo único que toca
/// en una fila ya sembrada es el <c>Source</c>, y sólo mientras siga siendo el texto
/// genérico con el que nació la semilla («Decreto de salario mínimo… 2026»): la feature 010
/// (R4, SC-001) exige que la contadora pueda trazar cada valor a su norma y artículo, y un
/// <c>Source</c> que alguien ya escribió a mano se respeta.
/// </para>
///
/// <para>
/// Feature 010 (data-model §1.6, research R4): los códigos de prestaciones, retiro,
/// procedimiento 2, PILA y DIAN entran aquí con vigencia 2026 y norma exacta, pero
/// <b>no</b> en <see cref="LegalParameterCodes.Required"/>: cada proceso tiene su propia
/// lista, para que el despliegue no niegue la nómina ordinaria de las cooperativas que ya
/// están en producción. Las fechas límite legales son <see cref="LegalParameterKind.DateInYear"/>
/// (<c>MMDD</c>, D-07) y la tabla de indemnización lleva dos valores por tramo
/// (<c>FixedValue</c> = días del primer año, <c>Rate</c> = días por año adicional, D-08).
/// </para>
///
/// <para>
/// <b>Los valores son la referencia de 2026 y deben verificarse contra la norma
/// vigente antes de la primera nómina real</b> (runbook
/// <c>docs/operaciones/nomina-primer-periodo.md</c>); la tabla de la feature 010 está
/// pendiente de la validación de la contadora (research › «Lo que falta del dueño»). Si un
/// valor está mal, se corrige aquí para las cooperativas nuevas y en la pantalla para las
/// existentes.
/// </para>
/// </summary>
public sealed class PayrollLegalParametersSeeder : IDataSeeder
{
    public int Order => 71;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    private static readonly DateTime Vigencia2026 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime Utc(int año, int mes, int día) => new(año, mes, día, 0, 0, 0, DateTimeKind.Utc);

    // --- Normas exactas (feature 010, R4). Cada una cabe en los 200 caracteres de Source. ---
    private const string FuenteSmmlv = "Decreto 1469 de 2025 (Decreto 159 de 2026, mismo valor)";
    private const string FuenteAuxilio = "Decreto 1470 de 2025";
    private const string FuenteAuxilioTope = "Ley 15 de 1959 art. 2; Ley 1 de 1963";
    private const string FuenteUvt = "Resolución DIAN 000238 del 15-12-2025";
    private const string FuenteSalud = "Ley 100 de 1993 art. 204; Ley 1122 de 2007 art. 10";
    private const string FuentePension = "Ley 100 de 1993 art. 20; Ley 797 de 2003 art. 7";
    private const string FuenteSaludAprendiz = "Ley 2466 de 2025 art. 21";
    private const string FuenteArl = "Decreto 1772 de 1994 art. 13 (Decreto 1072 de 2015)";
    private const string FuenteSenaCaja = "Ley 21 de 1982 art. 7";
    private const string FuenteIcbf = "Ley 89 de 1988 art. 1";
    private const string FuenteExoneracion = "ET art. 114-1 (Ley 1607 de 2012 art. 25; Ley 1819 de 2016 art. 65; Ley 1955 de 2019 art. 204)";
    private const string FuenteCesantias = "CST art. 249; Ley 50 de 1990 art. 99";
    private const string FuenteIntereses = "Ley 52 de 1975 art. 1; Ley 50 de 1990 art. 99 num. 2";
    private const string FuentePrima = "CST art. 306 (Ley 1788 de 2016 art. 1)";
    private const string FuenteVacaciones = "CST art. 186";
    private const string FuenteRentaExenta = "ET art. 206 num. 10 (Ley 2277 de 2022 art. 2)";
    private const string FuenteDeducciones = "ET art. 388; DUR 1.2.4.1.6 par. 3 (Decreto 2231 de 2023 art. 9)";
    private const string FuenteArt387 = "ET art. 387";
    private const string FuenteAfcAvp = "ET arts. 126-1 y 126-4 (30 %, 3.800 UVT al año)";
    private const string FuenteRedondeoRetencion = "Práctica DIAN de aproximación al múltiplo de mil (norma exacta pendiente de verificar)";
    private const string FuenteTabla383 = "ET art. 383 (Ley 2010 de 2019 art. 42)";
    private const string FuenteFspLey797 = "Ley 797 de 2003 art. 8 (Ley 100 de 1993 art. 27)";
    private const string FuenteFspLey2381 = "Ley 2381 de 2024 art. 20; Sentencia C-264 de 2026";
    private const string FuenteMaxDeduccion = "CST arts. 149 y 150 (Ley 1429 de 2010 art. 18)";
    private const string FuenteSalarioIntegral = "CST art. 132 num. 2 y 3 (Ley 50 de 1990 art. 18); Ley 100 de 1993 art. 18";
    private const string FuenteIbcTope = "Ley 100 de 1993 art. 18 (Ley 797 de 2003 art. 5)";
    private const string FuenteIncapacidadDias = "Decreto 780 de 2016 art. 3.2.1.10 (Decreto 2943 de 2013 art. 1)";
    private const string FuenteIncapacidadPct = "CST art. 227";
    private const string FuenteHorasMes = "Ley 2101 de 2021 art. 2 (jornada de 42 h desde el 15/07/2026)";
    private const string FuenteIndemnizacion = "CST art. 64 (Ley 789 de 2002 art. 28)";
    private const string FuenteAprendiz = "Ley 2466 de 2025 art. 21; Circular Mintrabajo 0083 de 2025";
    private const string FuenteCesantiasGravadas = "ET art. 206 num. 4";
    private const string FuenteIndemnizacionRetefte = "ET art. 401-3 (Ley 788 de 2002 art. 92); Concepto DIAN 30573 de 2015";
    private const string FuentePilaRedondeo = "Decreto 780 de 2016 art. 3.2.1.5 (Decreto 1990 de 2016)";

    /// <summary>
    /// Los textos con que nació la semilla 2026 (feature 005). Una fila cuyo <c>Source</c> siga
    /// siendo uno de éstos no la tocó nadie, y el seeder la precisa; cualquier otro texto lo
    /// escribió una persona y se respeta. Se conservan para reconocerlos, no para sembrarlos.
    /// </summary>
    public static readonly IReadOnlySet<string> FuentesGenericasAnteriores = new HashSet<string>(StringComparer.Ordinal)
    {
        "Decreto de salario mínimo y auxilio de transporte 2026",
        "Resolución DIAN que fija la UVT 2026",
        "Ley 100 de 1993, Ley 797 de 2003 y Decreto 1072 de 2015",
        "Ley 21 de 1982, Ley 89 de 1988, Ley 1607 de 2012 art. 25",
        "Estatuto Tributario arts. 383, 387 y 206 num. 10",
        "Código Sustantivo del Trabajo y Ley 50 de 1990",
        "Ley 789 de 2002 art. 30 y Decreto 933 de 2003",
        "Ley 2101 de 2021 (jornada de 42 h desde el 15/07/2026)",
        // Los que dejó la migración NominaPrestacionesYDian antes de la revisión de N1 (2026-09-21), cuando
        // sus literales no eran los de aquí: una base migrada con ellos se pone al día en el arranque siguiente.
        "Decreto 1469 de 2025 (salario mínimo 2026; Decreto 159 de 2026, mismo valor)",
        "Decreto 1470 de 2025 (auxilio de transporte 2026)",
        "Resolución DIAN 000238 de 2025 (UVT 2026)",
        "Ley 797 de 2003 art. 8",
    };

    public static IReadOnlyList<PayrollLegalParameter> Catalogo()
    {
        var lista = new List<PayrollLegalParameter>
        {
            Monto(LegalParameterCodes.Smmlv, "Salario mínimo mensual legal vigente", 1_750_905m, FuenteSmmlv),
            Monto(LegalParameterCodes.TransportAllowance, "Auxilio de transporte mensual", 249_095m, FuenteAuxilio),
            Cantidad(LegalParameterCodes.TransportAllowanceCapSmmlv, "Tope de salario para auxilio de transporte (en SMMLV)", 2m, FuenteAuxilioTope),
            Monto(LegalParameterCodes.Uvt, "Unidad de Valor Tributario", 52_374m, FuenteUvt),

            Porcentaje(LegalParameterCodes.HealthEmployeePct, "Salud a cargo del empleado", 4m, FuenteSalud),
            Porcentaje(LegalParameterCodes.PensionEmployeePct, "Pensión a cargo del empleado", 4m, FuentePension),
            Porcentaje(LegalParameterCodes.HealthEmployerPct, "Salud a cargo del empleador", 8.5m, FuenteSalud),
            Porcentaje(LegalParameterCodes.PensionEmployerPct, "Pensión a cargo del empleador", 12m, FuentePension),
            Porcentaje(LegalParameterCodes.HealthApprenticePct, "Salud de aprendices y pasantes (total, a cargo del patrocinador)", 12.5m, FuenteSaludAprendiz),
            Porcentaje(LegalParameterCodes.WorkRiskClass1Pct, "ARL clase de riesgo I", 0.522m, FuenteArl),
            Porcentaje(LegalParameterCodes.WorkRiskClass2Pct, "ARL clase de riesgo II", 1.044m, FuenteArl),
            Porcentaje(LegalParameterCodes.WorkRiskClass3Pct, "ARL clase de riesgo III", 2.436m, FuenteArl),
            Porcentaje(LegalParameterCodes.WorkRiskClass4Pct, "ARL clase de riesgo IV", 4.350m, FuenteArl),
            Porcentaje(LegalParameterCodes.WorkRiskClass5Pct, "ARL clase de riesgo V", 6.960m, FuenteArl),
            Porcentaje(LegalParameterCodes.SenaPct, "SENA", 2m, FuenteSenaCaja),
            Porcentaje(LegalParameterCodes.IcbfPct, "ICBF", 3m, FuenteIcbf),
            Porcentaje(LegalParameterCodes.FamilyCompensationPct, "Caja de compensación familiar", 4m, FuenteSenaCaja),
            Cantidad(LegalParameterCodes.PayrollExemptionThresholdSmmlv, "Tope (SMMLV) de exoneración de salud, SENA e ICBF del empleador", 10m, FuenteExoneracion),

            Porcentaje(LegalParameterCodes.SeveranceProvisionPct, "Provisión de cesantías", 8.33m, FuenteCesantias),
            Porcentaje(LegalParameterCodes.SeveranceInterestProvisionPct, "Provisión de intereses a las cesantías", 1m, FuenteIntereses),
            Porcentaje(LegalParameterCodes.ServiceBonusProvisionPct, "Provisión de prima de servicios", 8.33m, FuentePrima),
            Porcentaje(LegalParameterCodes.VacationProvisionPct, "Provisión de vacaciones", 4.17m, FuenteVacaciones),

            Porcentaje(LegalParameterCodes.WithholdingExemptIncomePct, "Renta exenta laboral", 25m, FuenteRentaExenta),
            Cantidad(LegalParameterCodes.WithholdingExemptIncomeCapUvt, "Tope mensual de la renta exenta (UVT)", 65.83m, FuenteRentaExenta),
            Porcentaje(LegalParameterCodes.WithholdingDeductionsCapPct, "Tope de deducciones y rentas exentas sobre el ingreso neto", 40m, FuenteDeducciones),
            Cantidad(LegalParameterCodes.WithholdingDeductionsCapUvt, "Tope mensual de deducciones y rentas exentas (UVT)", 111.67m, FuenteDeducciones),

            Porcentaje(LegalParameterCodes.MaxDeductionOfSalaryPct, "Máximo de deducciones sobre el salario", 50m, FuenteMaxDeduccion),
            Porcentaje(LegalParameterCodes.IntegralSalaryBasePct, "Base de aportes del salario integral", 70m, FuenteSalarioIntegral),
            Cantidad(LegalParameterCodes.ContributionBaseCapSmmlv, "Tope del IBC (en SMMLV)", 25m, FuenteIbcTope),
            Cantidad(LegalParameterCodes.SickLeaveEmployerDays, "Días de incapacidad a cargo del empleador", 2m, FuenteIncapacidadDias),
            Porcentaje(LegalParameterCodes.SickLeaveEmployerPct, "Porcentaje pagado en incapacidad general", 66.67m, FuenteIncapacidadPct),
            // Ley 2101 de 2021, último escalón: 42 h/semana desde el 15/07/2026 → 210 h/mes (7 h × 30).
            // Decisión del 2026-09-13: la base 2026 lleva ya la norma de julio de 2026 (la plataforma
            // está en pruebas y ninguna nómina real del primer semestre se liquida aquí); los 220 de
            // la jornada de 44 h quedaron en la historia de la semilla, no en la base.
            Cantidad(LegalParameterCodes.HoursPerMonth, "Horas del mes para el valor hora", 210m, FuenteHorasMes),

            // Depuracion de la base de retencion (solo si el empleado declara la deduccion).
            Cantidad(LegalParameterCodes.WithholdingHousingInterestCapUvt, "Tope mensual de intereses de vivienda (UVT)", 100m, FuenteArt387 + " inc. 1"),
            Cantidad(LegalParameterCodes.WithholdingPrepaidHealthCapUvt, "Tope mensual de medicina prepagada (UVT)", 16m, FuenteArt387 + " inc. 2 lit. a"),
            Porcentaje(LegalParameterCodes.WithholdingDependentsPct, "Deduccion por dependientes sobre el ingreso bruto", 10m, FuenteArt387 + " inc. 2 lit. b"),
            Cantidad(LegalParameterCodes.WithholdingDependentsCapUvt, "Tope mensual de la deduccion por dependientes (UVT)", 32m, FuenteArt387 + " inc. 2 lit. b"),
            Porcentaje(LegalParameterCodes.WithholdingVoluntarySavingsPct, "Renta exenta por aportes voluntarios (AFC y pension voluntaria)", 30m, FuenteAfcAvp),
            Cantidad(LegalParameterCodes.WithholdingVoluntarySavingsCapUvt, "Tope mensual de la renta exenta por aportes voluntarios (UVT)", 316.67m, FuenteAfcAvp),
            Monto(LegalParameterCodes.WithholdingRoundingMultiple, "Multiplo de aproximacion de la retencion en la fuente", 1_000m, FuenteRedondeoRetencion),

            // ------------------------------------------------ feature 010: prestaciones sociales --
            Cantidad("PRIMA_DIAS_ANIO", "Días de prima de servicios por año (15 por semestre)", 30m, FuentePrima),
            FechaDelAño("PRIMA_FECHA_LIMITE_S1", "Fecha límite de pago de la prima del primer semestre (MMDD)", 6, 30, FuentePrima),
            FechaDelAño("PRIMA_FECHA_LIMITE_S2", "Fecha límite de pago de la prima del segundo semestre (MMDD)", 12, 20, FuentePrima),
            Cantidad("CESANTIAS_DIAS_ANIO", "Días de cesantías por año", 30m, FuenteCesantias),
            Cantidad("CESANTIAS_VENTANA_ESTABILIDAD_MESES", "Meses sin cambio de salario para liquidar cesantías con el último", 3m, "CST art. 253 (Decreto 2351 de 1965 art. 17)"),
            FechaDelAño("CESANTIAS_FECHA_LIMITE_CONSIGNACION", "Fecha límite de consignación de cesantías al fondo (MMDD)", 2, 14, "Ley 50 de 1990 art. 99 num. 3"),
            Porcentaje("INT_CESANTIAS_PCT", "Intereses anuales a las cesantías", 12m, FuenteIntereses),
            FechaDelAño("INT_CESANTIAS_FECHA_LIMITE", "Fecha límite de pago de los intereses a las cesantías (MMDD)", 1, 31, "Ley 52 de 1975 art. 1; Decreto 116 de 1976 art. 2"),
            Cantidad("VACACIONES_DIAS_ANIO", "Días hábiles de vacaciones por año", 15m, FuenteVacaciones),
            Vigente(Porcentaje("VACACIONES_COMPENSABLE_PCT", "Máximo de vacaciones compensables en dinero durante el contrato", 50m, "CST art. 189 num. 1 (Ley 1429 de 2010 art. 20)"), Utc(2010, 12, 29)),

            // ------------------------------------------------------------ feature 010: retiro --
            Cantidad("INDEMNIZACION_UMBRAL_SMMLV", "Salario (en SMMLV) que separa las dos tablas de indemnización", 10m, FuenteIndemnizacion),
            Cantidad("INDEMNIZACION_OBRA_MINIMO_DIAS", "Mínimo de días de indemnización en contrato a término fijo o por obra", 15m, FuenteIndemnizacion),
            Cantidad("SANCION_MORA_ART65_TOPE_MESES", "Meses máximos de la sanción moratoria por no pago de la liquidación (informativo)", 24m, "CST art. 65 (Ley 789 de 2002 art. 29)"),
            Cantidad("SALARIO_INTEGRAL_MINIMO_SMMLV", "Salario integral mínimo (en SMMLV, sin el factor prestacional)", 10m, FuenteSalarioIntegral),
            Porcentaje("SALARIO_INTEGRAL_FACTOR_PCT", "Factor prestacional mínimo del salario integral", 30m, FuenteSalarioIntegral),
            Vigente(Porcentaje("APRENDIZ_LECTIVA_APOYO_PCT", "Apoyo de sostenimiento del aprendiz en etapa lectiva (% del SMMLV)", 75m, FuenteAprendiz), Utc(2025, 6, 25)),
            Vigente(Porcentaje("APRENDIZ_PRACTICA_APOYO_PCT", "Apoyo de sostenimiento del aprendiz en etapa práctica (% del SMMLV)", 100m, FuenteAprendiz), Utc(2025, 6, 25)),
            // Sí/no como 1/0: la Ley 2466 no exige parafiscales sobre el apoyo de la etapa práctica y el
            // dueño decidió no aportarlos por defecto; la cooperativa que sí aporte lo cambia aquí.
            Cantidad("APORTES_APRENDIZ_PRACTICA_PARAFISCALES", "Aporta parafiscales sobre el apoyo del aprendiz en práctica (1 = sí, 0 = no)", 0m, FuenteAprendiz),

            // -------------------------------------------- feature 010: retención (R5, R8) --
            Vigente(Cantidad("RETEFTE_RENTA_EXENTA_TOPE_ANUAL_UVT", "Tope anual de la renta exenta laboral (UVT)", 790m, FuenteRentaExenta), Utc(2023, 1, 1)),
            Vigente(Cantidad("RETEFTE_DEDUCCIONES_TOPE_ANUAL_UVT", "Tope anual de deducciones y rentas exentas (UVT)", 1_340m, FuenteDeducciones), Utc(2023, 12, 22)),
            Cantidad("RETEFTE_P2_DIVISOR", "Divisor del ingreso de los doce meses en el procedimiento 2", 13m, "ET art. 386"),
            Cantidad("CESANTIAS_EXENCION_TOPE_UVT", "Ingreso mensual promedio (UVT, últimos seis meses) hasta el cual las cesantías son exentas", 350m, FuenteCesantiasGravadas),
            Porcentaje("INDEMNIZACION_RETEFTE_PCT", "Retención sobre indemnizaciones y bonificaciones por retiro", 20m, FuenteIndemnizacionRetefte),
            Cantidad("INDEMNIZACION_RETEFTE_TOPE_UVT", "Ingreso mensual (UVT) desde el cual la indemnización se retiene", 204m, FuenteIndemnizacionRetefte),

            // ------------------------------------------------------------- feature 010: PILA --
            Cantidad("FSP_UMBRAL_SMMLV", "IBC (en SMMLV) desde el cual se aporta al fondo de solidaridad pensional", 4m, "Ley 100 de 1993 art. 27; Ley 797 de 2003 art. 8"),
            Cantidad("IBC_MINIMO_SMMLV", "IBC mínimo (en SMMLV, proporcional a los días cotizados)", 1m, "Ley 797 de 2003 art. 5; AT2 v30 «01 - Dependiente»"),
            Cantidad("PILA_IBC_REDONDEO", "Múltiplo al que se aproxima el IBC por encima (pesos)", 1m, FuentePilaRedondeo),
            Cantidad("PILA_APORTE_REDONDEO_MULTIPLO", "Múltiplo al que se aproxima cada aporte por encima (pesos)", 100m, FuentePilaRedondeo),

            // ------------------------------------------------------------- feature 010: DIAN --
            Cantidad("DIAN_PLAZO_TRANSMISION_DIAS", "Días del mes siguiente para transmitir la nómina electrónica", 10m, "Resolución DIAN 000227 de 2025 art. 1.5.3.4.1.1"),
        };

        // Fondo de solidaridad pensional: tramos en múltiplos de SMMLV → porcentaje. Es la tabla
        // de la Ley 797; la de la Ley 2381 rige desde el 2027-04-01 y entra por Revisiones().
        lista.Add(Tabla(LegalParameterCodes.SolidarityFundTable, "Fondo de solidaridad pensional (tramos en SMMLV)", FuenteFspLey797,
            unitCode: LegalParameterCodes.Smmlv, marginal: false,
        [
            (4m, 16m, 1.0m, 0m),
            (16m, 17m, 1.2m, 0m),
            (17m, 18m, 1.4m, 0m),
            (18m, 19m, 1.6m, 0m),
            (19m, 20m, 1.8m, 0m),
            (20m, null, 2.0m, 0m),
        ]));

        // Retención en la fuente art. 383 E.T.: tramos en UVT → tarifa marginal sobre el
        // exceso del tramo más UVT fijas.
        lista.Add(Tabla(LegalParameterCodes.WithholdingTableUvt, "Retención en la fuente por salarios (tramos en UVT)", FuenteTabla383,
            unitCode: LegalParameterCodes.Uvt, marginal: true,
        [
            (0m, 95m, 0m, 0m),
            (95m, 150m, 19m, 0m),
            (150m, 360m, 28m, 10m),
            (360m, 640m, 33m, 69m),
            (640m, 945m, 35m, 162m),
            (945m, 2300m, 37m, 268m),
            (2300m, null, 39m, 770m),
        ]));

        // Indemnización por despido sin justa causa (CST art. 64) en contrato indefinido: dos
        // valores por tramo (D-08). FixedValue = días del primer año; Rate = días por cada año
        // adicional (proporcional por fracción). El tramo es el salario en SMMLV, no marginal.
        lista.Add(Vigente(Tabla("INDEMNIZACION_TABLA", "Indemnización art. 64 CST: días del 1.er año y por año adicional (tramos de salario en SMMLV)", FuenteIndemnizacion,
            unitCode: LegalParameterCodes.Smmlv, marginal: false,
        [
            (0m, 10m, 20m, 30m),
            (10m, null, 15m, 20m),
        ]), Utc(2002, 12, 27)));

        // Cesantías e intereses gravados (ET art. 206 num. 4): el tramo es el ingreso mensual
        // promedio de los últimos seis meses en UVT y Rate es el porcentaje NO gravado (exento).
        // Los tramos son «desde inclusive, hasta exclusive», como todas las tablas del motor: la
        // norma dice «hasta 350 → 100 %», así que un promedio exactamente de 350,0000 UVT cae en
        // el segundo tramo; es un límite continuo que en pesos no se da.
        lista.Add(Tabla("CESANTIAS_GRAVADA_TABLA_UVT", "Cesantías e intereses: porcentaje no gravado según ingreso mensual promedio (UVT)", FuenteCesantiasGravadas,
            unitCode: LegalParameterCodes.Uvt, marginal: false,
        [
            (0m, 350m, 100m, 0m),
            (350m, 410m, 90m, 0m),
            (410m, 470m, 80m, 0m),
            (470m, 530m, 60m, 0m),
            (530m, 590m, 40m, 0m),
            (590m, 650m, 20m, 0m),
            (650m, null, 0m, 0m),
        ]));

        // Plazo de pago de la PILA según los dos últimos dígitos del NIT del aportante
        // (Decreto 780 de 2016 art. 3.2.2.1, Decreto 923 de 2017): la unidad es nula (el tramo es el
        // número 00..99, no pesos) y FixedValue es el día hábil del mes. Sólo alimenta el aviso.
        lista.Add(Tabla("PILA_PLAZO_PAGO_POR_NIT", "Plazo de pago de la PILA: día hábil según los dos últimos dígitos del NIT", "Decreto 780 de 2016 art. 3.2.2.1 (Decreto 923 de 2017)",
            unitCode: null, marginal: false,
        [
            (0m, 8m, 0m, 2m),
            (8m, 15m, 0m, 3m),
            (15m, 22m, 0m, 4m),
            (22m, 29m, 0m, 5m),
            (29m, 36m, 0m, 6m),
            (36m, 43m, 0m, 7m),
            (43m, 50m, 0m, 8m),
            (50m, 57m, 0m, 9m),
            (57m, 64m, 0m, 10m),
            (64m, 70m, 0m, 11m),
            (70m, 76m, 0m, 12m),
            (76m, 82m, 0m, 13m),
            (82m, 88m, 0m, 14m),
            (88m, 94m, 0m, 15m),
            (94m, null, 0m, 16m),
        ]));

        return lista;
    }

    private static PayrollLegalParameter Monto(string code, string name, decimal value, string source) =>
        Nuevo(code, name, LegalParameterKind.Amount, value, source);

    private static PayrollLegalParameter Porcentaje(string code, string name, decimal value, string source) =>
        Nuevo(code, name, LegalParameterKind.Percent, value, source);

    /// <summary>Cantidades y topes expresados en unidades (SMMLV, UVT, días, horas) se guardan como Amount.</summary>
    private static PayrollLegalParameter Cantidad(string code, string name, decimal value, string source) =>
        Nuevo(code, name, LegalParameterKind.Amount, value, source);

    /// <summary>Fecha del año como <c>MMDD</c> (D-07): 30 de junio → 630, 20 de diciembre → 1220.</summary>
    private static PayrollLegalParameter FechaDelAño(string code, string name, int mes, int día, string source) =>
        Nuevo(code, name, LegalParameterKind.DateInYear, mes * 100 + día, source);

    private static PayrollLegalParameter Nuevo(string code, string name, LegalParameterKind kind, decimal? value, string source) => new()
    {
        Code = code,
        Name = name,
        Kind = kind,
        Value = value,
        ValidFrom = Vigencia2026,
        Source = source,
        CreatedBy = SeedContext.ParametricCreatedBy,
    };

    private static PayrollLegalParameter Tabla(string code, string name, string source, string? unitCode, bool marginal,
        (decimal From, decimal? To, decimal Rate, decimal Fixed)[] tramos)
    {
        var p = Nuevo(code, name, LegalParameterKind.RangeTable, null, source);
        p.RangeUnitParameterCode = unitCode;
        p.RangeIsMarginal = marginal;
        var order = 0;
        foreach (var (from, to, rate, fixedValue) in tramos)
        {
            p.Ranges.Add(new PayrollLegalParameterRange
            {
                FromValue = from,
                ToValue = to,
                Rate = rate,
                FixedValue = fixedValue,
                Order = ++order,
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
        }
        return p;
    }

    /// <summary>
    /// Vigencias posteriores a la base del año, para lo que la ley cambia a mitad de año. Se
    /// aplican también sobre cooperativas que ya tenían la semilla: se inserta la versión nueva
    /// y se cierra la anterior el día antes, sólo si esa anterior sigue abierta y nadie la
    /// tocó (una vigencia registrada a mano después de la base se respeta y no se pisa).
    /// </summary>
    public static IReadOnlyList<PayrollLegalParameter> Revisiones() =>
    [
        // Ley 2381 de 2024 (reforma pensional), vigente desde el 2027-04-01 por la Sentencia C-264 del
        // 02-09-2026: la tabla del fondo de solidaridad cambia para quien NO esté en régimen de
        // transición. La tabla de la Ley 797 se cierra el 2027-03-31; la bandera de transición de
        // la ficha decide a quién se le aplica cada una (feature 010, R4/R9).
        Vigente(Tabla(LegalParameterCodes.SolidarityFundTable, "Fondo de solidaridad pensional, Ley 2381 de 2024 (tramos en SMMLV)", FuenteFspLey2381,
            unitCode: LegalParameterCodes.Smmlv, marginal: false,
        [
            (4m, 7m, 1.5m, 0m),
            (7m, 11m, 1.8m, 0m),
            (11m, 19m, 2.5m, 0m),
            (19m, 20m, 2.8m, 0m),
            (20m, null, 3.0m, 0m),
        ]), Utc(2027, 4, 1)),

        // Los valores 2027 de SMMLV, AUX_TRANSPORTE y UVT se decretan a fines de diciembre de 2026 y
        // entran aquí (o por la pantalla) antes de liquidar los intereses de enero.
    ];

    private static PayrollLegalParameter Vigente(PayrollLegalParameter p, DateTime desde) { p.ValidFrom = desde; return p; }

    /// <summary>Una fila sembrada que nadie editó: la creó la semilla y, si algo la actualizó, fue la semilla misma.</summary>
    private static bool Intacta(PayrollLegalParameter p) =>
        p.CreatedBy == SeedContext.ParametricCreatedBy
        && (p.UpdatedBy is null || p.UpdatedBy == SeedContext.ParametricCreatedBy);

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, ct);

    /// <summary>
    /// La semilla sobre cualquier contexto de la cooperativa. Separada de <see cref="SeedAsync"/>
    /// para poder probarla con InMemory: <see cref="SeedContext"/> exige el
    /// <c>ApplicationDbContext</c> concreto y las pruebas de idempotencia trabajan sobre el de
    /// prueba.
    /// </summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existentes = await db.PayrollLegalParameters.IgnoreQueryFilters().ToListAsync(ct);
        var porClave = existentes.ToDictionary(e => Clave(e.Code, e.ValidFrom), StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        foreach (var parametro in Catalogo())
        {
            if (porClave.TryGetValue(Clave(parametro.Code, parametro.ValidFrom), out var existente))
            {
                PrecisarFuente(existente, parametro.Source);
                continue;
            }
            db.PayrollLegalParameters.Add(parametro);
            porClave[Clave(parametro.Code, parametro.ValidFrom)] = parametro;
            inserted++;
        }
        // La base tiene que estar en la base antes de mirar qué versión cerrar: en una cooperativa
        // nueva se insertan ambas en la misma pasada.
        if (inserted > 0) await db.SaveChangesAsync(ct);
        foreach (var revision in Revisiones())
        {
            if (porClave.ContainsKey(Clave(revision.Code, revision.ValidFrom))) continue;
            var versiones = await db.PayrollLegalParameters.IgnoreQueryFilters()
                .Where(p => p.Code == revision.Code).OrderByDescending(p => p.ValidFrom).ToListAsync(ct);
            var ultima = versiones.FirstOrDefault();
            // Alguien registró a mano una vigencia igual o posterior: la ley ya está reflejada, no se pisa.
            if (ultima is not null && ultima.ValidFrom >= revision.ValidFrom) continue;
            // Se cierra la anterior sólo si sigue abierta y nadie la tocó; una fila que la cooperativa
            // editó (o que ya cerró a mano) queda como está. Si quedara abierta, manda la vigencia más
            // reciente que aplique, así que la nueva rige igual desde su fecha.
            if (ultima is not null && ultima.ValidTo is null && Intacta(ultima))
            {
                ultima.ValidTo = revision.ValidFrom.AddDays(-1);
                ultima.UpdatedBy = SeedContext.ParametricCreatedBy;
                ultima.UpdatedAt = DateTime.UtcNow;
            }
            db.PayrollLegalParameters.Add(revision);
            inserted++;
        }

        await db.SaveChangesAsync(ct);
        return inserted;
    }

    private static string Clave(string code, DateTime validFrom) => $"{code}|{validFrom:yyyy-MM-dd}";

    /// <summary>
    /// Pone la norma exacta en una fila que aún lleva el texto genérico de la semilla 2026 (o
    /// ninguno). Un <c>Source</c> escrito por una persona no se toca: es su trazabilidad, no la nuestra.
    /// </summary>
    private static void PrecisarFuente(PayrollLegalParameter existente, string? fuente)
    {
        if (string.IsNullOrWhiteSpace(fuente) || existente.Source == fuente) return;
        var esGenerica = string.IsNullOrWhiteSpace(existente.Source) || FuentesGenericasAnteriores.Contains(existente.Source);
        if (!esGenerica) return;
        existente.Source = fuente;
        existente.UpdatedBy = SeedContext.ParametricCreatedBy;
        existente.UpdatedAt = DateTime.UtcNow;
    }
}
