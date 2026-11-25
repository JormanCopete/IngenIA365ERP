using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// 2026-11-24 (feature 025, HU2; FR-007, FR-010, FR-012): la tasa pactada en mensual vencida en las pantallas. La E.A. que se muestra al lado es
/// <c>(1 + MV)^12 − 1</c>, la misma que deriva el motor (<c>EquivalenciaDeTasas.EfectivaDeMensual</c>); la validación del editor repite la de
/// <c>CreditLineVersionInputValidator</c> (Principio VIII): con MV, tasa fija, su MV con hasta cuatro decimales en porcentaje, 30/360 y lineal. (nuevo)
/// </summary>
public static class TasaMensualVencida
{
    /// <summary>La base E.A. (de fábrica), como la publica la API.</summary>
    public const int BaseEfectivaAnual = 1;

    /// <summary>La base mensual vencida.</summary>
    public const int BaseMensual = 2;

    private const int MesesDelAnio = 12;
    private const int ConvencionTreinta360 = 1;
    private const int EquivalenciaLineal = 2;
    private const int DecimalesDeLaMensual = 8;
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Las bases para el selector «Tasa en».</summary>
    public static IReadOnlyList<(int Valor, string Texto)> Bases { get; } = [(BaseEfectivaAnual, "E.A."), (BaseMensual, "Mensual vencida")];

    /// <summary>La E.A. de una mensual vencida: (1 + MV)^12 − 1.</summary>
    public static decimal EfectivaDeMensual(decimal mensual)
    {
        var factor = 1m;
        for (var i = 0; i < MesesDelAnio; i++) factor *= 1m + mensual;
        return factor - 1m;
    }

    /// <summary>La mensual vencida equivalente a una E.A.: (1 + EA)^(1/12) − 1 (para mostrar al lado de una E.A.).</summary>
    public static decimal MensualDeEfectiva(decimal efectiva) =>
        efectiva <= -1m ? 0m : (decimal)(Math.Pow((double)(1m + efectiva), 1d / MesesDelAnio) - 1d);

    /// <summary>«1,8 % MV = 23,8721 % E.A.»; sin MV, «23,8721 % E.A.».</summary>
    public static string Texto(decimal? efectivaAnual, decimal? mensual) => mensual is { } mv
        ? $"{Porcentaje(mv)} MV = {Porcentaje(EfectivaDeMensual(mv))} E.A."
        : efectivaAnual is { } ea ? $"{Porcentaje(ea)} E.A." : "—";

    /// <summary>Una fracción como porcentaje con hasta cuatro decimales (0,238721 → «23,8721 %»).</summary>
    public static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.####", Colombia) + " %";

    /// <summary>Lo que el editor no deja guardar de la tasa en MV (vacía si todo está bien o la base es E.A.).</summary>
    public static IReadOnlyList<string> Problemas(VersionDeLineaRequest v)
    {
        ArgumentNullException.ThrowIfNull(v);
        var problemas = new List<string>();
        var actual = v.Rates.Current;
        if (actual.Basis != BaseMensual) return problemas;
        if (actual.Kind != 1) problemas.Add("La tasa variable se pacta en efectiva anual.");
        if (actual.MonthlyRate is not { } mv) problemas.Add("Indicá la tasa mensual vencida.");
        else if (mv < 0m) problemas.Add("La tasa no puede ser negativa.");
        else if (decimal.Round(mv, DecimalesDeLaMensual) != mv) problemas.Add("La tasa mensual admite hasta cuatro decimales en porcentaje.");
        if (v.DayCount != ConvencionTreinta360 || v.RateEquivalence != EquivalenciaLineal)
            problemas.Add("Una tasa en mensual vencida exige la convención 30/360 y la equivalencia lineal.");
        if (v.Rates.Bands.Any(b => b.MonthlyRate is null))
            problemas.Add("Con la tasa en mensual vencida cada banda lleva su tasa mensual.");
        return problemas;
    }
}
