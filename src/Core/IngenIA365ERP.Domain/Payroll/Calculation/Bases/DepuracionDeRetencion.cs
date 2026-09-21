using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;
using IngenIA365ERP.Domain.Payroll.Settlements;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Bases;

/// <summary>Cómo se observan los topes anuales de la depuración (política por empresa <c>RetefteTopesAnualesModo</c>, research R5).</summary>
public enum ModoDeTopesAnuales
{
    /// <summary>Los topes mensuales en UVT (790/12, 1.340/12) proporcionados a los días del pago. Es lo que siempre hizo la nómina ordinaria.</summary>
    Mensualizado = 0,

    /// <summary>El cupo anual por persona (Oficio DIAN 3966/2023): tope anual en UVT menos lo ya consumido en el año.</summary>
    Acumulado = 1,
}

/// <summary>Lo que el empleado ya consumió en el año de cada cupo anual, en pesos. Sólo importa en modo <see cref="ModoDeTopesAnuales.Acumulado"/>.</summary>
public sealed record AcumuladoAnualDeRetencion(decimal RentaExentaUsada, decimal DeduccionesYExentasUsadas)
{
    public static readonly AcumuladoAnualDeRetencion Ninguno = new(0m, 0m);
}

/// <summary>La base depurada y cada paso que la formó, para pegarlos a la explicación de la retención.</summary>
public sealed record Depuration(decimal Base, IReadOnlyList<ExplanationStep> Steps, bool HasDeclaredItems);

