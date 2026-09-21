using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Retención en la fuente de las liquidaciones especiales, por norma y por rubro
/// (FR-006a, research R5), toda desde parámetros y con explicación paso a paso:
/// <list type="bullet">
/// <item><b>Prima</b>: en procedimiento 1 retención independiente sobre la prima sola (ET
/// art. 385: sin restar aportes, menos la renta exenta del porcentaje legal dentro de su
/// cupo, tabla del art. 383, aproximación); en procedimiento 2, la prima depurada por el
/// porcentaje fijo del empleado (art. 386).</item>
/// <item><b>Cesantías e intereses</b> (ET art. 206 num. 4): el ingreso laboral promedio de
/// los seis últimos meses en UVT decide el porcentaje no gravado (tabla
/// <c>CESANTIAS_GRAVADA_TABLA_UVT</c>); la parte gravada de cada rubro va a la tabla 383 de
/// forma independiente. Lo consignado al fondo no se retiene al consignar: sólo lo pagado
/// directo (intereses, y las cesantías de la definitiva).</item>
/// <item><b>Indemnización y bonificación por retiro</b> (ET art. 401-3): si el ingreso
/// mensual supera el tope en UVT, (valor − renta exenta dentro del cupo anual) × tarifa;
/// si no, cero.</item>
/// <item><b>Vacaciones y salario pendiente</b>: pago laboral ordinario, depurado con
/// <see cref="DepuracionDeRetencion"/> y el procedimiento del empleado.</item>
/// </list>
/// Los cupos anuales se observan según la política <c>RetefteTopesAnualesModo</c>.
/// </summary>
public static class SettlementWithholdingRules
{
    private static readonly string[] AportesObligatorios =
    [
        WellKnownConceptCodes.HealthEmployee, WellKnownConceptCodes.PensionEmployee, WellKnownConceptCodes.SolidarityFund,
    ];

    /// <summary>Rubros con retención propia: no entran a la depuración del ingreso ordinario.</summary>
    private static readonly string[] ConRetencionPropia =
    [
        WellKnownConceptCodes.ServiceBonus, WellKnownConceptCodes.Severance, WellKnownConceptCodes.SeveranceInterest,
        WellKnownConceptCodes.Indemnity, WellKnownConceptCodes.RetirementBonus,
    ];

    /// <summary>Las líneas cuya cantidad son los días que el pago ordinario cubre (salario pendiente, disfrute, compensación); el auxilio repite los del salario y no se cuenta.</summary>
    private static readonly string[] ConDiasPagados =
    [
        WellKnownConceptCodes.PendingSalary, WellKnownConceptCodes.VacationPayout, WellKnownConceptCodes.VacationCompensation,
    ];

    // ------------------------------------------------------------------ prima --

    public static void ServiceBonus(SettlementContext ctx)
    {
        var prima = ctx.LineAmount(WellKnownConceptCodes.ServiceBonus);
        if (prima <= 0m) return;
        var def = ctx.Concept(WellKnownConceptCodes.WithholdingOnServiceBonus);
        if (def is null) return;

        // Sin aportes (la prima no cotiza) y sin deducciones declaradas: sólo la renta exenta legal (art. 385).
        var depuracion = DepuracionDeRetencion.Depurar(prima, 0m, [], ctx.Parameters, 1m, ctx.Policies.RetefteTopesAnualesModo, ctx.Input.WithholdingYearToDate);
        var exp = new Explanation
        {
            Form = ctx.Employee.WithholdingProcedure == 2 ? "Retención de la prima (procedimiento 2, ET art. 386)" : "Retención de la prima (procedimiento 1, ET art. 385)",
            Base = new ExplanationBase("Prima de servicios depurada", depuracion.Base),
        };
        exp.Note("Norma", ctx.Employee.WithholdingProcedure == 2
            ? "La prima se suma a los pagos gravables del mes y se le aplica el porcentaje fijo del empleado; como se paga aparte, aquí se calcula sobre la prima depurada."
            : "En el procedimiento 1 la prima se retiene de forma independiente: el valor es el que corresponde al intervalo de la prima sola, sin sumarla al salario del mes.");
        exp.Steps.AddRange(depuracion.Steps);

        var linea = PorProcedimiento(ctx, def, depuracion.Base, exp);
        if (linea is not null) ctx.Add(linea);
    }

