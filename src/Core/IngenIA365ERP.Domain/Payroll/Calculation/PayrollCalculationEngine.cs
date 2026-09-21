using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// El motor de liquidación (D-01): puro, sin IO, determinista. Recibe la entrada de UN
/// empleado y devuelve sus líneas, bases, totales, banderas y explicaciones. El orden
/// de evaluación es el de data-model §3: salario por tramos → auxilio de transporte →
/// devengos automáticos → novedades de devengo → compuestos → bases → deducciones de
/// ley → retención (base depurada) → deducciones autorizadas con tope y saldo diferido
/// → aportes del empleador y provisiones → redondeo y ajuste.
///
/// <para>
/// El motor NO conoce ningún valor legal: todo porcentaje, tope y tabla sale de
/// <see cref="ParameterSet"/>, y si falta uno requerido se niega nombrándolo
/// (FR-010, FR-011). Reconoce por su código sólo a los conceptos de
/// <see cref="WellKnownConceptCodes"/>, por lo que SON, no por cuánto valen.
/// </para>
/// </summary>
public sealed class PayrollCalculationEngine
{
    private readonly Dictionary<CalculationKind, ICalculationRule> _rules = new ICalculationRule[]
    {
        new FixedAmountRule(), new PercentOfBaseRule(), new QuantityTimesUnitRule(), new RangeTableRule(), new CompositeOfConceptsRule(),
    }.ToDictionary(r => r.Kind);

    private static readonly string[] OrdenDeducciones =
    [
        WellKnownConceptCodes.HealthEmployee, WellKnownConceptCodes.PensionEmployee, WellKnownConceptCodes.SolidarityFund,
    ];

    private static readonly string[] OrdenEmpleador =
    [
        WellKnownConceptCodes.HealthEmployer, WellKnownConceptCodes.PensionEmployer, WellKnownConceptCodes.WorkRisk,
        WellKnownConceptCodes.Sena, WellKnownConceptCodes.Icbf, WellKnownConceptCodes.FamilyCompensation,
    ];

    /// <summary>Parámetros requeridos sin vigencia a la fecha: lo que el comando de cálculo comprueba antes de tocar a nadie.</summary>
    public static IReadOnlyList<string> MissingRequiredParameters(IEnumerable<PayrollLegalParameter> parameters, DateTime asOf) =>
        new ParameterSet(parameters, asOf).Missing(LegalParameterCodes.Required);

    public static decimal Round(decimal value, PayrollRounding rounding) =>
        Math.Round(value, rounding == PayrollRounding.Peso ? 0 : 2, MidpointRounding.AwayFromZero);

