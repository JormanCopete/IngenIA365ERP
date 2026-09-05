using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 005 (FR-038): la semilla curada de conceptos legales colombianos estándar,
/// ya parametrizados con su forma de cálculo, sus bases y su naturaleza. La
/// cooperativa los ajusta o desactiva pero no los borra, y crea los propios. El
/// catálogo heredado (<c>PAY_PayrollConcepts</c>) no participa: se traduce a mano.
///
/// <para>
/// Idempotente por <c>(Code, ValidFrom)</c>: nunca actualiza lo existente (FR-016 del
/// framework de semillas). Los porcentajes y topes NO están aquí: viven en
/// <see cref="PayrollLegalParametersSeeder"/> como parámetros con vigencia, y cada
/// concepto los referencia por código. Los únicos números de esta semilla son
/// factores de recargo (1.25, 1.75…), que son reglas del concepto, no valores legales.
/// </para>
/// </summary>
public sealed class PayrollConceptDefinitionsSeeder : IDataSeeder
{
    public int Order => 70;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    private static readonly DateTime Vigencia = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const int Todas = 0;
    private static int Clases(params EmployeeClass[] clases) => clases.Aggregate(0, (m, c) => m | (1 << (int)c));
    private static readonly int SinAprendicesNiPasantes = Clases(EmployeeClass.Standard, EmployeeClass.IntegralSalary, EmployeeClass.Pensioner);
    private static readonly int ConPrestaciones = Clases(EmployeeClass.Standard, EmployeeClass.Pensioner);
    private static readonly int CotizanPension = Clases(EmployeeClass.Standard, EmployeeClass.IntegralSalary);
    private static readonly int ConAuxilio = Clases(EmployeeClass.Standard, EmployeeClass.Apprentice, EmployeeClass.Intern, EmployeeClass.Pensioner);