    // --------------------------------------------------- cesantías e intereses --

    /// <param name="severancePaidDirectly">Verdadero en la definitiva (las cesantías se pagan al trabajador); falso en la anual (van al fondo).</param>
    public static void SeveranceAndInterest(SettlementContext ctx, bool severancePaidDirectly)
    {
        var cesantias = ctx.LineAmount(WellKnownConceptCodes.Severance);
        var intereses = ctx.LineAmount(WellKnownConceptCodes.SeveranceInterest);
        if (cesantias <= 0m && intereses <= 0m) return;
        var def = ctx.Concept(WellKnownConceptCodes.WithholdingOnSeverance);
        if (def is null) return;

        var uvt = ctx.Parameters.Value(LegalParameterCodes.Uvt);
        var topeUvt = ctx.Parameters.Value(SettlementParameterCodes.SeveranceExemptionCapUvt);
        var pTope = ctx.Parameters.Describe(SettlementParameterCodes.SeveranceExemptionCapUvt);
        var (ingreso, textoIngreso) = ctx.MonthlyIncome(6);
        var ingresoUvt = ingreso / uvt;

        var exp = new Explanation
        {
            Form = "Retención de cesantías e intereses (ET art. 206 num. 4)",
            Base = new ExplanationBase("Ingreso laboral mensual promedio de los seis últimos meses", ingreso),
        };
        exp.Step($"Ingreso mensual promedio de los seis últimos meses ({textoIngreso})", ingreso);
        exp.Step($"En UVT (1 UVT = {Fmt.Money(uvt)})", ingresoUvt);

        decimal fraccionGravada;
        if (ingresoUvt <= topeUvt)
        {
            fraccionGravada = 0m;
            exp.Note("Exención", $"El ingreso promedio ({Fmt.Num(ingresoUvt)} UVT) no supera {Fmt.Num(topeUvt)} UVT ({pTope.Code}): cesantías e intereses exentos en su totalidad.");
        }
        else
        {
            var tabla = ctx.Parameters.Table(SettlementParameterCodes.SeveranceTaxableTableUvt);
            var busqueda = RangeTableLookup.Find(tabla, ingreso, ctx.Parameters);
            exp.Parameter = new ExplanationParameter(tabla.Code, tabla.ValidFrom, null);
            if (!string.IsNullOrWhiteSpace(tabla.Source)) exp.Note("Tabla", tabla.Source);
            var noGravada = busqueda.Rate;
            fraccionGravada = 1m - noGravada;
            exp.Range = busqueda.ExplanationRange;
            exp.Note("Tramo", busqueda.Found
                ? $"{busqueda.RangeText()}: porcentaje NO gravado {Fmt.Pct(noGravada)}; gravado {Fmt.Pct(fraccionGravada)}."
                : $"El ingreso ({Fmt.Num(ingresoUvt)} UVT) no cae en ningún tramo de {tabla.Code}: se toma como gravado en su totalidad.");
            if (!busqueda.Found) fraccionGravada = 1m;
        }

        var total = 0m;
        if (cesantias > 0m)
        {
            if (severancePaidDirectly)
                total += Rubro(ctx, exp, "Cesantías pagadas al trabajador", cesantias, fraccionGravada);
            else
                exp.Note("Cesantías", $"Las cesantías ({Fmt.Money(cesantias)}) se consignan al fondo: la retención, si la hay, la practica quien las pague al trabajador (ET art. 206 num. 4).");
        }
        if (intereses > 0m)
            total += Rubro(ctx, exp, "Intereses a las cesantías", intereses, fraccionGravada);

        exp.Step("Retención de cesantías e intereses", total);
        exp.Factor = fraccionGravada;
        exp.Summary = $"Gravado {Fmt.Pct(fraccionGravada)}: {Fmt.Money(total)}";
        ctx.Add(LineFactory.Create(def, total, exp, baseAmount: ingreso, factor: fraccionGravada, parameterCode: SettlementParameterCodes.SeveranceTaxableTableUvt));
    }

