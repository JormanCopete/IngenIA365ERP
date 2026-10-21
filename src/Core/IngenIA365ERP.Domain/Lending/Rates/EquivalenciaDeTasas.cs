using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;

namespace IngenIA365ERP.Domain.Lending.Rates;

/// <summary>
/// De la efectiva anual (EA) a la tasa del día y a la del período (feature 013, T26, R34, M5; FR-010, FR-020; D1). Sólo
/// <c>decimal</c>, sobre <see cref="AritmeticaDecimal"/>.
///
/// <list type="bullet">
/// <item><b>Compuesta</b> (defecto): <c>r_d = RaizEntera(1 + EA, B) − 1</c>, redondeada a <see cref="DecimalesDeTasa"/> decimales:
/// <b>la única aproximación del motor</b>, y queda escrita en la explicación. B es la base de la convención (360 en 30/360 y
/// real/360, 365 en real/365). La tasa de n días es <c>(1 + r_d)^n − 1</c>: un período completo da exactamente la tasa del
/// período.</item>
/// <item><b>Lineal</b> (D1, la valida la contadora): <c>NA_m = m·((1 + EA)^(1/m) − 1)</c> con m los períodos del año de la
/// periodicidad, <c>r_d = NA_m / B</c> y la tasa de n días <c>r_d·n</c>. Un período comercial completo da lo mismo que la compuesta;
/// los períodos irregulares y las fracciones, no.</item>
/// </list>
///
/// <para>La nominal del período (FR-020, informativa) va por la misma vía: <c>(1 + EA)^(1/m) − 1</c>. Pura: sin IO, sin reloj y sin
/// valores legales (la usura y los índices llegan por parámetro). (nuevo)</para>
/// </summary>
public static class EquivalenciaDeTasas
{
    /// <summary>Decimales de una tasa derivada (diaria, nominal, efectiva): los de <c>TasaEquivalente</c> (28,18).</summary>
    public const int DecimalesDeTasa = 18;

    /// <summary>Decimales máximos de una tasa pactada como fracción (<c>TasaAnual</c>, 9,6).</summary>
    public const int DecimalesDeTasaPactada = 6;

    /// <summary>El código con que se rechaza una tasa pactada con más de seis decimales de fracción (data-model §0.4).</summary>
    public const string CodigoTasaDemasiadoPrecisa = "Lending.Rate.TooPrecise";

    /// <summary>La tasa diaria de una EA en la convención y la equivalencia selladas.</summary>
    public static decimal TasaDiaria(decimal efectivaAnual, DayCountConvention convencion, RateEquivalence equivalencia,
        RepaymentPeriodicity periodicidad)
    {
        ValidarEfectiva(efectivaAnual);
        var b = ContadorDeDias.Base(convencion);
        return equivalencia switch
        {
            RateEquivalence.Compound => Redondear(AritmeticaDecimal.RaizEntera(1m + efectivaAnual, b) - 1m),
            RateEquivalence.Linear => Redondear(NominalAnualSinRedondear(efectivaAnual, periodicidad) / b),
            _ => throw new ArgumentOutOfRangeException(nameof(equivalencia), equivalencia, "Equivalencia de tasas desconocida."),
        };
    }

