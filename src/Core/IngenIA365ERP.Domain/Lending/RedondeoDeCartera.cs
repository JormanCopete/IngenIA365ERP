using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending;

/// <summary>
/// El redondeo sellado de una línea y de su crédito (feature 013, T35, R44, M15; FR-010; data-model §0.4): <b>unidad</b> (0,01; 1;
/// 10; 100; 1.000), <b>modo</b> y <b>momento</b>. El motor no redondea en ningún otro punto: las tasas derivadas van a 18 decimales
/// (<c>EquivalenciaDeTasas</c>) y los montos pasan por aquí.
///
/// <list type="bullet">
/// <item><see cref="RoundingMode.AwayFromZero"/>: el medio se aleja de cero; <see cref="RoundingMode.ToEven"/>: el medio va al par;
/// <see cref="RoundingMode.Up"/>: hacia arriba (techo); <see cref="RoundingMode.Down"/>: hacia abajo (piso).</item>
/// <item>Momento en el plan (<c>GeneradorDePlan</c>): <see cref="RoundingMoment.PerConcept"/> redondea a la unidad cada concepto
/// (capital, interés, seguro, la cuota nivelada); <see cref="RoundingMoment.PerInstallment"/> deja los conceptos al centavo y lleva a
/// la unidad el total de la cuota, con el residuo a capital; <see cref="RoundingMoment.PerTotal"/> deja todo el plan al centavo y la
/// unidad se aplica al total de una liquidación (US4, US5).</item>
/// </list>
///
/// <para>En los tres el residuo queda visible: el plan dice en su explicación lo que absorbió la última cuota. Puro. (nuevo)</para>
/// </summary>
public sealed record RedondeoDeCartera
{
    /// <summary>El centavo: la unidad mínima de un monto (<c>Monto</c>, 18,2).</summary>
    public static decimal Centavo { get; } = new(1, 0, 0, false, 2);

    /// <summary>Las unidades admitidas: 0,01; 1; 10; 100; 1.000.</summary>
    public static IReadOnlyList<decimal> UnidadesAdmitidas { get; } = [Centavo, 1m, new decimal(10), 100m, new decimal(1000)];

    /// <summary>Al centavo, alejándose de cero, por concepto: el defecto cuando la línea no dice otra cosa.</summary>
    public static RedondeoDeCartera AlCentavo { get; } = new(Centavo, RoundingMode.AwayFromZero, RoundingMoment.PerConcept);

    /// <summary>
    /// K6b (T1054; data-model §0.4, alias <c>ValorUvr</c> 18,4): la diezmilésima, la unidad mínima de un valor en UVR. No es una unidad que una línea
    /// pueda sellar para pesos: sólo la usa <see cref="EnUvr"/>.
    /// </summary>
    public static decimal DiezMilesima { get; } = new(1, 0, 0, false, 4);

    public RedondeoDeCartera(decimal unidad, RoundingMode modo, RoundingMoment momento)
        : this(unidad, modo, momento, Centavo)
    {
        if (!UnidadesAdmitidas.Contains(unidad))
            throw new ArgumentOutOfRangeException(nameof(unidad), unidad, "La unidad de redondeo es 0,01; 1; 10; 100 o 1.000.");
    }

    private RedondeoDeCartera(decimal unidad, RoundingMode modo, RoundingMoment momento, decimal minima)
    {
        if (!Enum.IsDefined(modo)) throw new ArgumentOutOfRangeException(nameof(modo), modo, "Modo de redondeo desconocido.");
        if (!Enum.IsDefined(momento)) throw new ArgumentOutOfRangeException(nameof(momento), momento, "Momento de redondeo desconocido.");
        Unidad = unidad;
        Modo = modo;
        Momento = momento;
        Minima = minima;
    }

    /// <summary>
    /// K6b (T1054): el redondeo con que el motor lleva un crédito en UVR —plan, causación, pagos y saldos en UVR—: a la diezmilésima (alias
    /// <c>ValorUvr</c>), con el modo y el momento que selló la línea. La unidad de la línea (en pesos) rige al convertir a pesos, no aquí.
    /// </summary>
    public static RedondeoDeCartera EnUvr(RedondeoDeCartera dePesos)
    {
        ArgumentNullException.ThrowIfNull(dePesos);
        return new RedondeoDeCartera(DiezMilesima, dePesos.Modo, dePesos.Momento, DiezMilesima);
    }

    public decimal Unidad { get; }

    public RoundingMode Modo { get; }

    public RoundingMoment Momento { get; }

    /// <summary>La unidad mínima de un monto: el centavo en pesos; la diezmilésima en UVR (K6b).</summary>
    public decimal Minima { get; }

    /// <summary>¿Lleva valores en UVR (K6b)?</summary>
    public bool EsDeUvr => Minima == DiezMilesima;

    /// <summary>A la unidad sellada, con el modo sellado.</summary>
    public decimal Redondear(decimal valor) => AUnidad(valor, Unidad, Modo);

    /// <summary>A la unidad mínima (el centavo; en UVR, la diezmilésima), con el modo sellado.</summary>
    public decimal RedondearAlCentavo(decimal valor) => AUnidad(valor, Minima, Modo);

    /// <summary>Un concepto del plan (interés, seguro, extraordinaria): a la unidad si el momento es por concepto; si no, al centavo.</summary>
    public decimal Concepto(decimal valor) => Momento == RoundingMoment.PerConcept ? Redondear(valor) : RedondearAlCentavo(valor);

    /// <summary>¿Lleva a la unidad el total de cada cuota?</summary>
    public bool PorCuota => Momento == RoundingMoment.PerInstallment;

    /// <summary>
    /// <paramref name="valor"/> a un múltiplo de <paramref name="unidad"/>: <c>Round(valor / unidad) × unidad</c>. <c>Up</c> y
    /// <c>Down</c> son dirigidos (techo y piso), no de medio.
    /// </summary>
    public static decimal AUnidad(decimal valor, decimal unidad, RoundingMode modo)
    {
        if (unidad <= 0m) throw new ArgumentOutOfRangeException(nameof(unidad), unidad, "La unidad es mayor que cero.");
        var regla = modo switch
        {
            RoundingMode.AwayFromZero => MidpointRounding.AwayFromZero,
            RoundingMode.ToEven => MidpointRounding.ToEven,
            RoundingMode.Up => MidpointRounding.ToPositiveInfinity,
            RoundingMode.Down => MidpointRounding.ToNegativeInfinity,
            _ => throw new ArgumentOutOfRangeException(nameof(modo), modo, "Modo de redondeo desconocido."),
        };
        return decimal.Round(valor / unidad, 0, regla) * unidad;
    }
}
