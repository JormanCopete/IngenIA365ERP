using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 005 (FR-010, FR-011): los parámetros legales del año con su vigencia y sus
/// tablas por rangos. El motor sólo conoce los códigos de
/// <see cref="LegalParameterCodes"/>; los valores viven aquí como DATOS de la semilla
/// del año y la cooperativa los mantiene desde la pantalla de parámetros legales.
///
/// <para>
/// Idempotente por <c>(Code, ValidFrom)</c>: nunca actualiza lo existente. Una
/// vigencia nueva (el año siguiente) es una fila nueva, no una edición.
/// </para>
///
/// <para>
/// <b>Los valores son la referencia de 2026 y deben verificarse contra la norma
/// vigente antes de la primera nómina real</b> (runbook
/// <c>docs/operaciones/nomina-primer-periodo.md</c>). Si un valor está mal, se corrige
/// aquí para las cooperativas nuevas y en la pantalla para las existentes.
/// </para>
/// </summary>
public sealed class PayrollLegalParametersSeeder : IDataSeeder
{
    public int Order => 71;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    private static readonly DateTime Vigencia2026 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string FuenteSalario = "Decreto de salario mínimo y auxilio de transporte 2026";
    private const string FuenteUvt = "Resolución DIAN que fija la UVT 2026";
    private const string FuenteSeguridadSocial = "Ley 100 de 1993, Ley 797 de 2003 y Decreto 1072 de 2015";
    private const string FuenteParafiscales = "Ley 21 de 1982, Ley 89 de 1988, Ley 1607 de 2012 art. 25";
    private const string FuenteRetencion = "Estatuto Tributario arts. 383, 387 y 206 num. 10";
    private const string FuenteLaboral = "Código Sustantivo del Trabajo y Ley 50 de 1990";

