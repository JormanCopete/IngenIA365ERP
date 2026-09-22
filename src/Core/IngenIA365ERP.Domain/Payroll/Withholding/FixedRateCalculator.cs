using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Withholding;

/// <summary>
/// El porcentaje fijo de retención del procedimiento 2 (ET art. 386; feature 010, US7;
/// research R8): suma de los pagos gravables de los doce meses anteriores menos los aportes
/// obligatorios reales, depurada con la misma norma de la nómina ordinaria
/// (<see cref="DepuracionDeRetencion"/>) en la secuencia que diga la política, dividida por
/// <c>RETEFTE_P2_DIVISOR</c> —o por los meses de vinculación cuando son menos de doce—,
/// llevada a la tabla marginal vigente (o la del plan) y expresada como retención teórica ÷
/// base promedio × 100 (Oficio DIAN 68292/2013). Cada paso queda en la explicación.
/// </summary>
public static class FixedRateCalculator
{
    public static FixedRateResult Calculate(FixedRateInput input)
    {
        var p = input.Parameters;
        var pasos = new List<ExplanationStep>();
        var meses = input.Months.Where(m => m.GrossIncome > 0m || m.MandatoryContributions > 0m).OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();
        if (meses.Count == 0) throw new InvalidOperationException("El cálculo del porcentaje fijo exige al menos un mes con historia.");

        foreach (var m in meses)
            pasos.Add(new ExplanationStep($"{m.Year}-{m.Month:00}: ingreso gravable{(m.IncludedSpecialRuns ? " (incluye liquidación especial)" : string.Empty)}", m.GrossIncome,
                $"aportes obligatorios {Fmt.Money(m.MandatoryContributions)}; corridas: {string.Join(", ", m.SourceRuns.Select(r => $"{r.Kind} v{r.Version} {Fmt.Money(r.Amount)}"))}"));

        var totalBruto = meses.Sum(m => m.GrossIncome);
        var totalAportes = meses.Sum(m => m.MandatoryContributions);
        pasos.Add(new ExplanationStep($"Sumatoria de los {meses.Count} meses", totalBruto, $"aportes obligatorios {Fmt.Money(totalAportes)}"));

        // Divisor: el del artículo cuando hay doce meses; los meses de vinculación cuando son menos.
        var divisorLegal = p.Value(WithholdingRateParameterCodes.Procedure2Divisor);
        var d = p.Describe(WithholdingRateParameterCodes.Procedure2Divisor);
        decimal divisor; string fuenteDivisor;
        if (meses.Count >= 12)
        {
            divisor = divisorLegal; fuenteDivisor = WithholdingRateParameterCodes.Procedure2Divisor;
            pasos.Add(new ExplanationStep($"Divisor: {Fmt.Num(divisorLegal)} ({d.Code}, vigente desde {Fmt.Date(d.ValidFrom)}; la prima es el mes trece)", divisor, null));
        }
        else
        {
            divisor = meses.Count; fuenteDivisor = "MesesDeVinculacion";
            pasos.Add(new ExplanationStep($"Divisor: {meses.Count} meses de vinculación (menos de doce con historia)", divisor, null));
        }

        Depuration depuracion;
        decimal promedio;
        if (input.Sequence == DepurationSequence.DepurateThenDivide)
        {
            // Las deducciones declaradas son mensuales: para la sumatoria se llevan a los meses considerados, y los topes en UVT también.
            var deduccionesAnuales = input.DeclaredDeductions.Select(x => x with { MonthlyAmount = x.MonthlyAmount is { } ma ? ma * meses.Count : null }).ToList();
            depuracion = DepuracionDeRetencion.Depurar(totalBruto, totalAportes, deduccionesAnuales, p, meses.Count, input.TopesMode);
            promedio = depuracion.Base / divisor;
            pasos.Add(new ExplanationStep("Secuencia: depurar la sumatoria y luego dividir (DepurarLuegoDividir)", null, null));
            pasos.Add(new ExplanationStep("Base depurada de la sumatoria", depuracion.Base, null));
            pasos.Add(new ExplanationStep($"Base mensual promedio: {Fmt.Money(depuracion.Base)} ÷ {Fmt.Num(divisor)}", promedio, null));
        }
        else
        {
            var brutoPromedio = totalBruto / divisor;
            var aportesPromedio = totalAportes / divisor;
            pasos.Add(new ExplanationStep("Secuencia: dividir la sumatoria y depurar el promedio (DividirLuegoDepurar)", null, null));
            pasos.Add(new ExplanationStep($"Ingreso mensual promedio: {Fmt.Money(totalBruto)} ÷ {Fmt.Num(divisor)}", brutoPromedio, $"aportes promedio {Fmt.Money(aportesPromedio)}"));
            depuracion = DepuracionDeRetencion.Depurar(brutoPromedio, aportesPromedio, input.DeclaredDeductions, p, 1m, input.TopesMode);
            promedio = depuracion.Base;
            pasos.Add(new ExplanationStep("Base mensual promedio depurada", promedio, null));
        }

        var uvt = p.Value(WithholdingRateParameterCodes.Uvt);
        var u = p.Describe(WithholdingRateParameterCodes.Uvt);
        var promedioUvt = uvt > 0m ? promedio / uvt : 0m;
        pasos.Add(new ExplanationStep($"Base promedio en UVT ({u.Code} {Fmt.Money(uvt)}, vigente desde {Fmt.Date(u.ValidFrom)})", promedioUvt, null));

        var tramo = RangeTableLookup.Find(input.Table, promedio, p);
        var teorica = Math.Max(0m, tramo.Value);
        pasos.Add(new ExplanationStep($"Retención teórica según {input.Table.Code}{(input.PlanTableUsed ? " (tabla del plan)" : string.Empty)}: {tramo.RangeText()}", teorica, null));

        var porcentaje = promedio > 0m ? Math.Round(teorica / promedio * 100m, 2, MidpointRounding.AwayFromZero) : 0m;
        pasos.Add(new ExplanationStep($"Porcentaje fijo: {Fmt.Money(teorica)} ÷ {Fmt.Money(promedio)} × 100, a dos decimales", porcentaje, null));

        // Lo declarado que entró (vivienda, prepagada, dependientes): los pasos de la depuración lo nombran así; el paso
        // «Dependientes: x % del ingreso» es informativo y no suma.
        var deduccionesTotales = depuracion.Steps
            .Where(s => (s.Label.StartsWith("Intereses de vivienda", StringComparison.Ordinal) || s.Label.StartsWith("Medicina prepagada", StringComparison.Ordinal) || s.Label.StartsWith("Dependientes", StringComparison.Ordinal))
                        && !s.Label.Contains("% del ingreso", StringComparison.Ordinal))
            .Sum(s => s.Value ?? 0m);
        var exenta = depuracion.Steps.Where(s => s.Label.StartsWith("Renta exenta", StringComparison.Ordinal)).Select(s => s.Value ?? 0m).LastOrDefault();

        return new FixedRateResult(
            meses.Count, divisor, fuenteDivisor, totalBruto, totalAportes, deduccionesTotales, exenta,
            depuracion.Base, promedio, uvt, promedioUvt, teorica, porcentaje, input.Sequence,
            pasos, depuracion.Steps, input.Table.Code, input.Table.ValidFrom, tramo.RangeText());
    }
}
