using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements;

/// <summary>
/// El motor de liquidaciones especiales (feature 010, research R1): puro, sin IO,
/// determinista. Recibe la entrada de UN empleado a UN corte y devuelve sus líneas
/// explicadas, totales, banderas, omisiones y advertencias. No es el motor ordinario con
/// un modo más: aquél ata el período a una periodicidad y corre once pasos de nómina; éste
/// mira un semestre, un año o toda la vinculación, y reutiliza sus piezas (explicación,
/// tramos de salario, calendario 30/360, búsqueda de tramo, depuración de retención).
///
/// <para>
/// Orden por tipo: prima → retención de la prima → ajuste de provisión; cesantías →
/// intereses → retención → ajuste; vacaciones (disfrute o compensación) → aportes →
/// retención → ajuste; definitiva: salario pendiente → novedades del período pendiente (D-28)
/// → prima proporcional → cesantías e intereses → vacaciones pendientes → indemnización →
/// aportes de ley → las cuatro retenciones → ajustes de provisión → descuentos validados. Un empleado sin derecho
/// (salario integral, aprendiz en etapa lectiva, pasante, prima ya pagada en la
/// definitiva) queda <b>excluido</b> con su código; en la definitiva sólo se omite el rubro.
/// </para>
///
/// <para>
/// El motor NO conoce ningún valor legal: días de prima y de cesantías por año, porcentaje
/// de intereses, días de vacaciones, tablas de indemnización y de retención, topes en UVT y
/// SMMLV salen de <see cref="ParameterSet"/>, y si falta uno de
/// <see cref="SettlementParameterCodes.Required"/> se niega nombrándolo (FR-003).
/// </para>
/// </summary>
public sealed class SettlementCalculationEngine
{
    /// <summary>Parámetros requeridos sin vigencia a la fecha: lo que el comando comprueba antes de tocar a nadie.</summary>
    public static IReadOnlyList<string> MissingRequiredParameters(IEnumerable<PayrollLegalParameter> parameters, DateTime asOf) =>
        new ParameterSet(parameters, asOf).Missing(SettlementParameterCodes.Required);

