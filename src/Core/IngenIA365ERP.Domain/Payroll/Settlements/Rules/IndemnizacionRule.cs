using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Indemnización por terminación sin justa causa (CST art. 64, Ley 789/2002 art. 28;
/// FR-019). Contrato indefinido: la tabla <c>INDEMNIZACION_TABLA</c> en SMMLV da, por
/// tramo salarial, los días del primer año (<c>FixedValue</c>) y los días por cada año
/// adicional (<c>Rate</c>), proporcionales por fracción (D-08). Término fijo y obra: los
/// días que faltaban para terminar el contrato, con el mínimo parametrizado en la obra.
/// La base es el salario ordinario sin auxilio (más el promedio de variables). Cero,
/// explicado, cuando el motivo no la genera. Aquí no hay 30, 20 ni 15: todo es tabla y
/// parámetro.
/// </summary>
public static class IndemnizacionRule
{
    public static void Evaluate(SettlementContext ctx)
    {
        var code = SettlementConceptCodes.SeverancePay;
        var terminacion = ctx.Input.Termination;
        if (terminacion is null)
        {
            ctx.Refuse("La liquidación definitiva no trae la terminación del contrato (motivo y tipo de contrato).");
            return;
        }
        if (!terminacion.GeneratesSeverancePay)
        {
            ctx.Skip(code, SettlementReasonCodes.MotivoNoGeneraIndemnizacion,
                $"El motivo de retiro «{terminacion.ReasonName}» ({terminacion.ReasonCode}) no genera indemnización: {Fmt.Money(0m)}.");
            return;
        }
        if (ctx.Employee.Class == EmployeeClass.Intern)
        {
            ctx.Skip(code, SettlementReasonCodes.Pasante, "Pasante sin contrato laboral: la indemnización del CST art. 64 no aplica.");
            return;
        }

        var def = ctx.Concept(code);
        if (def is null) return;

        var retiro = ctx.EffectiveEnd;
        var exp = new Explanation { Form = "Indemnización por despido sin justa causa" };
        exp.Note("Motivo", $"{terminacion.ReasonName} ({terminacion.ReasonCode}): genera indemnización (CST art. 64).");

        var salario = ctx.SalaryAt(retiro);
        var variable = ctx.AverageVariable(retiro.AddMonths(-12).AddDays(1), retiro, vacationBase: true);
        var baseMensual = salario + variable;
        exp.Step("Salario ordinario al retiro (sin auxilio de transporte)", salario);
        if (variable != 0m) exp.Step("+ Promedio mensual de devengos variables del último año", variable);
        var diario = baseMensual / CalendarConventions.DaysPerMonth;
        exp.Step($"Valor del día: base / {CalendarConventions.DaysPerMonth}", diario);
        exp.Base = new ExplanationBase("Salario base de la indemnización", baseMensual);

        decimal dias;
        switch (ctx.Employee.ContractType)
        {
            case DianContractType.Indefinite:
            {
                var tabla = ctx.Parameters.Table(SettlementParameterCodes.SeverancePayTable);
                var busqueda = RangeTableLookup.Find(tabla, baseMensual, ctx.Parameters);
                var umbral = ctx.Parameters.Value(SettlementParameterCodes.SeverancePayThresholdSmmlv);
                var pUmbral = ctx.Parameters.Describe(SettlementParameterCodes.SeverancePayThresholdSmmlv);
                exp.Parameter = new ExplanationParameter(tabla.Code, tabla.ValidFrom, null);
                if (!string.IsNullOrWhiteSpace(tabla.Source)) exp.Note("Tabla", tabla.Source);
                exp.Step($"Salario en {busqueda.UnitName} (1 {busqueda.UnitName} = {Fmt.Money(busqueda.UnitValue)}); umbral de la norma: {Fmt.Num(umbral)} ({pUmbral.Code})", busqueda.BaseInUnits);
                if (busqueda.Range is not { } tramo)
                {
                    ctx.Refuse($"El salario ({Fmt.Num(busqueda.BaseInUnits)} {busqueda.UnitName}) no cae en ningún tramo de {tabla.Code}: revise la tabla de indemnización.");
                    return;
                }
                exp.Range = busqueda.ExplanationRange;
                var primerAnio = tramo.FixedValue ?? 0m;
                var porAnio = tramo.Rate ?? 0m;
                var hasta = tramo.ToValue is { } t ? Fmt.Num(t) : "en adelante";
                exp.Note("Tramo", $"{Fmt.Num(tramo.FromValue)} a {hasta} {busqueda.UnitName}: {Fmt.Num(primerAnio)} días el primer año y {Fmt.Num(porAnio)} por cada año adicional, proporcionales por fracción.");

                var antiguedad = CalendarConventions.Days(ctx.Employee.JoinDate, retiro);
                exp.Step($"Tiempo de servicio del {Fmt.Date(ctx.Employee.JoinDate)} al {Fmt.Date(retiro)} (días comerciales)", antiguedad);
                if (antiguedad <= 360)
                {
                    dias = primerAnio;
                    exp.Step("Primer año o menos: días del primer año", dias);
                }
                else
                {
                    var adicionales = antiguedad - 360;
                    var diasAdicionales = adicionales * porAnio / 360m;
                    exp.Step($"Días adicionales al primer año: {adicionales} × {Fmt.Num(porAnio)} / 360", diasAdicionales);
                    dias = primerAnio + diasAdicionales;
                    exp.Step("Días de indemnización", dias);
                }
                break;
            }
            case DianContractType.FixedTerm:
            case DianContractType.WorkOrLabor:
            case DianContractType.Apprenticeship:
            {
                if (ctx.Employee.ContractEndDate is not { } finContrato)
                {
                    ctx.Refuse("El contrato es a término fijo, por obra o de aprendizaje y la terminación no trae la fecha en que terminaba: sin ella no hay «tiempo que faltare» (CST art. 64).");
                    return;
                }
                var faltante = CalendarConventions.Days(retiro.AddDays(1), finContrato);
                exp.Step($"Días que faltaban del {Fmt.Date(retiro.AddDays(1))} al {Fmt.Date(finContrato)} (contrato {Nombre(ctx.Employee.ContractType)})", faltante);
                dias = faltante;
                if (ctx.Employee.ContractType == DianContractType.WorkOrLabor)
                {
                    var minimo = ctx.Parameters.Value(SettlementParameterCodes.SeverancePayWorkContractMinimumDays);
                    var pMin = ctx.Parameters.Describe(SettlementParameterCodes.SeverancePayWorkContractMinimumDays);
                    exp.Parameter = pMin;
                    if (dias < minimo)
                    {
                        exp.Step($"Mínimo de la obra o labor ({pMin.Code}, vigente desde {Fmt.Date(pMin.ValidFrom)})", minimo);
                        dias = minimo;
                    }
                }
                break;
            }
            default:
                ctx.Skip(code, SettlementReasonCodes.Pasante, $"Contrato {Nombre(ctx.Employee.ContractType)}: sin indemnización del CST art. 64.");
                return;
        }

        var valor = dias * diario;
        exp.Step($"{Fmt.Num(dias)} días × {Fmt.Money(diario)}", valor);
        exp.Summary = $"{Fmt.Num(dias)} días × {Fmt.Money(diario)} = {Fmt.Money(valor)}";
        ctx.Add(LineFactory.Create(def, valor, exp, quantity: dias, baseAmount: baseMensual));
    }

    /// <summary>Bonificación por retiro pactada (voluntaria o no): mismo tratamiento tributario que la indemnización (ET art. 401-3).</summary>
    public static void EvaluateRetirementBonus(SettlementContext ctx)
    {
        var terminacion = ctx.Input.Termination;
        if (terminacion is not { VoluntaryRetirementBonus: { } bono } || bono <= 0m) return;
        if (ctx.Concept(SettlementConceptCodes.RetirementBonus) is not { } bonoDef) return;
        var exp = new Explanation { Form = "Bonificación por retiro" };
        exp.Step("Bonificación pactada al retiro", bono);
        exp.Summary = $"Bonificación por retiro: {Fmt.Money(bono)}";
        ctx.Add(LineFactory.Create(bonoDef, bono, exp));
    }

    private static string Nombre(DianContractType t) => t switch
    {
        DianContractType.FixedTerm => "a término fijo",
        DianContractType.Indefinite => "a término indefinido",
        DianContractType.WorkOrLabor => "por obra o labor",
        DianContractType.Apprenticeship => "de aprendizaje",
        DianContractType.Internship => "de prácticas o pasantía",
        _ => t.ToString(),
    };
}
