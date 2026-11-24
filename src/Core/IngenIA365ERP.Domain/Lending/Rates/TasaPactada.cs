using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Rates;

/// <summary>
/// 2026-11-24 (feature 025, HU2; FR-007, FR-008): la tasa corriente tal como se pactó, con su base. En E.A. el valor es la efectiva anual;
/// en MV, la mensual vencida exacta (0,018 = 1,8 % MV). La diaria sale de <see cref="EquivalenciaDeTasas.TasaDiaria(TasaPactada,
/// DayCountConvention, RateEquivalence, RepaymentPeriodicity)"/>; la E.A. que se compara con la usura y se informa, de
/// <see cref="EfectivaAnual"/> (la derivada sin redondear a seis). Pura. (nuevo)
/// </summary>
public readonly record struct TasaPactada(RateBasis Base, decimal Valor)
{
    /// <summary>Una efectiva anual.</summary>
    public static TasaPactada Efectiva(decimal efectivaAnual) => new(RateBasis.EffectiveAnnual, efectivaAnual);

    /// <summary>Una mensual vencida.</summary>
    public static TasaPactada Mensual(decimal mensual) => new(RateBasis.MonthlyNominal, mensual);

    /// <summary>La de unas condiciones: la mensual si la hay; si no, la efectiva.</summary>
    public static TasaPactada De(decimal efectivaAnual, decimal? mensual) =>
        mensual is { } mv ? Mensual(mv) : Efectiva(efectivaAnual);

    /// <summary>¿Se pactó en mensual vencida?</summary>
    public bool EsMensual => Base == RateBasis.MonthlyNominal;

    /// <summary>La mensual pactada, o nula si es E.A.</summary>
    public decimal? MensualPactada => EsMensual ? Valor : null;

    /// <summary>La E.A.: la pactada, o la derivada de la MV a 18 decimales.</summary>
    public decimal EfectivaAnual => EsMensual ? EquivalenciaDeTasas.EfectivaDeMensual(Valor) : Valor;
}