    internal static IReadOnlyList<PayrollConceptDefinition> Catalogo()
    {
        var lista = new List<PayrollConceptDefinition>();

        // ---------------------------------------------------------- devengos --
        lista.Add(Def(WellKnownConceptCodes.BasicSalary, "Salario básico", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.IsAutomatic = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
            d.IsBenefitRelated = true;
        }));
        lista.Add(Def(WellKnownConceptCodes.TransportAllowance, "Auxilio de transporte", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.AmountParameterCode = LegalParameterCodes.TransportAllowance; d.ProrateByDays = true; d.IsAutomatic = true;
            d.AffectsBenefitsBase = true; d.IsBenefitRelated = true; d.ApplicableClasses = ConAuxilio;
        }));

        foreach (var (code, name, factor) in new (string, string, decimal)[]
        {
            ("HEX_DIURNA", "Hora extra diurna", 1.25m),
            ("HEX_NOCTURNA", "Hora extra nocturna", 1.75m),
            ("HEX_DOM_DIURNA", "Hora extra dominical o festiva diurna", 2.00m),
            ("HEX_DOM_NOCTURNA", "Hora extra dominical o festiva nocturna", 2.50m),
            ("RECARGO_NOCTURNO", "Recargo nocturno", 0.35m),
            ("RECARGO_DOMINICAL", "Recargo dominical o festivo", 0.75m),
        })
        {
            lista.Add(Def(code, name, ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
            {
                d.UnitKind = UnitKind.HourWithSurcharge; d.UnitFactor = factor; d.RequiresQuantity = true; d.AllowsRepeatInPeriod = true;
                d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
                d.IsBenefitRelated = true;
            }));
        }

        lista.Add(Def("COMISION", "Comisiones", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
        }));
        lista.Add(Def("BONIF_SALARIAL", "Bonificación salarial", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
        }));
        lista.Add(Def("BONIF_NO_SALARIAL", "Bonificación no salarial", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true; d.AffectsWithholdingBase = true;
        }));
        lista.Add(Def("AUX_NO_SALARIAL", "Auxilio no salarial", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true; d.AffectsWithholdingBase = true;
        }));
        lista.Add(Def("VIATICOS", "Viáticos no permanentes", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true;
        }));

        // ----------------------------------------- ausentismos y licencias --
        lista.Add(Def(WellKnownConceptCodes.GeneralSickLeave, "Incapacidad por enfermedad general", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.PercentParameterCode = LegalParameterCodes.SickLeaveEmployerPct;
            d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
            d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
        }));
        lista.Add(Def("INCAP_LABORAL", "Incapacidad por accidente o enfermedad laboral", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
            d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
        }));
        lista.Add(Def("LIC_MATERNIDAD", "Licencia de maternidad o paternidad", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.RequiresDates = true; d.ReducesWorkedDays = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
        }));
        lista.Add(Def("VACACIONES", "Vacaciones disfrutadas", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.RequiresDates = true; d.ReducesWorkedDays = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
        }));
        lista.Add(Def("LIC_REMUNERADA", "Licencia remunerada", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
        }));
        lista.Add(Def("LIC_NO_REMUNERADA", "Licencia no remunerada", ConceptNature.Informative, CalculationKind.FixedAmount, d =>
        {
            d.FixedAmount = 0m; d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
        }));
        lista.Add(Def("SUSPENSION", "Suspensión del contrato", ConceptNature.Informative, CalculationKind.FixedAmount, d =>
        {
            d.FixedAmount = 0m; d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
        }));

        // ---------------------------------------------- deducciones de ley --
        lista.Add(Def(WellKnownConceptCodes.HealthEmployee, "Salud (aporte del empleado)", ConceptNature.Deduction, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.HealthEmployeePct; d.IsAutomatic = true;
        }));
        lista.Add(Def(WellKnownConceptCodes.PensionEmployee, "Pensión (aporte del empleado)", ConceptNature.Deduction, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.PensionEmployeePct; d.IsAutomatic = true;
            d.ApplicableClasses = CotizanPension;
        }));
        lista.Add(Def(WellKnownConceptCodes.SolidarityFund, "Fondo de solidaridad pensional", ConceptNature.Deduction, CalculationKind.RangeTable, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.TableParameterCode = LegalParameterCodes.SolidarityFundTable; d.IsAutomatic = true;
            d.ApplicableClasses = CotizanPension;
        }));
        lista.Add(Def(WellKnownConceptCodes.Withholding, "Retención en la fuente", ConceptNature.Deduction, CalculationKind.RangeTable, d =>
        {
            d.BaseKind = CalculationBase.WithholdingBase; d.TableParameterCode = LegalParameterCodes.WithholdingTableUvt; d.IsAutomatic = true;
        }));

        // ------------------------------------------- deducciones autorizadas --
        foreach (var (code, name) in new (string, string)[]
        {
            ("LIBRANZA", "Libranza"),
            ("PRESTAMO_EMP", "Préstamo de la cooperativa al empleado"),
            ("EMBARGO", "Embargo judicial"),
            ("OTROS_DESC", "Otros descuentos autorizados"),
        })
        {
            lista.Add(Def(code, name, ConceptNature.Deduction, CalculationKind.FixedAmount, d =>
            {
                d.RequiresAmount = true; d.AllowsRepeatInPeriod = true;
            }));
        }
        lista.Add(Def(WellKnownConceptCodes.LoanDeduction, "Descuento por nómina de Cartera", ConceptNature.Deduction, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true;
        }));
        lista.Add(Def(WellKnownConceptCodes.RoundingAdjustment, "Ajuste por redondeo", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.FixedAmount = 0m; d.IsAutomatic = true;
        }));

        // -------------------------------------------- aportes del empleador --
        lista.Add(Def(WellKnownConceptCodes.HealthEmployer, "Salud (aporte del empleador)", ConceptNature.EmployerContribution, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.HealthEmployerPct; d.IsAutomatic = true;
        }));
        lista.Add(Def(WellKnownConceptCodes.PensionEmployer, "Pensión (aporte del empleador)", ConceptNature.EmployerContribution, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.PensionEmployerPct; d.IsAutomatic = true;
            d.ApplicableClasses = CotizanPension;
        }));
        lista.Add(Def(WellKnownConceptCodes.WorkRisk, "Riesgos laborales (ARL)", ConceptNature.EmployerContribution, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase;
            d.PercentParameterCode = "ARL_CLASE_" + WellKnownConceptCodes.WorkRiskClassPlaceholder + "_PCT";
            d.IsAutomatic = true;
        }));
        lista.Add(Def(WellKnownConceptCodes.Sena, "SENA", ConceptNature.EmployerContribution, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.SenaPct; d.IsAutomatic = true;
            d.ApplicableClasses = SinAprendicesNiPasantes;
        }));
        lista.Add(Def(WellKnownConceptCodes.Icbf, "ICBF", ConceptNature.EmployerContribution, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.IcbfPct; d.IsAutomatic = true;
            d.ApplicableClasses = SinAprendicesNiPasantes;
        }));
        lista.Add(Def(WellKnownConceptCodes.FamilyCompensation, "Caja de compensación familiar", ConceptNature.EmployerContribution, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.FamilyCompensationPct; d.IsAutomatic = true;
            d.ApplicableClasses = SinAprendicesNiPasantes;
        }));

        // ----------------------------------------------------- provisiones --
        lista.Add(Def("PROV_CESANTIAS", "Provisión de cesantías", ConceptNature.Provision, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.BenefitsBase; d.PercentParameterCode = LegalParameterCodes.SeveranceProvisionPct; d.IsAutomatic = true;
            d.ApplicableClasses = ConPrestaciones;
        }));
        lista.Add(Def("PROV_INT_CESANTIAS", "Provisión de intereses a las cesantías", ConceptNature.Provision, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.BenefitsBase; d.PercentParameterCode = LegalParameterCodes.SeveranceInterestProvisionPct; d.IsAutomatic = true;
            d.ApplicableClasses = ConPrestaciones;
        }));
        lista.Add(Def("PROV_PRIMA", "Provisión de prima de servicios", ConceptNature.Provision, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.BenefitsBase; d.PercentParameterCode = LegalParameterCodes.ServiceBonusProvisionPct; d.IsAutomatic = true;
            d.ApplicableClasses = ConPrestaciones;
        }));
        lista.Add(Def("PROV_VACACIONES", "Provisión de vacaciones", ConceptNature.Provision, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.SalaryEarnings; d.PercentParameterCode = LegalParameterCodes.VacationProvisionPct; d.IsAutomatic = true;
            d.ApplicableClasses = ConPrestaciones;
        }));

        return lista;
    }

    private static PayrollConceptDefinition Def(string code, string name, ConceptNature nature, CalculationKind kind, Action<PayrollConceptDefinition> configurar)
    {
        var d = new PayrollConceptDefinition
        {
            Code = code,
            Name = name,
            Nature = nature,
            CalculationKind = kind,
            ApplicableClasses = Todas,
            Origin = ConceptOrigin.Seed,
            ValidFrom = Vigencia,
            IsActive = true,
            CreatedBy = SeedContext.ParametricCreatedBy,
        };
        configurar(d);
        return d;
    }

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var existentes = await db.PayrollConceptDefinitions.IgnoreQueryFilters()
            .Select(c => new { c.Code, c.ValidFrom })
            .ToListAsync(ct);
        var claves = existentes.Select(e => $"{e.Code}|{e.ValidFrom:yyyy-MM-dd}").ToHashSet(StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        foreach (var def in Catalogo())
        {
            if (claves.Contains($"{def.Code}|{def.ValidFrom:yyyy-MM-dd}")) continue;
            db.PayrollConceptDefinitions.Add(def);
            inserted++;
        }
        if (inserted > 0) await db.SaveChangesAsync(ct);
        return inserted;
    }
}