    public CalculationResult Calculate(CalculationInput input)
    {
        var period = input.Period;
        var asOf = period.EndDate.Date;
        var parameters = new ParameterSet(input.Parameters, asOf);
        var faltantes = parameters.Missing(LegalParameterCodes.Required);
        if (faltantes.Count > 0) throw CalculationRefusedException.MissingParameters(faltantes, asOf);

        var concepts = new ConceptSet(input.Concepts, asOf);
        var employee = input.Employee;
        var refusals = new List<string>();
        var flags = RunEmployeeFlag.None;
        var hash = InputsHasher.Compute(input);

        // --- novedades: concepto vigente y aplicable a la clase ---
        var novedades = new List<(NoveltyInput Nov, PayrollConceptDefinition Def)>();
        foreach (var n in input.Novelties)
        {
            var def = concepts.Find(n.ConceptCode)
                ?? throw new CalculationRefusedException(
                    $"La novedad {n.PublicId} referencia el concepto {n.ConceptCode}, sin versión vigente al {asOf:yyyy-MM-dd}.", [n.ConceptCode]);
            if (!def.AppliesTo(employee.Class))
            {
                refusals.Add($"La novedad {def.Code} ({def.Name}) no aplica a la clase de empleado {employee.Class} y se omitió.");
                continue;
            }
            novedades.Add((n, def));
        }

        // --- ausencias que restan días al salario ---
        var ausencias = novedades
            .Where(x => x.Def.ReducesWorkedDays && x.Nov.StartDate is not null && x.Nov.EndDate is not null)
            .Select(x => CalendarConventions.Overlap(period.StartDate, period.EndDate, x.Nov.StartDate!.Value, x.Nov.EndDate!.Value))
            .Where(o => o is not null)
            .Select(o => o!.Value)
            .ToList();

        var tramos = SalaryTranches.Build(period, employee, ausencias);
        var ctx = new RuleContext { Input = input, Parameters = parameters, Concepts = concepts, Tranches = tramos };

        if (tramos.Count == 0)
        {
            refusals.Add("El empleado no tiene días vinculados dentro del período (ingreso posterior o retiro anterior): no se liquida.");
            return Resultado(employee, ctx, refusals, RunEmployeeFlag.None, hash, [], 0m);
        }

        var rounding = input.Policies.Rounding;

        void Add(CalculationLine line)
        {
            line.Amount = Round(line.RawAmount, rounding);
            line.Order = ctx.Lines.Count + 1;
            ctx.Lines.Add(line);
        }

        // Los automáticos en cero no producen línea, salvo los de tabla (retención, fondo
        // de solidaridad): su explicación de «no aplica» es justo lo que la contadora
        // quiere leer.
        void AddAutomatic(CalculationLine? line)
        {
            if (line is null) return;
            if (Round(line.RawAmount, rounding) == 0m && line.Explanation.Range is null && line.RangeFrom is null
                && !line.Explanation.Form.StartsWith("Tabla", StringComparison.Ordinal))
            {
                ctx.Skip($"{line.Code}: valor cero ({line.Explanation.Summary}).");
                return;
            }
            Add(line);
        }

        // 1. Salario básico por tramos.
        if (concepts.Find(WellKnownConceptCodes.BasicSalary) is { } salarioDef)
        {
            if (salarioDef.AppliesTo(employee.Class)) Add(SalaryLine(salarioDef, ctx));
            else ctx.Skip($"{salarioDef.Code}: no aplica a la clase {employee.Class}.");
        }
        else
        {
            refusals.Add($"No hay una versión vigente del concepto {WellKnownConceptCodes.BasicSalary}: el salario no se liquidó.");
        }

        // 2. Auxilio de transporte: sólo si el salario está bajo el tope parametrizado (FR-012).
        if (concepts.Find(WellKnownConceptCodes.TransportAllowance) is { } auxDef && auxDef.AppliesTo(employee.Class))
        {
            var (eligible, reason) = BaseBuilder.TransportAllowanceEligibility(ctx);
            if (eligible)
            {
                var line = Evaluate(auxDef, null, ctx);
                if (line is not null)
                {
                    line.Explanation.Note("Derecho", reason);
                    AddAutomatic(line);
                }
            }
            else ctx.Skip($"{auxDef.Code}: {reason}");
        }

        // 3. Otros devengos automáticos.
        foreach (var def in Automatic(concepts, employee.Class, ConceptNature.Earning, [WellKnownConceptCodes.BasicSalary, WellKnownConceptCodes.TransportAllowance]))
            AddAutomatic(Evaluate(def, null, ctx));

        // 4. Novedades de devengo e informativas (las compuestas van en el paso 5).
        foreach (var (nov, def) in novedades.Where(x => x.Def.Nature is ConceptNature.Earning or ConceptNature.Informative
                                                         && x.Def.CalculationKind != CalculationKind.CompositeOfConcepts))
        {
            var line = Evaluate(def, nov, ctx);
            if (line is not null) Add(line);
        }

        // 5. Compuestos de devengo, en orden de dependencias.
        Composites(ctx, novedades, refusals, d => d.Nature is ConceptNature.Earning or ConceptNature.Informative, Add);

        // 6. Bases.
        var baseSteps = BaseBuilder.Build(ctx);

        // 7. Deducciones de ley (automáticas, salvo la retención).
        foreach (var def in Automatic(concepts, employee.Class, ConceptNature.Deduction, [])
                     .Where(d => d.BaseKind != CalculationBase.WithholdingBase)
                     .OrderBy(d => Posicion(OrdenDeducciones, d.Code)).ThenBy(d => d.Code, StringComparer.Ordinal))
        {
            if (def.Code.Equals(WellKnownConceptCodes.HealthEmployee, StringComparison.OrdinalIgnoreCase) && !employee.Affiliations.Health)
            {
                flags |= RunEmployeeFlag.MissingAffiliation;
                refusals.Add("Sin afiliación a salud registrada en la ficha: el aporte no tiene destinatario.");
            }
            if (def.Code.Equals(WellKnownConceptCodes.PensionEmployee, StringComparison.OrdinalIgnoreCase) && !employee.Affiliations.Pension)
            {
                flags |= RunEmployeeFlag.MissingAffiliation;
                refusals.Add("Sin afiliación a pensión registrada en la ficha: el aporte no tiene destinatario.");
            }
            AddAutomatic(Evaluate(def, null, ctx));
        }

        // 8. Retención en la fuente sobre la base depurada (FR-039).
        var depuracion = WithholdingBaseBuilder.Build(ctx);
        foreach (var def in Automatic(concepts, employee.Class, ConceptNature.Deduction, [])
                     .Where(d => d.BaseKind == CalculationBase.WithholdingBase))
        {
            if (employee.WithholdingProcedure == 2)
            {
                if (employee.WithholdingRatePercent is not { } tasa)
                {
                    flags |= RunEmployeeFlag.WithholdingRateMissing;
                    refusals.Add($"Procedimiento 2 sin porcentaje de retención vigente al {asOf:yyyy-MM-dd}: la retención no se calculó. " +
                                 "Registre el porcentaje del semestre en la ficha del empleado.");
                    continue;
                }
                AddAutomatic(WithholdingProcedure2(def, ctx, depuracion, tasa));
            }
            else
            {
                var line = Evaluate(def, null, ctx);
                if (line is not null)
                {
                    line.Explanation.Steps.InsertRange(0, depuracion.Steps);
                    if (!depuracion.HasDeclaredItems)
                        line.Explanation.Note("Depuraciones declaradas", "Ninguna: si el empleado tiene intereses de vivienda, medicina prepagada o dependientes, faltó registrarlos.");
                    AddAutomatic(line);
                }
            }
        }

        // 9. Deducciones autorizadas (novedades) con tope y saldo diferido.
        var devengos = ctx.Lines.Where(l => l.Nature == ConceptNature.Earning).Sum(l => l.Amount);
        var deduccionesLey = ctx.Lines.Where(l => l.Nature == ConceptNature.Deduction).Sum(l => l.Amount);
        var pctMaximo = parameters.Fraction(LegalParameterCodes.MaxDeductionOfSalaryPct);
        var pMax = parameters.Describe(LegalParameterCodes.MaxDeductionOfSalaryPct);
        var disponible = Math.Max(0m, (devengos - deduccionesLey) * pctMaximo);
        foreach (var (nov, def) in novedades.Where(x => x.Def.Nature == ConceptNature.Deduction
                                                         && x.Def.CalculationKind != CalculationKind.CompositeOfConcepts))
        {
            var line = Evaluate(def, nov, ctx);
            if (line is null) continue;

            if (nov.Origin == NoveltyOrigin.LoanDeduction)
            {
                line.Explanation.Note("Origen", "Descuento ya aplicado y contabilizado por Cartera: se muestra para el neto y el comprobante, no genera asiento ni se difiere.");
                Add(line);
                continue;
            }

            if (line.RawAmount > disponible)
            {
                var diferido = line.RawAmount - disponible;
                line.Explanation.Step($"Tope de deducciones autorizadas: {Fmt.Pct(pctMaximo)} de (devengos − deducciones de ley) ({pMax.Code})", disponible);
                line.Explanation.Step("Saldo que se difiere al período siguiente", diferido);
                line.DeferredAmount = Round(diferido, rounding);
                line.RawAmount = disponible;
                flags |= RunEmployeeFlag.DeductionsOverMax;
            }
            disponible = Math.Max(0m, disponible - line.RawAmount);
            Add(line);
        }
        Composites(ctx, novedades, refusals, d => d.Nature == ConceptNature.Deduction, Add);

        // 10. Aportes del empleador y provisiones: no afectan el neto.
        var exoneracion = input.Policies.ApplyEmployerExemption;
        var ibc = ctx.Bases.ContributionBase ?? 0m;
        foreach (var def in Automatic(concepts, employee.Class, ConceptNature.EmployerContribution, [])
                     .OrderBy(d => Posicion(OrdenEmpleador, d.Code)).ThenBy(d => d.Code, StringComparer.Ordinal)
                     .Concat(Automatic(concepts, employee.Class, ConceptNature.Provision, [])))
        {
            if (exoneracion && WellKnownConceptCodes.EmployerExemptionApplies.Contains(def.Code, StringComparer.OrdinalIgnoreCase))
            {
                var smmlv = parameters.Value(LegalParameterCodes.Smmlv);
                var topeSmmlv = parameters.Value(LegalParameterCodes.PayrollExemptionThresholdSmmlv);
                var tope = smmlv * topeSmmlv * period.DaysInPeriod / CalendarConventions.DaysPerMonth;
                if (ibc < tope)
                {
                    ctx.Skip($"{def.Code}: exonerado (art. 114-1 E.T.): IBC {Fmt.Money(ibc)} < {Fmt.Num(topeSmmlv)} SMMLV ({Fmt.Money(tope)}).");
                    continue;
                }
            }

            var line = Evaluate(def, null, ctx);
            if (line is null)
            {
                if (def.Code.Equals(WellKnownConceptCodes.WorkRisk, StringComparison.OrdinalIgnoreCase))
                {
                    flags |= RunEmployeeFlag.MissingAffiliation;
                    refusals.Add("Sin clase de riesgo ARL registrada en la ficha: el aporte a riesgos laborales no se calculó.");
                }
                continue;
            }
            AddAutomatic(line);
        }
        Composites(ctx, novedades, refusals, d => d.Nature is ConceptNature.EmployerContribution or ConceptNature.Provision, Add);

        // 11. Totales, redondeo y ajuste (FR-017).
        var totalDevengos = ctx.Lines.Where(l => l.Nature == ConceptNature.Earning).Sum(l => l.Amount);
        var totalDeducciones = ctx.Lines.Where(l => l.Nature == ConceptNature.Deduction).Sum(l => l.Amount);
        var netoCrudo = ctx.Lines.Where(l => l.Nature == ConceptNature.Earning).Sum(l => l.RawAmount)
                      - ctx.Lines.Where(l => l.Nature == ConceptNature.Deduction).Sum(l => l.RawAmount);
        var netoRedondeado = Round(netoCrudo, rounding);
        var ajuste = netoRedondeado - (totalDevengos - totalDeducciones);
        if (ajuste != 0m)
        {
            if (concepts.Find(WellKnownConceptCodes.RoundingAdjustment) is { } ajusteDef)
            {
                var exp = new Explanation { Form = "Ajuste de redondeo" };
                exp.Step("Neto sin redondear", netoCrudo);
                exp.Step("Neto redondeado", netoRedondeado);
                exp.Step("Suma de líneas redondeadas (devengos − deducciones)", totalDevengos - totalDeducciones);
                exp.Step("Diferencia imputada al ajuste", ajuste);
                exp.Summary = $"Ajuste por redondeo: {Fmt.Money(ajuste)}";
                Add(LineFactory.Create(ajusteDef, ajuste, exp));
                totalDevengos += ajuste;
            }
            else
            {
                refusals.Add($"Diferencia de redondeo de {Fmt.Money(ajuste)} sin concepto {WellKnownConceptCodes.RoundingAdjustment} vigente: los totales son la suma de las líneas.");
            }
        }

        var neto = totalDevengos - totalDeducciones;
        if (neto < 0m) flags |= RunEmployeeFlag.NegativeNet;

        return Resultado(employee, ctx, refusals, flags, hash, baseSteps, ajuste);
    }

