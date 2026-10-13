using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>A qué se redondea el deterioro de cada crédito (<c>Riesgo.RedondeoDeterioro</c>, pregunta E9): al centavo o al peso.</summary>
public enum RedondeoDeDeterioro
{
    Centavo = 1,
    Peso = 2,
}

/// <summary>Una garantía del crédito para el deterioro: su naturaleza y el valor que le corresponde a este crédito.</summary>
public sealed record GarantiaParaDeterioro(NaturalezaDeGarantia Naturaleza, decimal Valor);

/// <summary>Los saldos al cierre que el deterioro mira: capital, interés corriente y de mora causados y no pagados en balance, y los otros conceptos (seguros, gastos).</summary>
public sealed record SaldosParaDeterioro(decimal Capital, decimal InteresCorriente, decimal InteresDeMora, decimal Otros);

/// <summary>
/// Lo que el deterioro individual necesita de un crédito ya calificado: segmento, categoría y subcategoría finales, días de mora (para
/// los meses de mora de las garantías y la condición del porcentaje), meses en E, saldos, si la causación está suspendida, sus
/// garantías y los aportes que le tocarían a prorrata (que no cuentan mientras rija D19).
/// </summary>
public sealed record CreditoParaDeteriorar(int LoanId, string SegmentoSarc, RatingCategory Categoria, RatingSubcategory Subcategoria, int DiasDeMora,
    SaldosParaDeterioro Saldos, bool Suspendido)
{
    public int? MesesEnE { get; init; }

    public IReadOnlyList<GarantiaParaDeterioro> Garantias { get; init; } = [];

    /// <summary>Los aportes del deudor que le tocarían a este crédito a prorrata; cero mientras rija D19.</summary>
    public decimal AportesAProrrata { get; init; }
}

/// <summary>El deterioro individual de un crédito (data-model §23.2), desglosado y con su explicación.</summary>
public sealed record DeterioroIndividual(
    int LoanId,
    int MesesDeMora,
    decimal ValorAdmisibleDeGarantias,
    decimal AportesDescontados,
    BaseDeDeterioro TipoDeBase,
    decimal Base,
    decimal Porcentaje,
    decimal DeterioroCapital,
    decimal DeterioroIntereses,
    decimal DeterioroOtros,
    ExplicacionDeCartera Explicacion)
{
    public decimal Total => DeterioroCapital + DeterioroIntereses + DeterioroOtros;
}

