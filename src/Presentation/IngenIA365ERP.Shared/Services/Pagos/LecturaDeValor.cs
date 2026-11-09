using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Pagos;

/// <summary>
/// Feature 022 (T053; FR-007): leer y mostrar un valor sin la ambigüedad del <c>type=number</c>, que según el navegador toma
/// «50.000» por cincuenta. Reglas, pensadas para quien digita en Colombia:
/// <list type="bullet">
/// <item>En pesos (<c>decimales = 0</c>) el punto, la coma y el espacio sólo son separadores de miles: «50000», «50.000»,
/// «50 000» y «1,250,000» valen lo mismo que sin separadores; «12.5» no se entiende y se dice.</item>
/// <item>Con decimales, la coma es la coma decimal («12,5»), el punto es de miles si arma grupos de tres («12.500») y si no, decimal
/// («12.5»); con los dos, el último que aparece es el decimal («1.234,56» o «1,234.56»).</item>
/// </list>
/// Se muestra con la cultura es-CO (miles con punto, decimales con coma). Pura y sin estado: la usa <c>CampoDeValor</c>.
/// </summary>
public static class LecturaDeValor
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>El valor con el formato de la pantalla: «50.000», «1.234,5» (con <paramref name="decimales"/>), o vacío si es nulo.</summary>
    public static string Mostrar(decimal? valor, int decimales = 0) =>
        valor is { } v ? v.ToString(decimales <= 0 ? "N0" : "N" + decimales, Colombia) : "";

    /// <summary>
    /// Lee lo escrito. Devuelve <c>Valido=false</c> y un motivo si no se entiende. Vacío es válido y vale nulo.
    /// Admite «$» y espacios; el signo menos, sólo si <paramref name="permiteNegativos"/>.
    /// </summary>
    public static (bool Valido, decimal? Valor, string? Motivo) Leer(string? texto, int decimales = 0, bool permiteNegativos = false)
    {
        var t = (texto ?? "").Replace("$", "").Replace(" ", " ").Trim();
        if (t.Length == 0) return (true, null, null);

        var negativo = t.StartsWith('-');
        if (negativo)
        {
            if (!permiteNegativos) return (false, null, "El valor no puede ser negativo.");
            t = t[1..].Trim();
        }

        t = string.Join('.', t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (t.Length == 0 || t.Any(c => !char.IsAsciiDigit(c) && c != '.' && c != ','))
            return (false, null, "Escriba sólo números, con o sin puntos de miles.");

        var puntos = t.Count(c => c == '.');
        var comas = t.Count(c => c == ',');
        string enteros, fraccion = "";

        if (puntos == 0 && comas == 0)
        {
            enteros = t;
        }
        else if (decimales <= 0)
        {
            if (!SonMiles(t)) return (false, null, "En pesos no hay decimales: escriba el valor entero (50000 o 50.000).");
            enteros = t;
        }
        else if (puntos > 0 && comas > 0)
        {
            var separador = Math.Max(t.LastIndexOf('.'), t.LastIndexOf(','));
            enteros = t[..separador];
            fraccion = t[(separador + 1)..];
            if (!SonMiles(enteros) || (enteros.Contains('.') && enteros.Contains(','))) return (false, null, "No se entiende el valor: use el punto para los miles y la coma para los decimales.");
        }
        else if (comas > 0)
        {
            if (comas == 1) { var i = t.IndexOf(','); enteros = t[..i]; fraccion = t[(i + 1)..]; }
            else if (SonMiles(t)) enteros = t;
            else return (false, null, "No se entiende el valor: use el punto para los miles y la coma para los decimales.");
        }
        else
        {
            var i = t.LastIndexOf('.');
            if (SonMiles(t) && !(puntos == 1 && t.Length - i - 1 != 3)) enteros = t;
            else if (puntos == 1) { enteros = t[..i]; fraccion = t[(i + 1)..]; }
            else return (false, null, "No se entiende el valor: use el punto para los miles y la coma para los decimales.");
        }

        if (fraccion.Length > Math.Max(0, decimales)) return (false, null, $"Admite hasta {decimales} decimales.");
        var digitos = new string(enteros.Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length == 0) digitos = "0";
        var numero = fraccion.Length > 0 ? digitos + "." + fraccion : digitos;
        if (!decimal.TryParse(numero, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor))
            return (false, null, "El valor es demasiado grande.");
        return (true, negativo ? -valor : valor, null);
    }

    /// <summary>Sólo dígitos, o grupos de tres tras el primero (de uno a tres dígitos) con un mismo separador.</summary>
    private static bool SonMiles(string t)
    {
        if (t.All(char.IsAsciiDigit)) return t.Length > 0;
        var separador = t.First(c => c is '.' or ',');
        var grupos = t.Split(separador);
        if (grupos[0].Length is < 1 or > 3 || !grupos[0].All(char.IsAsciiDigit)) return false;
        return grupos.Skip(1).All(g => g.Length == 3 && g.All(char.IsAsciiDigit));
    }
}