    private static decimal Rubro(SettlementContext ctx, Explanation exp, string nombre, decimal valor, decimal fraccionGravada)
    {
        var gravado = valor * fraccionGravada;
        exp.Step($"{nombre}: {Fmt.Money(valor)} × {Fmt.Pct(fraccionGravada)} gravado", gravado);
        if (gravado <= 0m) return 0m;
        var retencion = Tabla383(ctx, gravado, exp, $"{nombre} gravados");
        return retencion;
    }

    // -------------------------------------------------- indemnización y bono --

    public static void SeverancePay(SettlementContext ctx)
    {
        var indemnizacion = ctx.LineAmount(WellKnownConceptCodes.Indemnity) + ctx.LineAmount(WellKnownConceptCodes.RetirementBonus);
        if (indemnizacion <= 0m) return;
        var def = ctx.Concept(WellKnownConceptCodes.WithholdingOnIndemnity);
        if (def is null) return;

        var uvt = ctx.Parameters.Value(LegalParameterCodes.Uvt);
        var topeUvt = ctx.Parameters.Value(SettlementParameterCodes.SeverancePayWithholdingCapUvt);
        var pTope = ctx.Parameters.Describe(SettlementParameterCodes.SeverancePayWithholdingCapUvt);
        var (ingreso, textoIngreso) = ctx.MonthlyIncome(1);
        var ingresoUvt = ingreso / uvt;

        var exp = new Explanation
        {
            Form = "Retención de la indemnización (ET art. 401-3)",
            Base = new ExplanationBase("Indemnización y bonificación por retiro", indemnizacion),
        };
        exp.Step("Indemnización y bonificación por retiro", indemnizacion);
        exp.Step($"Ingreso mensual del trabajador ({textoIngreso})", ingreso);
        exp.Step($"En UVT (1 UVT = {Fmt.Money(uvt)}); tope {Fmt.Num(topeUvt)} UVT ({pTope.Code}, vigente desde {Fmt.Date(pTope.ValidFrom)})", ingresoUvt);

        if (ingresoUvt <= topeUvt)
        {
            exp.Note("Norma", $"El ingreso mensual no supera {Fmt.Num(topeUvt)} UVT: la indemnización no está sometida a retención.");
            exp.Summary = "Indemnización sin retención: ingreso mensual bajo el tope";
            ctx.Add(LineFactory.Create(def, 0m, exp, baseAmount: indemnizacion, parameterCode: pTope.Code));
            return;
        }

        var pctExenta = ctx.Parameters.Fraction(LegalParameterCodes.WithholdingExemptIncomePct);
        var exenta = indemnizacion * pctExenta;
        exp.Step($"Renta exenta del {Fmt.Pct(pctExenta)} ({LegalParameterCodes.WithholdingExemptIncomePct})", exenta);
        var usado = ctx.Policies.RetefteTopesAnualesModo == ModoDeTopesAnuales.Acumulado ? (ctx.Input.WithholdingYearToDate?.RentaExentaUsada ?? 0m) : 0m;
        var cupo = Math.Max(0m, ctx.Parameters.Value(SettlementParameterCodes.WithholdingExemptIncomeAnnualCapUvt) * uvt - usado);
        exp.Step($"Cupo anual de la renta exenta ({SettlementParameterCodes.WithholdingExemptIncomeAnnualCapUvt} × UVT" + (usado > 0m ? $" − usado en el año {Fmt.Money(usado)}" : string.Empty) + ")", cupo);
        if (exenta > cupo)
        {
            exp.Step("Renta exenta limitada al cupo anual", cupo);
            exenta = cupo;
        }
        var gravado = indemnizacion - exenta;
        exp.Step("Base gravada", gravado);
        var tarifa = ctx.Parameters.Fraction(SettlementParameterCodes.SeverancePayWithholdingPct);
        var pTarifa = ctx.Parameters.Describe(SettlementParameterCodes.SeverancePayWithholdingPct);
        var valor = gravado * tarifa;
        exp.Step($"Base gravada × {Fmt.Pct(tarifa)} ({pTarifa.Code}, vigente desde {Fmt.Date(pTarifa.ValidFrom)})", valor);
        valor = RangeTableLookup.RoundToParameterMultiple(valor, LegalParameterCodes.WithholdingRoundingMultiple, ctx.Parameters, exp);
        exp.Factor = tarifa;
        exp.Parameter = pTarifa;
        exp.Summary = $"{Fmt.Money(gravado)} × {Fmt.Pct(tarifa)} = {Fmt.Money(valor)}";
        ctx.Add(LineFactory.Create(def, valor, exp, baseAmount: gravado, factor: tarifa, parameterCode: pTarifa.Code));
    }