/// <summary>
/// El deterioro individual del Anexo 1 (feature 013, US9, T0694; T46, R55; FR-103; SC-023):
/// <list type="bullet">
/// <item><b>Base</b> (descubierto): capital − Σ valor de cada garantía × la fracción admisible de su naturaleza según los <b>meses de
/// mora</b> (días ÷ 30, meses cumplidos; tabla hipotecaria y no hipotecaria de la norma) − aportes a prorrata, nunca negativa; con
/// base «saldo total», el capital sin descontar.</item>
/// <item><b>Aportes</b>: sólo si la norma los admite, hay indicadores vigentes y <b>no rige D19</b>; mientras rija, cero, y la
/// explicación lo dice (<see cref="ContextoDeAportes"/>).</item>
/// <item><b>Porcentaje</b>: el de la política efectiva para el segmento, la categoría, la subcategoría y la condición (días o meses
/// en E).</item>
/// <item><b>Suspendido</b>: los intereses y los otros conceptos causados y no pagados (en balance) se deterioran al 100 %; lo que ya
/// está en suspenso vive en cuentas de orden y no se deteriora. Sin suspensión, el deterioro es sólo del capital.</item>
/// <item><b>Redondeo</b> (<c>Riesgo.RedondeoDeterioro</c>, E9): cada componente al centavo o al peso, alejándose de cero en el medio.</item>
/// </list>
/// Recibe la norma efectiva. Pura: sin IO, sin EF, sin valores legales. (nuevo)
/// </summary>
public static class MotorDeDeterioroIndividual
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Los días que hacen un mes de mora para las tablas de garantías.</summary>
    public const int DiasPorMesDeMora = 30;

    public static DeterioroIndividual Calcular(Anexo1 norma, CreditoParaDeteriorar c, ContextoDeAportes aportes, RedondeoDeDeterioro redondeo)
    {
        ArgumentNullException.ThrowIfNull(norma);
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(aportes);
        aportes.ExigirQueNoApliquen(norma);
        if (c.Saldos.Capital < 0m || c.Saldos.InteresCorriente < 0m || c.Saldos.InteresDeMora < 0m || c.Saldos.Otros < 0m)
            throw new ArgumentOutOfRangeException(nameof(c), "Los saldos del deterioro no son negativos.");

        var explicacion = new ExplicacionDeCartera();
        var meses = c.DiasDeMora / DiasPorMesDeMora;
        var admisible = 0m;
        foreach (var g in c.Garantias)
        {
            var fraccion = norma.FraccionAdmisible(g.Naturaleza, meses);
            var valor = g.Valor * fraccion;
            admisible += valor;
            explicacion.Linea($"Garantía {g.Naturaleza} de {N(g.Valor)} × {N(fraccion)} admisible con {meses.ToString(Inv)} meses de mora = {N(valor)}.");
        }

        var aporte = 0m;
        explicacion.Linea(c.AportesAProrrata > 0m
            ? $"Aportes a prorrata {N(c.AportesAProrrata)}: no se descuentan ({aportes.MotivoDeQueNoApliquen(norma)})."
            : $"Aportes: no se descuentan ({aportes.MotivoDeQueNoApliquen(norma)}).");

        var porcentaje = norma.Porcentaje(c.SegmentoSarc, c.Categoria, c.Subcategoria, c.DiasDeMora, c.MesesEnE);
        var baseDeDeterioro = porcentaje.Base == BaseDeDeterioro.TotalBalance
            ? c.Saldos.Capital
            : Math.Max(0m, c.Saldos.Capital - admisible - aporte);
        explicacion.Linea(porcentaje.Base == BaseDeDeterioro.TotalBalance
            ? $"Base = capital {N(c.Saldos.Capital)} (la norma pide el saldo total)."
            : $"Base = capital {N(c.Saldos.Capital)} − garantías admisibles {N(admisible)} − aportes {N(aporte)} = {N(baseDeDeterioro)}.");

        var capital = Redondear(baseDeDeterioro * porcentaje.Fraccion, redondeo);
        explicacion.Linea($"Capital: {N(baseDeDeterioro)} × {N(porcentaje.Fraccion)} ({c.SegmentoSarc} {HomologacionDeCalificacion.Texto(c.Categoria, c.Subcategoria)}) = {N(capital)}.");

        decimal intereses = 0m, otros = 0m;
        if (c.Suspendido)
        {
            intereses = Redondear(c.Saldos.InteresCorriente + c.Saldos.InteresDeMora, redondeo);
            otros = Redondear(c.Saldos.Otros, redondeo);
            explicacion.Linea($"Causación suspendida: intereses {N(intereses)} y otros conceptos {N(otros)} al 100 %.");
        }
        else
            explicacion.Linea("Causación no suspendida: intereses y otros conceptos sin deterioro individual.");

        explicacion.Linea($"Deterioro individual = {N(capital + intereses + otros)} (redondeo al {redondeo.ToString().ToLowerInvariant()}).");
        return new(c.LoanId, meses, admisible, aporte, porcentaje.Base, baseDeDeterioro, porcentaje.Fraccion, capital, intereses, otros, explicacion);
    }

    /// <summary>Al centavo o al peso, alejándose de cero en el medio.</summary>
    public static decimal Redondear(decimal valor, RedondeoDeDeterioro redondeo) => redondeo switch
    {
        RedondeoDeDeterioro.Centavo => RedondeoDeCartera.AUnidad(valor, RedondeoDeCartera.Centavo, RoundingMode.AwayFromZero),
        RedondeoDeDeterioro.Peso => RedondeoDeCartera.AUnidad(valor, 1m, RoundingMode.AwayFromZero),
        _ => throw new ArgumentOutOfRangeException(nameof(redondeo), redondeo, "Redondeo del deterioro desconocido."),
    };

    private static string N(decimal valor) => ExplicacionDeCartera.Numero(valor);
}