    public static IReadOnlyList<PayrollLegalParameter> Catalogo()
    {
        var lista = new List<PayrollLegalParameter>
        {
            Monto(LegalParameterCodes.Smmlv, "Salario mínimo mensual legal vigente", 1_750_905m, FuenteSalario),
            Monto(LegalParameterCodes.TransportAllowance, "Auxilio de transporte mensual", 249_095m, FuenteSalario),
            Cantidad(LegalParameterCodes.TransportAllowanceCapSmmlv, "Tope de salario para auxilio de transporte (en SMMLV)", 2m, FuenteSalario),
            Monto(LegalParameterCodes.Uvt, "Unidad de Valor Tributario", 52_374m, FuenteUvt),

            Porcentaje(LegalParameterCodes.HealthEmployeePct, "Salud a cargo del empleado", 4m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.PensionEmployeePct, "Pensión a cargo del empleado", 4m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.HealthEmployerPct, "Salud a cargo del empleador", 8.5m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.PensionEmployerPct, "Pensión a cargo del empleador", 12m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.HealthApprenticePct, "Salud de aprendices y pasantes (total, a cargo del patrocinador)", 12.5m, "Ley 789 de 2002 art. 30 y Decreto 933 de 2003"),
            Porcentaje(LegalParameterCodes.WorkRiskClass1Pct, "ARL clase de riesgo I", 0.522m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.WorkRiskClass2Pct, "ARL clase de riesgo II", 1.044m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.WorkRiskClass3Pct, "ARL clase de riesgo III", 2.436m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.WorkRiskClass4Pct, "ARL clase de riesgo IV", 4.350m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.WorkRiskClass5Pct, "ARL clase de riesgo V", 6.960m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.SenaPct, "SENA", 2m, FuenteParafiscales),
            Porcentaje(LegalParameterCodes.IcbfPct, "ICBF", 3m, FuenteParafiscales),
            Porcentaje(LegalParameterCodes.FamilyCompensationPct, "Caja de compensación familiar", 4m, FuenteParafiscales),
            Cantidad(LegalParameterCodes.PayrollExemptionThresholdSmmlv, "Tope (SMMLV) de exoneración de salud, SENA e ICBF del empleador", 10m, FuenteParafiscales),

            Porcentaje(LegalParameterCodes.SeveranceProvisionPct, "Provisión de cesantías", 8.33m, FuenteLaboral),
            Porcentaje(LegalParameterCodes.SeveranceInterestProvisionPct, "Provisión de intereses a las cesantías", 1m, FuenteLaboral),
            Porcentaje(LegalParameterCodes.ServiceBonusProvisionPct, "Provisión de prima de servicios", 8.33m, FuenteLaboral),
            Porcentaje(LegalParameterCodes.VacationProvisionPct, "Provisión de vacaciones", 4.17m, FuenteLaboral),

            Porcentaje(LegalParameterCodes.WithholdingExemptIncomePct, "Renta exenta laboral", 25m, FuenteRetencion),
            Cantidad(LegalParameterCodes.WithholdingExemptIncomeCapUvt, "Tope mensual de la renta exenta (UVT)", 65.83m, FuenteRetencion),
            Porcentaje(LegalParameterCodes.WithholdingDeductionsCapPct, "Tope de deducciones y rentas exentas sobre el ingreso neto", 40m, FuenteRetencion),
            Cantidad(LegalParameterCodes.WithholdingDeductionsCapUvt, "Tope mensual de deducciones y rentas exentas (UVT)", 111.67m, FuenteRetencion),

            Porcentaje(LegalParameterCodes.MaxDeductionOfSalaryPct, "Máximo de deducciones sobre el salario", 50m, FuenteLaboral),
            Porcentaje(LegalParameterCodes.IntegralSalaryBasePct, "Base de aportes del salario integral", 70m, FuenteLaboral),
            Cantidad(LegalParameterCodes.ContributionBaseCapSmmlv, "Tope del IBC (en SMMLV)", 25m, FuenteSeguridadSocial),
            Cantidad(LegalParameterCodes.SickLeaveEmployerDays, "Días de incapacidad a cargo del empleador", 2m, FuenteSeguridadSocial),
            Porcentaje(LegalParameterCodes.SickLeaveEmployerPct, "Porcentaje pagado en incapacidad general", 66.67m, FuenteSeguridadSocial),
            Cantidad(LegalParameterCodes.HoursPerMonth, "Horas del mes para el valor hora", 240m, FuenteLaboral),

            // Depuracion de la base de retencion (solo si el empleado declara la deduccion).
            Cantidad(LegalParameterCodes.WithholdingHousingInterestCapUvt, "Tope mensual de intereses de vivienda (UVT)", 100m, FuenteRetencion),
            Cantidad(LegalParameterCodes.WithholdingPrepaidHealthCapUvt, "Tope mensual de medicina prepagada (UVT)", 16m, FuenteRetencion),
            Porcentaje(LegalParameterCodes.WithholdingDependentsPct, "Deduccion por dependientes sobre el ingreso bruto", 10m, FuenteRetencion),
            Cantidad(LegalParameterCodes.WithholdingDependentsCapUvt, "Tope mensual de la deduccion por dependientes (UVT)", 32m, FuenteRetencion),
            Porcentaje(LegalParameterCodes.WithholdingVoluntarySavingsPct, "Renta exenta por aportes voluntarios (AFC y pension voluntaria)", 30m, FuenteRetencion),
            Cantidad(LegalParameterCodes.WithholdingVoluntarySavingsCapUvt, "Tope mensual de la renta exenta por aportes voluntarios (UVT)", 316.67m, FuenteRetencion),
            Monto(LegalParameterCodes.WithholdingRoundingMultiple, "Multiplo de aproximacion de la retencion en la fuente", 1_000m, FuenteRetencion),
        };

        // Fondo de solidaridad pensional: tramos en múltiplos de SMMLV → porcentaje.
        lista.Add(Tabla(LegalParameterCodes.SolidarityFundTable, "Fondo de solidaridad pensional (tramos en SMMLV)", FuenteSeguridadSocial,
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
        lista.Add(Tabla(LegalParameterCodes.WithholdingTableUvt, "Retención en la fuente por salarios (tramos en UVT)", FuenteRetencion,
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

        return lista;
    }

    private static PayrollLegalParameter Monto(string code, string name, decimal value, string source) =>
        Nuevo(code, name, LegalParameterKind.Amount, value, source);

    private static PayrollLegalParameter Porcentaje(string code, string name, decimal value, string source) =>
        Nuevo(code, name, LegalParameterKind.Percent, value, source);

    /// <summary>Cantidades y topes expresados en unidades (SMMLV, UVT, días, horas) se guardan como Amount.</summary>
    private static PayrollLegalParameter Cantidad(string code, string name, decimal value, string source) =>
        Nuevo(code, name, LegalParameterKind.Amount, value, source);

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

    private static PayrollLegalParameter Tabla(string code, string name, string source, string unitCode, bool marginal,
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

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var existentes = await db.PayrollLegalParameters.IgnoreQueryFilters()
            .Select(p => new { p.Code, p.ValidFrom })
            .ToListAsync(ct);
        var claves = existentes.Select(e => $"{e.Code}|{e.ValidFrom:yyyy-MM-dd}").ToHashSet(StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        foreach (var parametro in Catalogo())
        {
            if (claves.Contains($"{parametro.Code}|{parametro.ValidFrom:yyyy-MM-dd}")) continue;
            db.PayrollLegalParameters.Add(parametro);
            inserted++;
        }
        if (inserted > 0) await db.SaveChangesAsync(ct);
        return inserted;
    }
}