/// <summary>
/// La depuración de la base de retención por salarios como función pura (ET arts. 206
/// num. 10, 387 y 388; feature 010 research R5): ingreso gravado − aportes obligatorios −
/// deducciones declaradas con sus topes − rentas exentas por aportes voluntarios con su
/// tope − renta exenta del porcentaje legal con su tope, y el tope global de deducciones
/// más rentas exentas. Hasta la 010 vivía dentro de <see cref="WithholdingBaseBuilder"/>
/// atada al <c>RuleContext</c> del período; la sacamos para que la nómina ordinaria, las
/// liquidaciones especiales (prima, vacaciones, salario pendiente) y el porcentaje fijo
/// del procedimiento 2 depuren con la misma norma escrita una sola vez. Todo tope y
/// porcentaje sale del <see cref="ParameterSet"/>.
/// </summary>
public static class DepuracionDeRetencion
{
    /// <param name="bruto">Ingreso gravado del pago (lo que afecta la base de retención).</param>
    /// <param name="aportesObligatorios">Salud, pensión y fondo de solidaridad del empleado sobre ese pago (ingreso no constitutivo).</param>
    /// <param name="deduccionesDeclaradas">Lo que el empleado declaró en la ficha (vivienda, prepagada, dependientes, AFC/AVP).</param>
    /// <param name="parameters">Parámetros vigentes a la fecha del pago.</param>
    /// <param name="proporcion">Fracción de mes que representa el pago (días/30): proporciona los topes mensuales en UVT.</param>
    /// <param name="modoTopes">Cómo se observan los cupos anuales del 25 % y del 40 %/1.340 UVT.</param>
    /// <param name="acumuladoAnual">Lo consumido en el año, en modo acumulado; nulo = nada.</param>
    public static Depuration Depurar(
        decimal bruto,
        decimal aportesObligatorios,
        IReadOnlyList<TaxDeductionInput> deduccionesDeclaradas,
        ParameterSet parameters,
        decimal proporcion,
        ModoDeTopesAnuales modoTopes = ModoDeTopesAnuales.Mensualizado,
        AcumuladoAnualDeRetencion? acumuladoAnual = null)
    {
        var pasos = new List<ExplanationStep>();
        var uvt = parameters.Value(LegalParameterCodes.Uvt);
        var acumulado = acumuladoAnual ?? AcumuladoAnualDeRetencion.Ninguno;

        decimal TopeUvt(string code) => parameters.Value(code) * uvt * proporcion;

        // Cupo anual restante en pesos: tope anual en UVT × UVT − lo consumido; nunca negativo.
        decimal CupoAnual(string codeAnual, decimal usado) => Math.Max(0m, parameters.Value(codeAnual) * uvt - usado);

        // 1. Ingreso gravado del pago.
        pasos.Add(new ExplanationStep("Ingreso gravado del período", bruto, null));

        // 2. Aportes obligatorios (ingreso no constitutivo).
        pasos.Add(new ExplanationStep("− Aportes obligatorios a salud, pensión y fondo de solidaridad", aportesObligatorios, null));
        var neto1 = Math.Max(0m, bruto - aportesObligatorios);
        pasos.Add(new ExplanationStep("Ingreso neto antes de deducciones", neto1, null));

        // 3. Deducciones y rentas exentas declaradas por el empleado.
        var deducciones = 0m;
        var voluntarios = 0m;
        if (deduccionesDeclaradas.Count == 0)
        {
            pasos.Add(new ExplanationStep("Deducciones y rentas exentas declaradas", 0m,
                "Ninguna registrada en la ficha: la base se depura sólo con los aportes obligatorios y la renta exenta legal."));
        }
        foreach (var d in deduccionesDeclaradas)
        {
            var monto = d.MonthlyAmount ?? (d.Percent is { } pc ? bruto * pc / 100m : 0m);
            switch (d.Kind)
            {
                case TaxDeductionKind.HousingInterest:
                    deducciones += Con(monto, TopeUvt(LegalParameterCodes.WithholdingHousingInterestCapUvt), "Intereses de vivienda", LegalParameterCodes.WithholdingHousingInterestCapUvt, pasos);
                    break;
                case TaxDeductionKind.PrepaidHealth:
                    deducciones += Con(monto, TopeUvt(LegalParameterCodes.WithholdingPrepaidHealthCapUvt), "Medicina prepagada", LegalParameterCodes.WithholdingPrepaidHealthCapUvt, pasos);
                    break;
                case TaxDeductionKind.Dependents:
                    var pctDep = parameters.Fraction(LegalParameterCodes.WithholdingDependentsPct);
                    var porPct = bruto * pctDep;
                    var montoDep = d.MonthlyAmount is { } m ? Math.Min(m, porPct) : porPct;
                    pasos.Add(new ExplanationStep($"Dependientes: {Fmt.Pct(pctDep)} del ingreso ({LegalParameterCodes.WithholdingDependentsPct})", porPct, null));
                    deducciones += Con(montoDep, TopeUvt(LegalParameterCodes.WithholdingDependentsCapUvt), "Dependientes", LegalParameterCodes.WithholdingDependentsCapUvt, pasos);
                    break;
                case TaxDeductionKind.VoluntaryPension:
                case TaxDeductionKind.AfcSavings:
                    voluntarios += monto;
                    pasos.Add(new ExplanationStep(d.Kind == TaxDeductionKind.AfcSavings ? "Ahorro AFC declarado" : "Aporte voluntario a pensión declarado", monto, null));
                    break;
            }
        }
        if (voluntarios > 0m)
        {
            var pctVol = parameters.Fraction(LegalParameterCodes.WithholdingVoluntarySavingsPct);
            var topeVolPct = bruto * pctVol;
            var topeVolUvt = TopeUvt(LegalParameterCodes.WithholdingVoluntarySavingsCapUvt);
            var topeVol = Math.Min(topeVolPct, topeVolUvt);
            if (voluntarios > topeVol)
            {
                pasos.Add(new ExplanationStep($"Aportes voluntarios limitados al {Fmt.Pct(pctVol)} del ingreso y al tope en UVT", topeVol, null));
                voluntarios = topeVol;
            }
            else
            {
                pasos.Add(new ExplanationStep("Renta exenta por aportes voluntarios", voluntarios, null));
            }
        }

        // 4. Renta exenta legal sobre lo que queda.
        var pctExenta = parameters.Fraction(LegalParameterCodes.WithholdingExemptIncomePct);
        var baseExenta = Math.Max(0m, neto1 - deducciones - voluntarios);
        var exenta = baseExenta * pctExenta;
        pasos.Add(new ExplanationStep($"Renta exenta del {Fmt.Pct(pctExenta)} sobre {Fmt.Money(baseExenta)} ({LegalParameterCodes.WithholdingExemptIncomePct})", exenta, null));
        if (modoTopes == ModoDeTopesAnuales.Acumulado)
        {
            var cupo = CupoAnual(SettlementParameterCodes.WithholdingExemptIncomeAnnualCapUvt, acumulado.RentaExentaUsada);
            pasos.Add(new ExplanationStep($"Cupo anual restante de la renta exenta ({SettlementParameterCodes.WithholdingExemptIncomeAnnualCapUvt} − usado en el año {Fmt.Money(acumulado.RentaExentaUsada)})", cupo, null));
            if (exenta > cupo)
            {
                pasos.Add(new ExplanationStep("Renta exenta limitada al cupo anual restante", cupo, null));
                exenta = cupo;
            }
        }
        else
        {
            var topeExenta = TopeUvt(LegalParameterCodes.WithholdingExemptIncomeCapUvt);
            if (exenta > topeExenta)
            {
                pasos.Add(new ExplanationStep($"Renta exenta limitada al tope en UVT ({LegalParameterCodes.WithholdingExemptIncomeCapUvt})", topeExenta, null));
                exenta = topeExenta;
            }
        }

        // 5. Tope global: deducciones + rentas exentas.
        var totalDepuracion = deducciones + voluntarios + exenta;
        var pctGlobal = parameters.Fraction(LegalParameterCodes.WithholdingDeductionsCapPct);
        var topeGlobalUvt = modoTopes == ModoDeTopesAnuales.Acumulado
            ? CupoAnual(SettlementParameterCodes.WithholdingDeductionsAnnualCapUvt, acumulado.DeduccionesYExentasUsadas)
            : TopeUvt(LegalParameterCodes.WithholdingDeductionsCapUvt);
        var topeGlobal = Math.Min(neto1 * pctGlobal, topeGlobalUvt);
        if (totalDepuracion > topeGlobal)
        {
            pasos.Add(new ExplanationStep(modoTopes == ModoDeTopesAnuales.Acumulado
                ? $"Deducciones y rentas exentas limitadas al {Fmt.Pct(pctGlobal)} del ingreso neto y al cupo anual restante en UVT"
                : $"Deducciones y rentas exentas limitadas al {Fmt.Pct(pctGlobal)} del ingreso neto y al tope en UVT", topeGlobal, null));
            totalDepuracion = topeGlobal;
        }
        else
        {
            pasos.Add(new ExplanationStep("Total de deducciones y rentas exentas", totalDepuracion, null));
        }

        var baseDepurada = Math.Max(0m, neto1 - totalDepuracion);
        pasos.Add(new ExplanationStep("Base de retención depurada", baseDepurada, null));

        return new Depuration(baseDepurada, pasos, deduccionesDeclaradas.Count > 0);
    }

    private static decimal Con(decimal monto, decimal tope, string nombre, string codigoTope, List<ExplanationStep> pasos)
    {
        if (monto > tope)
        {
            pasos.Add(new ExplanationStep($"{nombre}: {Fmt.Money(monto)} limitado al tope ({codigoTope})", tope, null));
            return tope;
        }
        pasos.Add(new ExplanationStep(nombre, monto, null));
        return monto;
    }
}
