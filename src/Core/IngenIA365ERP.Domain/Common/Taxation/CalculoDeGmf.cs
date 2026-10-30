namespace IngenIA365ERP.Domain.Common.Taxation;

/// <summary>Cómo se redondea el GMF (parámetro <c>Gmf.Redondeo</c>: <c>AlPeso</c> o <c>SinRedondeo</c>). (nuevo)</summary>
public enum RedondeoDeGmf
{
    AlPeso = 1,
    SinRedondeo = 2,
}

/// <summary>
/// El cálculo del gravamen a los movimientos financieros (feature 015, US10, T0134; R-25; FR-100, FR-205; ET art. 871), común a Tesorería y —en R1— a los
/// egresos de Recaudo. Puro: sin IO, en <c>decimal</c>. La tarifa llega como <b>fracción</b> (0,004 = 4 por mil) y la da el parámetro con vigencia
/// <c>Gmf.Tarifa</c>: ningún valor legal se escribe aquí. Una tarifa cero da cero (la norma puede bajarla a cero). <see cref="RedondeoDeGmf.AlPeso"/> redondea al
/// peso con la mitad hacia arriba; <see cref="RedondeoDeGmf.SinRedondeo"/> deja dos decimales. (nuevo)
/// </summary>
public static class CalculoDeGmf
{
    /// <summary>El GMF de <paramref name="baseGravable"/> con <paramref name="tarifa"/> (fracción entre 0 y 1).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Base negativa o tarifa fuera de [0, 1].</exception>
    public static decimal Calcular(decimal baseGravable, decimal tarifa, RedondeoDeGmf redondeo)
    {
        if (baseGravable < 0m) throw new ArgumentOutOfRangeException(nameof(baseGravable), baseGravable, "La base del GMF no puede ser negativa.");
        if (tarifa is < 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(tarifa), tarifa, "La tarifa del GMF es una fracción entre 0 y 1.");
        if (tarifa == 0m || baseGravable == 0m) return 0m;
        var bruto = baseGravable * tarifa;
        return redondeo == RedondeoDeGmf.AlPeso
            ? Math.Round(bruto, 0, MidpointRounding.AwayFromZero)
            : Math.Round(bruto, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Lee el texto del parámetro <c>Gmf.Redondeo</c>; cualquier otro valor cae en <see cref="RedondeoDeGmf.AlPeso"/> (el defecto sembrado).</summary>
    public static RedondeoDeGmf Redondeo(string? texto) =>
        string.Equals(texto, nameof(RedondeoDeGmf.SinRedondeo), StringComparison.OrdinalIgnoreCase) ? RedondeoDeGmf.SinRedondeo : RedondeoDeGmf.AlPeso;
}
