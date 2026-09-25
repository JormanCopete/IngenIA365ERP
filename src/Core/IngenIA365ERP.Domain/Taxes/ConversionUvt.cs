namespace IngenIA365ERP.Domain.Taxes;

/// <summary>
/// Cómo se redondea a pesos un valor expresado en UVT (<c>Tributario.RedondeoUvtAPesos</c>, feature 012, T19, T23): al
/// peso, a la centena o al mil más cercano, con el punto medio alejándose de cero. (nuevo)
/// </summary>
public enum RedondeoUvt
{
    Peso = 0,
    Centena = 1,
    Mil = 2,
}

/// <summary>
/// La conversión de una base mínima en UVT a pesos y la comparación con la base (feature 012, T162; FR-013; research
/// R21): la UVT la da <c>LectorDeUvt</c> a la fecha del documento y el redondeo el parámetro
/// <c>Tributario.RedondeoUvtAPesos</c>. Una retención procede cuando la base es <b>igual o superior</b> al mínimo.
/// Puro: sin valores legales. (nuevo)
/// </summary>
public static class ConversionUvt
{
    /// <summary>El valor del parámetro como <see cref="RedondeoUvt"/>; vacío o desconocido, <see cref="RedondeoUvt.Peso"/> (su defecto).</summary>
    public static RedondeoUvt Redondeo(string? valor) =>
        Enum.TryParse<RedondeoUvt>(valor?.Trim(), ignoreCase: true, out var r) && Enum.IsDefined(r) ? r : RedondeoUvt.Peso;

    /// <summary><paramref name="valorEnUvt"/> × <paramref name="uvt"/>, redondeado según <paramref name="redondeo"/>.</summary>
    public static decimal APesos(decimal uvt, decimal valorEnUvt, RedondeoUvt redondeo)
    {
        decimal unidad = redondeo switch
        {
            RedondeoUvt.Centena => 100,
            RedondeoUvt.Mil => 1000,
            _ => 1,
        };
        var bruto = uvt * valorEnUvt;
        return Math.Round(bruto / unidad, 0, MidpointRounding.AwayFromZero) * unidad;
    }

    /// <summary>La retención procede con base igual o superior al mínimo en pesos (FR-013).</summary>
    public static bool Procede(decimal baseGravable, decimal minimoEnPesos) => baseGravable >= minimoEnPesos;
}