    // ------------------------------------------------------------------ helpers --

    private CalculationLine? Evaluate(PayrollConceptDefinition def, NoveltyInput? novelty, RuleContext ctx)
    {
        if (!_rules.TryGetValue(def.CalculationKind, out var rule))
            throw new CalculationRefusedException($"El concepto {def.Code} usa la forma {def.CalculationKind}, que el motor no conoce.", [def.Code]);
        return rule.Evaluate(def, novelty, ctx);
    }

    private static IEnumerable<PayrollConceptDefinition> Automatic(ConceptSet concepts, EmployeeClass clase, ConceptNature nature, string[] excluir) =>
        concepts.All
            .Where(c => c.IsAutomatic && c.Nature == nature && c.AppliesTo(clase)
                        && c.CalculationKind != CalculationKind.CompositeOfConcepts
                        && !excluir.Contains(c.Code, StringComparer.OrdinalIgnoreCase)
                        && !c.Code.Equals(WellKnownConceptCodes.RoundingAdjustment, StringComparison.OrdinalIgnoreCase)
                        // Feature 010: los de las liquidaciones especiales son del otro motor.
                        && !WellKnownConceptCodes.SettlementOnly.Contains(c.Code, StringComparer.OrdinalIgnoreCase))
            .OrderBy(c => c.Code, StringComparer.Ordinal);

