using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Novedades del período pendiente en la definitiva (D-28): las extras, recargos, incapacidades,
/// comisiones y descuentos autorizados que el empleado tenía activos en el período abierto donde
/// cae el retiro. La nómina ordinaria de ese período ya no lo incluye —el último tramo lo paga la
/// definitiva—, así que sin esto se perderían. Cada novedad se vuelve una línea con su propio
/// concepto, evaluada con la misma forma de cálculo de la ordinaria («valor fijo» y «cantidad ×
/// unidad») sobre los tramos de salario del período pendiente hasta el retiro; las deducciones de
/// ley y la retención ordinaria las toman después por las marcas del concepto, igual que al
/// salario pendiente. Una forma que necesita las bases de la ordinaria (porcentaje sobre base,
/// compuesto, tabla) no se liquida aquí y queda en las omisiones con
/// <see cref="SettlementReasonCodes.NovedadNoLiquidable"/>.
/// </summary>
public static class PendingNoveltiesRule
{
    private static readonly Dictionary<CalculationKind, ICalculationRule> Formas = new ICalculationRule[]
    {
        new FixedAmountRule(), new QuantityTimesUnitRule(),
    }.ToDictionary(r => r.Kind);

    public static void Evaluate(SettlementContext ctx)
    {
        if (ctx.Input.PendingNovelties.Count == 0) return;
        if (ctx.Input.PendingSalary is not { } pendiente)
        {
            foreach (var n in ctx.Input.PendingNovelties)
                ctx.Skip(n.ConceptCode, SettlementReasonCodes.SinDiasPendientes,
                    $"{n.ConceptCode}: la novedad {n.PublicId} llegó sin período pendiente informado; la nómina ordinaria la liquida.");
            return;
        }

        var inicio = pendiente.PeriodStart.Date;
        var fin = ctx.EffectiveEnd < pendiente.PeriodEnd.Date ? ctx.EffectiveEnd : pendiente.PeriodEnd.Date;
        var tramos = ctx.Tranches(inicio, fin);
        if (tramos.Count == 0)
        {
            foreach (var n in ctx.Input.PendingNovelties)
                ctx.Skip(n.ConceptCode, SettlementReasonCodes.SinDiasPendientes, $"{n.ConceptCode}: el retiro es anterior al período pendiente; la novedad no tiene días que liquidar.");
            return;
        }

        // El contexto de la ordinaria sobre el tramo pendiente: mismas reglas, mismo salario por fecha.
        var ordinario = new RuleContext
        {
            Input = new CalculationInput
            {
                Period = new PeriodInput(inicio, fin, PayrollPeriodicity.Monthly),
                Employee = new EmployeeInput
                {
                    PublicId = ctx.Employee.PublicId,
                    DisplayName = ctx.Employee.DisplayName,
                    Class = ctx.Employee.Class,
                    JoinDate = ctx.EmploymentStart,
                    TerminationDate = ctx.Employee.TerminationDate,
                    SalaryHistory = ctx.Employee.SalaryHistory,
                    Affiliations = ctx.Employee.Affiliations,
                },
                Novelties = ctx.Input.PendingNovelties,
                Concepts = ctx.Input.Concepts,
                Parameters = ctx.Input.Parameters,
                Policies = new CalculationPolicies { Rounding = ctx.Policies.Rounding },
            },
            Parameters = ctx.Parameters,
            Concepts = ctx.Concepts,
            Tranches = tramos,
        };

        foreach (var novedad in ctx.Input.PendingNovelties)
        {
            var def = ctx.Concept(novedad.ConceptCode);
            if (def is null) continue;
            if (def.Nature is not (ConceptNature.Earning or ConceptNature.Deduction))
            {
                ctx.Skip(def.Code, SettlementReasonCodes.NovedadNoLiquidable, $"{def.Code}: la novedad {novedad.PublicId} es {def.Nature} y no se paga ni se descuenta en la definitiva.");
                continue;
            }
            if (!def.AppliesTo(ctx.Employee.Class))
            {
                ctx.Skip(def.Code, SettlementReasonCodes.NovedadNoLiquidable, $"{def.Code}: no aplica a la clase {ctx.Employee.Class}; la novedad {novedad.PublicId} se omitió.");
                continue;
            }
            if (!Formas.TryGetValue(def.CalculationKind, out var forma))
            {
                ctx.Skip(def.Code, SettlementReasonCodes.NovedadNoLiquidable,
                    $"{def.Code}: la forma «{def.CalculationKind}» necesita las bases de la nómina ordinaria y la definitiva no la liquida; la novedad {novedad.PublicId} queda pendiente de tratarse a mano.");
                continue;
            }

            var linea = forma.Evaluate(def, novedad, ordinario);
            if (linea is null)
            {
                ctx.Skip(def.Code, SettlementReasonCodes.NovedadNoLiquidable, ordinario.Skips.LastOrDefault() ?? $"{def.Code}: la novedad {novedad.PublicId} no produjo valor.");
                continue;
            }
            linea.Explanation.Note("Período pendiente",
                $"Novedad del período {Fmt.Date(pendiente.PeriodStart)} a {Fmt.Date(pendiente.PeriodEnd)} liquidada en la definitiva: la nómina ordinaria de ese período ya no incluye al empleado (D-28).");
            ctx.Add(linea);
        }
    }
}
