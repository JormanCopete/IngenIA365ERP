using System.Globalization;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Common.Parametros;

namespace IngenIA365ERP.Domain.Lending.Parameters;

/// <summary>
/// Cómo se compara un parámetro de Cartera con el límite legal que lo acota (feature 013, T42; data-model parte 3 §12.1, §12.5). Puro:
/// recibe el texto canónico del parámetro y el valor del límite, nunca lee la base. Lo usan las reglas del alta (rechazar lo menos
/// estricto) y la lectura (entregar lo más estricto):
/// <list type="bullet">
/// <item>un entero o un decimal se compara tal cual;</item>
/// <item>un sí/no es 1 o 0 (<c>MORA_SOBRE_INTERESES_PERMITIDA</c>: 0 = la norma no lo permite);</item>
/// <item>un horario <c>HH:mm-HH:mm</c> se compara por la punta que el límite acota —la de inicio con los límites
/// <c>COBRANZA_HORA_INICIO_*</c>, la de fin con <c>COBRANZA_HORA_FIN_*</c>— en horas con fracción.</item>
/// </list>
/// Un parámetro vacío (la clave lo admite) no tiene valor propio: rige el límite. (nuevo)
/// </summary>
public static class LimitesDeParametro
{
    private const int MinutosPorHora = 60;

    private static readonly HashSet<string> LimitesDeInicio = new(StringComparer.Ordinal)
    {
        CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes,
        CatalogoDeLimitesLegales.CobranzaHoraInicioSabado,
    };

    private static readonly HashSet<string> LimitesDeFin = new(StringComparer.Ordinal)
    {
        CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes,
        CatalogoDeLimitesLegales.CobranzaHoraFinSabado,
    };

    /// <summary>
    /// El valor del parámetro en la unidad del límite, o nulo si no tiene valor propio (vacío) o el texto no tiene la forma esperada
    /// (la definición ya lo habría rechazado).
    /// </summary>
    public static decimal? Comparable(DefinicionDeParametro definicion, string? texto, string codigoDeLimite)
    {
        var t = texto?.Trim() ?? string.Empty;
        if (t.Length == 0) return null;
        switch (definicion.Tipo)
        {
            case TipoDeParametro.Bool:
                return bool.TryParse(t, out var b) ? (b ? decimal.One : decimal.Zero) : null;
            case TipoDeParametro.Int:
            case TipoDeParametro.Decimal:
                return decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) ? d : null;
            case TipoDeParametro.Text when Franja(t) is { } franja:
                if (LimitesDeInicio.Contains(codigoDeLimite)) return Horas(franja.Inicio);
                if (LimitesDeFin.Contains(codigoDeLimite)) return Horas(franja.Fin);
                return null;
            default:
                return null;
        }
    }

    /// <summary>Si el valor de la cooperativa es menos estricto que el límite (se rechaza al registrarlo).</summary>
    public static bool EsMenosEstricto(DefinicionDeParametro definicion, string? texto, string codigoDeLimite, decimal limite) =>
        Comparable(definicion, texto, codigoDeLimite) is { } valor && CatalogoDeLimitesLegales.EsMenosEstricto(codigoDeLimite, valor, limite);

    /// <summary>
    /// El texto canónico que rige entre el de la cooperativa y el límite: lo más estricto de los dos. Vacío: el límite, escrito en el
    /// tipo de la clave. Un texto que no se puede comparar con ese límite se devuelve igual.
    /// </summary>
    public static string MasEstricto(DefinicionDeParametro definicion, string? texto, string codigoDeLimite, decimal limite)
    {
        var t = texto?.Trim() ?? string.Empty;
        var propio = Comparable(definicion, t, codigoDeLimite);
        if (propio is null && t.Length > 0) return t;

        var rige = propio is { } p ? CatalogoDeLimitesLegales.MasEstricto(codigoDeLimite, p, limite) : limite;
        switch (definicion.Tipo)
        {
            case TipoDeParametro.Bool:
                return rige >= decimal.One ? "true" : "false";
            case TipoDeParametro.Int:
                return decimal.ToInt32(decimal.Truncate(rige)).ToString(CultureInfo.InvariantCulture);
            case TipoDeParametro.Decimal:
                return rige.ToString(CultureInfo.InvariantCulture);
            case TipoDeParametro.Text when Franja(t) is { } franja:
                var hora = Hora(rige);
                return LimitesDeInicio.Contains(codigoDeLimite)
                    ? $"{hora:HH\\:mm}-{franja.Fin:HH\\:mm}"
                    : $"{franja.Inicio:HH\\:mm}-{hora:HH\\:mm}";
            default:
                return t;
        }
    }

    private static (TimeOnly Inicio, TimeOnly Fin)? Franja(string texto)
    {
        var partes = texto.Split('-');
        if (partes.Length != 2) return null;
        return TimeOnly.TryParseExact(partes[0], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio)
               && TimeOnly.TryParseExact(partes[1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fin)
            ? (inicio, fin)
            : null;
    }

    private static decimal Horas(TimeOnly hora) => hora.Hour + decimal.Divide(hora.Minute, MinutosPorHora);

    private static TimeOnly Hora(decimal horas)
    {
        var minutos = decimal.ToInt32(decimal.Round(horas * MinutosPorHora, MidpointRounding.AwayFromZero));
        return new TimeOnly(Math.Clamp(minutos / MinutosPorHora, 0, 23), minutos % MinutosPorHora);
    }
}