    // -------------------------------------------- vacaciones y salario pendiente --

    public static void OrdinaryIncome(SettlementContext ctx)
    {
        var bruto = ctx.Lines
            .Where(l => l.Nature == ConceptNature.Earning
                        && !ConRetencionPropia.Contains(l.Code, StringComparer.OrdinalIgnoreCase)
                        && ctx.Concepts.Find(l.Code) is { AffectsWithholdingBase: true })
            .Sum(l => l.Amount);
        if (bruto <= 0m) return;
        var def = ctx.Concept(WellKnownConceptCodes.Withholding);
        if (def is null) return;

        var aportes = ctx.Lines
            .Where(l => l.Nature == ConceptNature.Deduction && AportesObligatorios.Contains(l.Code, StringComparer.OrdinalIgnoreCase))
            .Sum(l => l.Amount);

        // Los topes mensuales en UVT se proporcionan a los días que este pago cubre, con tope de un
        // mes (D-29): es la misma regla con que la nómina ordinaria proporciona los suyos a los días
        // del período (ModoDeTopesAnuales.Mensualizado). Con proporción 1 fija, la liquidación de
        // vacaciones de un mes sumaba a la ordinaria de ese mes un segundo mes entero de topes de
        // vivienda, prepagada, dependientes y renta exenta. En modo Acumulado la exenta y el tope
        // global ya van por cupo anual; la proporción sólo toca los topes por rubro.
        var diasPagados = ctx.Lines
            .Where(l => l.Nature == ConceptNature.Earning && ConDiasPagados.Contains(l.Code, StringComparer.OrdinalIgnoreCase))
            .Sum(l => l.Quantity ?? 0m);
        var proporcion = diasPagados > 0m ? Math.Min(1m, diasPagados / CalendarConventions.DaysPerMonth) : 1m;

        var depuracion = DepuracionDeRetencion.Depurar(bruto, aportes, ctx.Employee.TaxDeductions, ctx.Parameters, proporcion,
            ctx.Policies.RetefteTopesAnualesModo, ctx.Input.WithholdingYearToDate);

        var exp = new Explanation
        {
            Form = ctx.Employee.WithholdingProcedure == 2 ? "Base × porcentaje (procedimiento 2)" : "Tabla por rangos",
            Base = new ExplanationBase(Bases.Label(CalculationBase.WithholdingBase), depuracion.Base),
        };
        exp.Note("Alcance", "Pago laboral ordinario de esta liquidación (salario pendiente, auxilio, vacaciones): se depura aparte; la prima, las cesantías y la indemnización llevan su propia retención.");
        exp.Step($"Días que cubre este pago ({Fmt.Num(diasPagados)}) / {CalendarConventions.DaysPerMonth}: proporción de los topes mensuales en UVT", proporcion);
        exp.Steps.AddRange(depuracion.Steps);
        if (!depuracion.HasDeclaredItems)
            exp.Note("Depuraciones declaradas", "Ninguna: si el empleado tiene intereses de vivienda, medicina prepagada o dependientes, faltó registrarlos.");

        var linea = PorProcedimiento(ctx, def, depuracion.Base, exp);
        if (linea is not null) ctx.Add(linea);
    }