    private static int Posicion(string[] orden, string code)
    {
        var i = Array.FindIndex(orden, o => o.Equals(code, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? orden.Length : i;
    }

    /// <summary>Compuestos de las naturalezas indicadas: automáticos o disparados por novedad, en orden topológico.</summary>
    private void Composites(RuleContext ctx, List<(NoveltyInput Nov, PayrollConceptDefinition Def)> novedades,
        List<string> refusals, Func<PayrollConceptDefinition, bool> natureFilter, Action<CalculationLine> add)
    {
        foreach (var def in ConceptDependencyGraph.TopologicalOrder(ctx.Concepts.All).Where(natureFilter))
        {
            if (!def.AppliesTo(ctx.Employee.Class)) continue;
            var disparadores = novedades.Where(x => x.Def.Code.Equals(def.Code, StringComparison.OrdinalIgnoreCase)).ToList();
            if (disparadores.Count > 0)
            {
                foreach (var (nov, _) in disparadores)
                {
                    var line = Evaluate(def, nov, ctx);
                    if (line is not null) add(line);
                }
            }
            else if (def.IsAutomatic)
            {
                var line = Evaluate(def, null, ctx);
                if (line is not null && line.RawAmount != 0m) add(line);
                else if (line is not null) ctx.Skip($"{def.Code}: valor cero.");
            }
        }
    }

    private static CalculationLine SalaryLine(PayrollConceptDefinition def, RuleContext ctx)
    {
        var exp = new Explanation { Form = "Salario por tramos" };
        var total = 0m;
        foreach (var t in ctx.Tranches)
        {
            var diario = t.MonthlySalary / CalendarConventions.DaysPerMonth;
            var valor = diario * t.PaidDays;
            var ausencia = t.AbsenceDays > 0 ? $" − {t.AbsenceDays} de ausencia" : string.Empty;
            exp.Step($"Tramo {Fmt.Date(t.From)} a {Fmt.Date(t.To)}: {t.Days} días{ausencia} = {t.PaidDays} días × {Fmt.Money(diario)} " +
                     $"(salario {Fmt.Money(t.MonthlySalary)} / {CalendarConventions.DaysPerMonth})", valor);
            total += valor;
        }
        exp.Step("Salario del período", total);
        exp.Summary = $"{ctx.PaidDays} días: {Fmt.Money(total)}";
        return LineFactory.Create(def, total, exp, quantity: ctx.PaidDays, baseAmount: SalaryTranches.SalaryAtEnd(ctx.Tranches));
    }

    private static CalculationLine WithholdingProcedure2(PayrollConceptDefinition def, RuleContext ctx,
        Depuration depuracion, decimal tasaPct)
    {
        var fraccion = tasaPct / 100m;
        var exp = new Explanation
        {
            Form = "Base × porcentaje (procedimiento 2)",
            Base = new ExplanationBase(Rules.Bases.Label(CalculationBase.WithholdingBase), depuracion.Base),
            Factor = fraccion,
        };
        exp.Steps.AddRange(depuracion.Steps);
        exp.Step("Porcentaje fijo del empleado (procedimiento 2, vigente para la fecha del período)", tasaPct);
        var valor = depuracion.Base * fraccion;
        exp.Step("Base depurada × porcentaje", valor);

        if (ctx.Parameters.Has(LegalParameterCodes.WithholdingRoundingMultiple))
        {
            var multiplo = ctx.Parameters.Value(LegalParameterCodes.WithholdingRoundingMultiple);
            if (multiplo > 0m)
            {
                var aproximado = Math.Round(valor / multiplo, 0, MidpointRounding.AwayFromZero) * multiplo;
                if (aproximado != valor)
                {
                    exp.Step($"Aproximado al múltiplo de {Fmt.Money(multiplo)} ({LegalParameterCodes.WithholdingRoundingMultiple})", aproximado);
                    valor = aproximado;
                }
            }
        }
        exp.Summary = $"{Fmt.Money(depuracion.Base)} × {Fmt.Pct(fraccion)} = {Fmt.Money(valor)}";
        return LineFactory.Create(def, valor, exp, baseAmount: depuracion.Base, factor: fraccion);
    }

    private static CalculationResult Resultado(EmployeeInput employee, RuleContext ctx, List<string> refusals,
        RunEmployeeFlag flags, string hash, IReadOnlyList<ExplanationStep> baseSteps, decimal ajuste)
    {
        decimal Suma(ConceptNature n) => ctx.Lines.Where(l => l.Nature == n).Sum(l => l.Amount);
        var devengos = Suma(ConceptNature.Earning);
        var deducciones = Suma(ConceptNature.Deduction);
        return new CalculationResult
        {
            EmployeePublicId = employee.PublicId,
            Lines = ctx.Lines.ToList(),
            Totals = new CalculationTotals(
                Earnings: devengos,
                Deductions: deducciones,
                EmployerContributions: Suma(ConceptNature.EmployerContribution),
                Provisions: Suma(ConceptNature.Provision),
                Net: devengos - deducciones,
                RoundingAdjustment: ajuste,
                DeferredDeductions: ctx.Lines.Sum(l => l.DeferredAmount)),
            Flags = flags,
            Tranches = ctx.Tranches,
            DaysLinked = ctx.LinkedDays,
            AbsenceDays = ctx.Tranches.Sum(t => t.AbsenceDays),
            Refusals = refusals,
            Skips = ctx.Skips.ToList(),
            BaseSteps = baseSteps,
            InputsHash = hash,
        };
    }
}
