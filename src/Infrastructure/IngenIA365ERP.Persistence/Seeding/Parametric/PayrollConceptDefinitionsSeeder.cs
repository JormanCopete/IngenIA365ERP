using IngenIA365ERP.Application.Common.Interfaces;
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
/// Idempotente por <c>(Code, ValidFrom)</c>: nunca reemplaza una versión existente (FR-016 del
/// framework de semillas). Los porcentajes y topes NO están aquí: viven en
/// <see cref="PayrollLegalParametersSeeder"/> como parámetros con vigencia, y cada
/// concepto los referencia por código. Los únicos números de esta semilla son
/// factores de recargo (1.25, 1.75…), que son reglas del concepto, no valores legales.
/// </para>
///
/// <para>
/// Feature 010 (data-model §1.7): entran los conceptos de las liquidaciones especiales (prima,
/// cesantías e intereses, vacaciones, indemnización, ajustes de provisión y sus retenciones),
/// automáticos porque los pone el motor de liquidaciones y no una novedad, y que la nómina
/// ordinaria salta (<see cref="WellKnownConceptCodes.SettlementOnly"/>). Dos columnas nuevas,
/// <c>AffectsVacationBase</c> (CST art. 192) y <c>DianElement</c> (ruta en el XML de la DIAN), se
/// ponen <b>en su sitio</b> en las versiones ya sembradas: no alteran ningún valor calculado de
/// la nómina ordinaria, así que no exigen versión nueva; un concepto propio de la cooperativa
/// (<c>Custom</c>) no se toca.
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

    public static IReadOnlyList<PayrollConceptDefinition> Catalogo()
    {
        var lista = new List<PayrollConceptDefinition>();

        // ---------------------------------------------------------- devengos --
        lista.Add(Def(WellKnownConceptCodes.BasicSalary, "Salario básico", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.IsAutomatic = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
            d.IsBenefitRelated = true; d.AffectsVacationBase = true; d.DianElement = "Devengados/Basico";
        }));
        lista.Add(Def(WellKnownConceptCodes.TransportAllowance, "Auxilio de transporte", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.AmountParameterCode = LegalParameterCodes.TransportAllowance; d.ProrateByDays = true; d.IsAutomatic = true;
            // Prestacional y gravado para retencion; NO hace parte del IBC de aportes ni de la base de vacaciones.
            d.AffectsBenefitsBase = true; d.AffectsWithholdingBase = true; d.IsBenefitRelated = true; d.ApplicableClasses = ConAuxilio;
            d.DianElement = "Devengados/Transporte/AuxilioTransporte";
        }));

        // El recargo nocturno es salario ordinario y entra a la base de vacaciones (CST art. 192); las
        // extras y el trabajo en descanso obligatorio, no. La ruta DIAN es la del anexo técnico 3.1.
        foreach (var (code, name, factor, vacaciones, dian) in new (string, string, decimal, bool, string)[]
        {
            ("HEX_DIURNA", "Hora extra diurna", 1.25m, false, "Devengados/HorasExtras/HEDs/HED"),
            ("HEX_NOCTURNA", "Hora extra nocturna", 1.75m, false, "Devengados/HorasExtras/HENs/HEN"),
            // Ley 2466 de 2025 (reforma laboral): el recargo dominical/festivo sube de 75 % a 80 % desde el
            // 01/07/2025, 90 % desde el 01/07/2026 y 100 % desde el 01/07/2027 (ver Revisiones()). La extra
            // dominical es la extra ordinaria (1,25 / 1,75) más ese recargo.
            // Decisión del 2026-09-13: la base 2026 lleva ya el escalón de julio de 2026 (90 %); el
            // siguiente (100 % desde el 01/07/2027) se cargará como revisión cuando la contadora lo confirme.
            ("HEX_DOM_DIURNA", "Hora extra dominical o festiva diurna", 2.15m, false, "Devengados/HorasExtras/HEDDFs/HEDDF"),
            ("HEX_DOM_NOCTURNA", "Hora extra dominical o festiva nocturna", 2.65m, false, "Devengados/HorasExtras/HENDFs/HENDF"),
            ("RECARGO_NOCTURNO", "Recargo nocturno", 0.35m, true, "Devengados/HorasExtras/HRNs/HRN"),
            ("RECARGO_DOMINICAL", "Recargo dominical o festivo", 0.90m, false, "Devengados/HorasExtras/HRDDFs/HRDDF"),
        })
        {
            lista.Add(Def(code, name, ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
            {
                d.UnitKind = UnitKind.HourWithSurcharge; d.UnitFactor = factor; d.RequiresQuantity = true; d.AllowsRepeatInPeriod = true;
                d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
                d.IsBenefitRelated = true; d.AffectsVacationBase = vacaciones; d.DianElement = dian;
            }));
        }

        lista.Add(Def("COMISION", "Comisiones", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
            d.AffectsVacationBase = true; d.DianElement = "Devengados/Comisiones/Comision";
        }));
        lista.Add(Def("BONIF_SALARIAL", "Bonificación salarial", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
            d.AffectsVacationBase = true; d.DianElement = "Devengados/Bonificaciones/BonificacionS";
        }));
        lista.Add(Def("BONIF_NO_SALARIAL", "Bonificación no salarial", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true; d.AffectsWithholdingBase = true;
            d.DianElement = "Devengados/Bonificaciones/BonificacionNS";
        }));
        lista.Add(Def("AUX_NO_SALARIAL", "Auxilio no salarial", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true; d.AffectsWithholdingBase = true;
            d.DianElement = "Devengados/Auxilios/AuxilioNS";
        }));
        lista.Add(Def("VIATICOS", "Viáticos no permanentes", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true;
            d.DianElement = "Devengados/OtrosConceptos/ConceptoNS";
        }));

        // ----------------------------------------- ausentismos y licencias --
        lista.Add(Def(WellKnownConceptCodes.GeneralSickLeave, "Incapacidad por enfermedad general", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.PercentParameterCode = LegalParameterCodes.SickLeaveEmployerPct;
            d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
            d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
            d.DianElement = "Devengados/Incapacidades/Incapacidad";
        }));
        lista.Add(Def("INCAP_LABORAL", "Incapacidad por accidente o enfermedad laboral", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
            d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
            d.DianElement = "Devengados/Incapacidades/Incapacidad";
        }));
        lista.Add(Def("LIC_MATERNIDAD", "Licencia de maternidad o paternidad", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.RequiresDates = true; d.ReducesWorkedDays = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
            d.DianElement = "Devengados/Licencias/LicenciaMP";
        }));
        lista.Add(Def(WellKnownConceptCodes.Vacation, "Vacaciones disfrutadas", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.RequiresDates = true; d.ReducesWorkedDays = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
            d.DianElement = "Devengados/Vacaciones/VacacionesComunes";
        }));
        lista.Add(Def("LIC_REMUNERADA", "Licencia remunerada", ConceptNature.Earning, CalculationKind.QuantityTimesUnit, d =>
        {
            d.UnitKind = UnitKind.Day; d.UnitFactor = 1m; d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true; d.IsBenefitRelated = true;
            d.DianElement = "Devengados/Licencias/LicenciaR";
        }));
        lista.Add(Def("LIC_NO_REMUNERADA", "Licencia no remunerada", ConceptNature.Informative, CalculationKind.FixedAmount, d =>
        {
            d.FixedAmount = 0m; d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
            // Informativa pero SÍ va al documento DIAN: la licencia no remunerada se reporta con sus fechas y sin valor.
            d.DianElement = "Devengados/Licencias/LicenciaNR";
        }));
        lista.Add(Def("SUSPENSION", "Suspensión del contrato", ConceptNature.Informative, CalculationKind.FixedAmount, d =>
        {
            d.FixedAmount = 0m; d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
        }));
        // Feature 010 (D-01): la ausencia que deja la liquidación de vacaciones en cada período que cubre el
        // disfrute cuando la política VacacionesPagoAnticipado es «sí»: la ordinaria descuenta los días del
        // salario SIN pagarlos (los pagó la liquidación). Con la política en «no» se genera VACACIONES.
        lista.Add(Def(WellKnownConceptCodes.VacationLeave, "Ausencia por vacaciones ya pagadas en la liquidación", ConceptNature.Informative, CalculationKind.FixedAmount, d =>
        {
            d.FixedAmount = 0m; d.RequiresDates = true; d.ReducesWorkedDays = true; d.AllowsRepeatInPeriod = true;
            d.ApplicableClasses = SinAprendicesNiPasantes;
        }));

        // ---------------------------------------------- deducciones de ley --
        lista.Add(Def(WellKnownConceptCodes.HealthEmployee, "Salud (aporte del empleado)", ConceptNature.Deduction, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.HealthEmployeePct; d.IsAutomatic = true;
            d.ApplicableClasses = SinAprendicesNiPasantes; // el patrocinador paga la salud completa del aprendiz
            d.DianElement = "Deducciones/Salud";
        }));
        lista.Add(Def(WellKnownConceptCodes.PensionEmployee, "Pensión (aporte del empleado)", ConceptNature.Deduction, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.PensionEmployeePct; d.IsAutomatic = true;
            d.ApplicableClasses = CotizanPension;
            d.DianElement = "Deducciones/FondoPension";
        }));
        lista.Add(Def(WellKnownConceptCodes.SolidarityFund, "Fondo de solidaridad pensional", ConceptNature.Deduction, CalculationKind.RangeTable, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.TableParameterCode = LegalParameterCodes.SolidarityFundTable; d.IsAutomatic = true;
            d.ApplicableClasses = CotizanPension;
            d.DianElement = "Deducciones/FondoSP";
        }));
        lista.Add(Def(WellKnownConceptCodes.Withholding, "Retención en la fuente", ConceptNature.Deduction, CalculationKind.RangeTable, d =>
        {
            d.BaseKind = CalculationBase.WithholdingBase; d.TableParameterCode = LegalParameterCodes.WithholdingTableUvt; d.IsAutomatic = true;
            d.DianElement = "Deducciones/RetencionFuente";
        }));

        // ------------------------------------------- deducciones autorizadas --
        foreach (var (code, name, dian) in new (string, string, string)[]
        {
            ("LIBRANZA", "Libranza", "Deducciones/Libranzas/Libranza"),
            ("PRESTAMO_EMP", "Préstamo de la cooperativa al empleado", "Deducciones/Cooperativa"),
            ("EMBARGO", "Embargo judicial", "Deducciones/OtrasDeducciones/OtraDeduccion"),
            ("OTROS_DESC", "Otros descuentos autorizados", "Deducciones/OtrasDeducciones/OtraDeduccion"),
        })
        {
            lista.Add(Def(code, name, ConceptNature.Deduction, CalculationKind.FixedAmount, d =>
            {
                d.RequiresAmount = true; d.AllowsRepeatInPeriod = true; d.DianElement = dian;
            }));
        }
        lista.Add(Def(WellKnownConceptCodes.LoanDeduction, "Descuento por nómina de Cartera", ConceptNature.Deduction, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.AllowsRepeatInPeriod = true; d.DianElement = "Deducciones/Cooperativa";
        }));
        lista.Add(Def(WellKnownConceptCodes.RoundingAdjustment, "Ajuste por redondeo", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.FixedAmount = 0m; d.IsAutomatic = true; d.DianElement = "Redondeo";
        }));
        // Feature 010: bonificación por retiro voluntario de la definitiva; es una novedad (trae el valor)
        // con el mismo tratamiento tributario de la indemnización (ET art. 401-3).
        lista.Add(Def(WellKnownConceptCodes.RetirementBonus, "Bonificación por retiro voluntario", ConceptNature.Earning, CalculationKind.FixedAmount, d =>
        {
            d.RequiresAmount = true; d.ApplicableClasses = SinAprendicesNiPasantes; d.DianElement = "Devengados/Bonificaciones/BonificacionNS";
        }));

        // -------------------------------------------- aportes del empleador --
        lista.Add(Def(WellKnownConceptCodes.HealthEmployer, "Salud (aporte del empleador)", ConceptNature.EmployerContribution, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.HealthEmployerPct; d.IsAutomatic = true;
            d.ApplicableClasses = SinAprendicesNiPasantes;
        }));
        lista.Add(Def("SALUD_APRENDIZ", "Salud de aprendices y pasantes (a cargo del patrocinador)", ConceptNature.EmployerContribution, CalculationKind.PercentOfBase, d =>
        {
            d.BaseKind = CalculationBase.ContributionBase; d.PercentParameterCode = LegalParameterCodes.HealthApprenticePct; d.IsAutomatic = true;
            d.ApplicableClasses = Clases(EmployeeClass.Apprentice, EmployeeClass.Intern);
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

        // ------------------------------- feature 010: liquidaciones especiales (data-model §1.7) --
        // Los pone el motor de liquidaciones con el valor ya calculado (Liquidado): son «valor fijo»
        // sólo para que la definición sea válida, y automáticos para que nadie los registre como
        // novedad; la nómina ordinaria los salta (WellKnownConceptCodes.SettlementOnly). Las bases
        // dicen qué grava cada rubro: la prima y las vacaciones disfrutadas van a retención, las
        // vacaciones compensadas además cotizan, y cesantías, intereses e indemnización a ninguna.
        lista.Add(Liquidado(WellKnownConceptCodes.ServiceBonus, "Prima de servicios", ConceptNature.Earning, ConPrestaciones, "Devengados/Primas/Prima", d =>
        {
            d.AffectsWithholdingBase = true;
        }));
        lista.Add(Liquidado(WellKnownConceptCodes.Severance, "Cesantías", ConceptNature.Earning, ConPrestaciones, "Devengados/Cesantias"));
        lista.Add(Liquidado(WellKnownConceptCodes.SeveranceInterest, "Intereses a las cesantías", ConceptNature.Earning, ConPrestaciones, "Devengados/Cesantias/@PagoIntereses"));
        lista.Add(Liquidado(WellKnownConceptCodes.VacationPayout, "Vacaciones disfrutadas (liquidación)", ConceptNature.Earning, SinAprendicesNiPasantes, "Devengados/Vacaciones/VacacionesComunes", d =>
        {
            d.AffectsWithholdingBase = true;
        }));
        lista.Add(Liquidado(WellKnownConceptCodes.VacationCompensation, "Vacaciones compensadas en dinero", ConceptNature.Earning, SinAprendicesNiPasantes, "Devengados/Vacaciones/VacacionesCompensadas", d =>
        {
            d.AffectsContributionBase = true; d.AffectsWithholdingBase = true;
        }));
        lista.Add(Liquidado(WellKnownConceptCodes.Indemnity, "Indemnización por terminación del contrato", ConceptNature.Earning, SinAprendicesNiPasantes, "Devengados/Indemnizacion"));
        lista.Add(Liquidado(WellKnownConceptCodes.PendingSalary, "Salario pendiente de la liquidación definitiva", ConceptNature.Earning, Todas, "Devengados/Basico", d =>
        {
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
            d.IsBenefitRelated = true; d.AffectsVacationBase = true;
        }));
        // Diferencia entre lo liquidado y la provisión acumulada; admite negativo (liberación) y el poster
        // invierte débito y crédito con valor negativo. No van al documento DIAN.
        foreach (var (code, name) in new (string, string)[]
        {
            (WellKnownConceptCodes.ServiceBonusProvisionAdjustment, "Ajuste de la provisión de prima"),
            (WellKnownConceptCodes.SeveranceProvisionAdjustment, "Ajuste de la provisión de cesantías"),
            (WellKnownConceptCodes.SeveranceInterestProvisionAdjustment, "Ajuste de la provisión de intereses a las cesantías"),
            (WellKnownConceptCodes.VacationProvisionAdjustment, "Ajuste de la provisión de vacaciones"),
        })
        {
            lista.Add(Liquidado(code, name, ConceptNature.Provision, ConPrestaciones, dian: null));
        }
        // Retención independiente de cada rubro (R5); la del salario pendiente y las vacaciones usa RETEFTE.
        foreach (var (code, name) in new (string, string)[]
        {
            (WellKnownConceptCodes.WithholdingOnServiceBonus, "Retención en la fuente sobre la prima"),
            (WellKnownConceptCodes.WithholdingOnSeverance, "Retención en la fuente sobre cesantías e intereses"),
            (WellKnownConceptCodes.WithholdingOnIndemnity, "Retención en la fuente sobre indemnización y bonificación por retiro"),
        })
        {
            lista.Add(Liquidado(code, name, ConceptNature.Deduction, SinAprendicesNiPasantes, "Deducciones/RetencionFuente"));
        }

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

    /// <summary>Concepto que sólo produce el motor de liquidaciones (feature 010): automático, sin novedad, valor calculado.</summary>
    private static PayrollConceptDefinition Liquidado(string code, string name, ConceptNature nature, int clases, string? dian, Action<PayrollConceptDefinition>? configurar = null) =>
        Def(code, name, nature, CalculationKind.FixedAmount, d =>
        {
            d.FixedAmount = 0m; d.IsAutomatic = true; d.ApplicableClasses = clases; d.DianElement = dian;
            configurar?.Invoke(d);
        });

    /// <summary>
    /// Versiones que arrancan a mitad de año por cambio de ley. Misma regla que en los parámetros
    /// legales: se inserta la versión y se cierra la anterior el día antes, sólo si la anterior es
    /// de la semilla y sigue abierta; una versión propia de la cooperativa no se pisa.
    /// </summary>
    public static IReadOnlyList<(string Code, DateTime ValidFrom, decimal UnitFactor)> Revisiones() =>
    [
        // Vacía desde el 2026-09-13: la base 2026 ya trae el 90 % de julio de 2026. Cuando la contadora
        // confirme el 100 % del 01/07/2027 (Ley 2466 de 2025) se carga aquí:
        // ("RECARGO_DOMINICAL", new DateTime(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc), 1.00m), y las extras 2,25 / 2,75.
    ];

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, ct);

    /// <summary>
    /// La semilla sobre cualquier contexto de la cooperativa. Separada de <see cref="SeedAsync"/>
    /// para poder probarla con InMemory: <see cref="SeedContext"/> exige el
    /// <c>ApplicationDbContext</c> concreto y las pruebas de idempotencia trabajan sobre el de prueba.
    /// </summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existentes = await db.PayrollConceptDefinitions.IgnoreQueryFilters().ToListAsync(ct);
        var claves = existentes.Select(e => $"{e.Code}|{e.ValidFrom:yyyy-MM-dd}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var base_ = Catalogo().ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        foreach (var def in Catalogo())
        {
            if (claves.Contains($"{def.Code}|{def.ValidFrom:yyyy-MM-dd}")) continue;
            db.PayrollConceptDefinitions.Add(def);
            inserted++;
        }
        // Feature 010: las dos columnas nuevas se ponen en su sitio en las versiones que LA SEMILLA CREÓ
        // (no cambian ningún valor calculado, así que no ameritan versión). Una versión que una persona
        // registró al revisar —Origin sigue Seed, pero CreatedBy es ella— lleva lo que ella decidió o
        // heredó (D-29) y no se toca; un concepto propio de la cooperativa (Custom) tampoco. Hasta el
        // 2026-09-21 se pisaban todas las Seed en cada arranque, y la revisión de COMISION perdía la base
        // de vacaciones hasta el reinicio siguiente, cuando volvía a tenerla.
        foreach (var existente in existentes.Where(e => e.Origin == ConceptOrigin.Seed && e.CreatedBy == SeedContext.ParametricCreatedBy))
        {
            if (!base_.TryGetValue(existente.Code, out var modelo)) continue;
            if (existente.AffectsVacationBase == modelo.AffectsVacationBase && existente.DianElement == modelo.DianElement) continue;
            existente.AffectsVacationBase = modelo.AffectsVacationBase;
            existente.DianElement = modelo.DianElement;
            existente.UpdatedBy = SeedContext.ParametricCreatedBy;
            existente.UpdatedAt = DateTime.UtcNow;
        }
        // La base tiene que estar en la base antes de mirar qué versión cerrar: en una cooperativa
        // nueva se insertan ambas en la misma pasada.
        await db.SaveChangesAsync(ct);
        foreach (var (code, validFrom, factor) in Revisiones())
        {
            if (claves.Contains($"{code}|{validFrom:yyyy-MM-dd}")) continue;
            var versiones = await db.PayrollConceptDefinitions.IgnoreQueryFilters()
                .Where(c => c.Code == code).OrderByDescending(c => c.ValidFrom).ToListAsync(ct);
            var ultima = versiones.FirstOrDefault();
            if (ultima is not null && (ultima.ValidFrom >= validFrom || ultima.Origin != ConceptOrigin.Seed)) continue;
            if (ultima is not null && ultima.ValidTo is null)
            {
                ultima.ValidTo = validFrom.AddDays(-1);
                ultima.UpdatedBy = SeedContext.ParametricCreatedBy;
                ultima.UpdatedAt = DateTime.UtcNow;
            }
            var nueva = base_[code];
            nueva.ValidFrom = validFrom;
            nueva.UnitFactor = factor;
            db.PayrollConceptDefinitions.Add(nueva);
            inserted++;
        }

        if (inserted > 0) await db.SaveChangesAsync(ct);
        return inserted;
    }
}