    // ---------------------------------------------------------------- comunes --

    /// <summary>Procedimiento 1: tabla del art. 383 sobre la base; procedimiento 2: la base por el porcentaje fijo del empleado.</summary>
    private static CalculationLine? PorProcedimiento(SettlementContext ctx, PayrollConceptDefinition def, decimal baseDepurada, Explanation exp)
    {
        if (ctx.Employee.WithholdingProcedure == 2)
        {
            if (ctx.Employee.WithholdingRatePercent is not { } tasa)
            {
                ctx.Flags |= SettlementFlags.WithholdingRateMissing;
                ctx.Refuse($"Procedimiento 2 sin porcentaje de retención vigente al {Fmt.Date(ctx.Cutoff)}: la retención ({def.Code}) no se calculó. " +
                           "Registre el porcentaje del semestre en la ficha del empleado.");
                return null;
            }
            var fraccion = tasa / 100m;
            exp.Step("Porcentaje fijo del empleado (procedimiento 2, vigente para la fecha de corte)", tasa);
            var valor = baseDepurada * fraccion;
            exp.Step("Base depurada × porcentaje", valor);
            valor = RangeTableLookup.RoundToParameterMultiple(valor, LegalParameterCodes.WithholdingRoundingMultiple, ctx.Parameters, exp);
            exp.Factor = fraccion;
            exp.Summary = $"{Fmt.Money(baseDepurada)} × {Fmt.Pct(fraccion)} = {Fmt.Money(valor)}";
            return LineFactory.Create(def, valor, exp, baseAmount: baseDepurada, factor: fraccion);
        }

        var tablaCode = def.TableParameterCode ?? LegalParameterCodes.WithholdingTableUvt;
        var tabla = ctx.Parameters.Table(tablaCode);
        var busqueda = RangeTableLookup.Find(tabla, baseDepurada, ctx.Parameters);
        exp.Parameter ??= new ExplanationParameter(tabla.Code, tabla.ValidFrom, null);
        RangeTableLookup.Explain(busqueda, exp);
        var retencion = busqueda.Found
            ? RangeTableLookup.RoundToParameterMultiple(busqueda.Value, LegalParameterCodes.WithholdingRoundingMultiple, ctx.Parameters, exp)
            : 0m;
        exp.Factor = busqueda.Rate;
        exp.Summary = busqueda.Found
            ? $"{Fmt.Num(busqueda.BaseInUnits)} {busqueda.UnitName} → tramo desde {Fmt.Num(busqueda.Range!.FromValue)}: {Fmt.Money(retencion)}"
            : $"{def.Name}: no aplica";
        return LineFactory.Create(def, retencion, exp, baseAmount: baseDepurada, factor: busqueda.Rate,
            rangeFrom: busqueda.Range?.FromValue, rangeTo: busqueda.Range?.ToValue, legalParameterId: tabla.Id, parameterCode: tabla.Code);
    }

    /// <summary>Tabla del art. 383 sobre un valor gravado, con aproximación, dejando los pasos en la explicación dada.</summary>
    private static decimal Tabla383(SettlementContext ctx, decimal gravado, Explanation exp, string etiqueta)
    {
        var tabla = ctx.Parameters.Table(LegalParameterCodes.WithholdingTableUvt);
        var busqueda = RangeTableLookup.Find(tabla, gravado, ctx.Parameters);
        exp.Step($"{etiqueta} en {busqueda.UnitName}", busqueda.BaseInUnits);
        exp.Note("Tramo", busqueda.RangeText());
        if (!busqueda.Found) return 0m;
        var valor = busqueda.Value;
        exp.Step(tabla.RangeIsMarginal ? $"{etiqueta}: (exceso × tarifa + fijo) × UVT" : $"{etiqueta}: base × tarifa", valor);
        return RangeTableLookup.RoundToParameterMultiple(valor, LegalParameterCodes.WithholdingRoundingMultiple, ctx.Parameters, exp);
    }
}