    /// <summary>
    /// La tasa de <paramref name="dias"/> días: compuesta <c>(1 + r_d)^n − 1</c> (exacta en decimal) o lineal <c>r_d·n</c>. No se
    /// redondea: el redondeo es de los montos (<see cref="RedondeoDeCartera"/>).
    /// </summary>
    public static decimal TasaDelPeriodo(decimal tasaDiaria, int dias, RateEquivalence equivalencia)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dias);
        return equivalencia switch
        {
            RateEquivalence.Compound => AritmeticaDecimal.Potencia(1m + tasaDiaria, dias) - 1m,
            RateEquivalence.Linear => tasaDiaria * dias,
            _ => throw new ArgumentOutOfRangeException(nameof(equivalencia), equivalencia, "Equivalencia de tasas desconocida."),
        };
    }

    /// <summary>
    /// Períodos del año de una periodicidad (el m de la nominal): 360 / días cuando el año comercial se divide exacto (decadal 36,
    /// quincenal 24, mensual 12… anual 1); semanal, las semanas enteras del año real (365 / 7 = 52); pago único, 1. Los días
    /// salen del valor del enum, nunca de un literal (CH39).
    /// </summary>
    public static int PeriodosPorAnio(RepaymentPeriodicity periodicidad)
    {
        var dias = (int)periodicidad;
        if (periodicidad == RepaymentPeriodicity.SinglePayment) return 1;
        if (dias <= 0) throw new ArgumentOutOfRangeException(nameof(periodicidad), periodicidad, "Periodicidad desconocida.");
        return ContadorDeDias.AnioComercial % dias == 0 ? ContadorDeDias.AnioComercial / dias : ContadorDeDias.AnioReal / dias;
    }

    /// <summary>La tasa nominal del período equivalente a la EA: <c>(1 + EA)^(1/m) − 1</c>, a 18 decimales (FR-020, informativa).</summary>
    public static decimal NominalDelPeriodo(decimal efectivaAnual, RepaymentPeriodicity periodicidad)
    {
        ValidarEfectiva(efectivaAnual);
        return Redondear(AritmeticaDecimal.RaizEntera(1m + efectivaAnual, PeriodosPorAnio(periodicidad)) - 1m);
    }

    /// <summary>La nominal anual pagadera por período: <c>NA_m = m·((1 + EA)^(1/m) − 1)</c>, a 18 decimales (D1).</summary>
    public static decimal NominalAnual(decimal efectivaAnual, RepaymentPeriodicity periodicidad)
    {
        ValidarEfectiva(efectivaAnual);
        return Redondear(NominalAnualSinRedondear(efectivaAnual, periodicidad));
    }

    /// <summary>
    /// La EA de una tasa nominal capitalizada <paramref name="periodosPorAnio"/> veces al año, vencida: <c>(1 + N/m)^m − 1</c>, a 18
    /// decimales. Es la conversión de un índice expresado como nominal (IBR) después de sumarle el margen (D10).
    /// </summary>
    public static decimal EfectivaDeNominal(decimal nominalAnual, int periodosPorAnio)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(periodosPorAnio, 1);
        return Redondear(AritmeticaDecimal.Potencia(1m + nominalAnual / periodosPorAnio, periodosPorAnio) - 1m);
    }

    /// <summary>La efectiva de <paramref name="dias"/> días de una tasa diaria compuesta: <c>(1 + r)^n − 1</c>, a 18 decimales.</summary>
    public static decimal EfectivaDeTasaDiaria(decimal tasaDiaria, int dias) =>
        Redondear(AritmeticaDecimal.Potencia(1m + tasaDiaria, dias) - 1m);

    /// <summary>¿Cabe la tasa pactada en seis decimales de fracción? Si no, se rechaza (<see cref="CodigoTasaDemasiadoPrecisa"/>), nunca se redondea.</summary>
    public static bool EsTasaPactable(decimal tasa) => decimal.Round(tasa, DecimalesDeTasaPactada) == tasa;

    /// <summary>Redondeo de una tasa derivada a 18 decimales, al par (la regla de <see cref="AritmeticaDecimal"/>).</summary>
    public static decimal Redondear(decimal tasa) => decimal.Round(tasa, DecimalesDeTasa, MidpointRounding.ToEven);

    private static decimal NominalAnualSinRedondear(decimal efectivaAnual, RepaymentPeriodicity periodicidad)
    {
        var m = PeriodosPorAnio(periodicidad);
        return m * (AritmeticaDecimal.RaizEntera(1m + efectivaAnual, m) - 1m);
    }

    private static void ValidarEfectiva(decimal efectivaAnual)
    {
        if (efectivaAnual < 0m)
            throw new ArgumentOutOfRangeException(nameof(efectivaAnual), efectivaAnual, "La efectiva anual no puede ser negativa.");
    }
}