    public SettlementResult Calculate(SettlementInput input)
    {
        var asOf = input.CutoffDate.Date;
        var parameters = new ParameterSet(input.Parameters, asOf);
        var faltantes = parameters.Missing(SettlementParameterCodes.Required);
        if (faltantes.Count > 0) throw CalculationRefusedException.MissingParameters(faltantes, asOf);

        var concepts = new ConceptSet(input.Concepts, asOf);
        var ctx = new SettlementContext { Input = input, Parameters = parameters, Concepts = concepts };
        var hash = SettlementInputsHasher.Compute(input);

        if (input.Employee.Class == EmployeeClass.Apprentice && input.Employee.ApprenticeStage is null)
            ctx.Refuse("El aprendiz no tiene etapa registrada en la ficha (lectiva o práctica, Ley 2466/2025): sin ella no se sabe si tiene derecho a prestaciones.");

        OpeningBalanceStep.WarnIfMissing(ctx);

        switch (input.Kind)
        {
            case SettlementKind.ServiceBonus:
            {
                var (inicio, fin) = input.PeriodStart is { } ps ? (ps.Date, asOf) : ServiceBonusRule.SemesterOf(asOf);
                var exclusion = ServiceBonusRule.Evaluate(ctx, inicio, fin);
                if (exclusion is not null) return Excluded(ctx, exclusion, hash);
                SettlementWithholdingRules.ServiceBonus(ctx);
                ProvisionAdjustmentRule.Evaluate(ctx);
                break;
            }
            case SettlementKind.Severance:
            {
                var (inicio, fin) = input.PeriodStart is { } ps ? (ps.Date, asOf) : SeveranceRule.YearOf(asOf);
                var cesantias = SeveranceRule.Evaluate(ctx, inicio, fin);
                SeveranceInterestRule.Evaluate(ctx, cesantias); // comparte la exclusión y la anota
                if (cesantias.ExclusionCode is { } exclusion) return Excluded(ctx, exclusion, hash);
                SettlementWithholdingRules.SeveranceAndInterest(ctx, severancePaidDirectly: false);
                ProvisionAdjustmentRule.Evaluate(ctx);
                break;
            }
            case SettlementKind.Vacation:
            {
                if (ctx.BenefitsExclusion(forVacations: true) is { } exclusion)
                {
                    ctx.Skip(WellKnownConceptCodes.VacationPayout, exclusion, SettlementContext.ExclusionText(exclusion));
                    return Excluded(ctx, exclusion, hash);
                }
                VacationRule.EvaluateMovement(ctx);
                LegalDeductionsRule.Evaluate(ctx);
                SettlementWithholdingRules.OrdinaryIncome(ctx);
                ProvisionAdjustmentRule.Evaluate(ctx);
                break;
            }
            case SettlementKind.Settlement:
            {
                var (semInicio, semFin) = ServiceBonusRule.SemesterOf(asOf);
                var (anioInicio, anioFin) = SeveranceRule.YearOf(asOf);
                PendingSalaryRule.Evaluate(ctx);
                PendingNoveltiesRule.Evaluate(ctx);
                ServiceBonusRule.Evaluate(ctx, semInicio, semFin);
                var cesantias = SeveranceRule.Evaluate(ctx, anioInicio, anioFin);
                SeveranceInterestRule.Evaluate(ctx, cesantias);
                VacationRule.EvaluateSettlementPayout(ctx);
                IndemnizacionRule.Evaluate(ctx);
                IndemnizacionRule.EvaluateRetirementBonus(ctx);
                LegalDeductionsRule.Evaluate(ctx);
                SettlementWithholdingRules.ServiceBonus(ctx);
                SettlementWithholdingRules.SeveranceAndInterest(ctx, severancePaidDirectly: true);
                SettlementWithholdingRules.SeverancePay(ctx);
                SettlementWithholdingRules.OrdinaryIncome(ctx);
                ProvisionAdjustmentRule.Evaluate(ctx);
                ProposedDeductionsRule.Evaluate(ctx);
                break;
            }
            default:
                throw new CalculationRefusedException($"El tipo de liquidación {input.Kind} no existe.", []);
        }

        return Resultado(ctx, hash, null);
    }

    private static SettlementResult Excluded(SettlementContext ctx, string reasonCode, string hash)
    {
        ctx.Lines.Clear();
        return Resultado(ctx, hash, reasonCode);
    }

    private static SettlementResult Resultado(SettlementContext ctx, string hash, string? exclusionCode)
    {
        decimal Suma(ConceptNature n) => ctx.Lines.Where(l => l.Nature == n).Sum(l => l.Amount);
        var devengos = Suma(ConceptNature.Earning);
        var deducciones = Suma(ConceptNature.Deduction);
        var neto = devengos - deducciones;
        if (neto < 0m) ctx.Flags |= SettlementFlags.NegativeNet;

        return new SettlementResult
        {
            EmployeePublicId = ctx.Employee.PublicId,
            Kind = ctx.Input.Kind,
            CutoffDate = ctx.Cutoff,
            ExclusionReasonCode = exclusionCode,
            ExclusionReason = exclusionCode is null ? null : SettlementContext.ExclusionText(exclusionCode),
            Lines = ctx.Lines.ToList(),
            Totals = new CalculationTotals(
                Earnings: devengos,
                Deductions: deducciones,
                EmployerContributions: Suma(ConceptNature.EmployerContribution),
                Provisions: Suma(ConceptNature.Provision),
                Net: neto,
                RoundingAdjustment: 0m,
                DeferredDeductions: 0m),
            Flags = ctx.Flags,
            Skips = ctx.Skips.ToList(),
            Warnings = ctx.Warnings.ToList(),
            Refusals = ctx.Refusals.ToList(),
            BaseSteps = ctx.BaseSteps.ToList(),
            Vacations = ctx.Vacations,
            InputsHash = hash,
        };
    }
}
