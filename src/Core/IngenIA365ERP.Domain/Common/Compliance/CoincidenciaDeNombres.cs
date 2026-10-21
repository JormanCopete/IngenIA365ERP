using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Domain.Common.Compliance;

/// <summary>
/// Coincidencia por nombre contra las listas restrictivas, en memoria (feature 013, T70, F-18; SQL Server no tiene
/// <c>pg_trgm</c>, así que no se le pide al motor). Pura: sin IO ni estado.
///
/// <list type="bullet">
/// <item><see cref="Normalizar"/>: mayúsculas, sin tildes ni diéresis (la <c>Ñ</c> pasa a <c>N</c>, como la escriben las
/// listas internacionales), cualquier signo es separador y los tokens se ordenan, para que «Pérez, José» y «JOSE PEREZ»
/// sean el mismo nombre.</item>
/// <item><see cref="NormalizarDocumento"/>: sin puntos, guiones, espacios ni ceros a la izquierda, en mayúsculas; la
/// coincidencia por documento es exacta sobre esto.</item>
/// <item><see cref="Similitud"/>: coeficiente de Sørensen–Dice sobre los pares de caracteres del nombre normalizado, en
/// <c>decimal</c> (0 a 1, cuatro decimales).</item>
/// <item><see cref="Evaluar"/>: el mejor entre el nombre de la lista y sus alias, contra el umbral que recibe
/// (<c>Listas.UmbralDeCoincidenciaDeNombre</c>): el umbral nunca está en el código.</item>
/// </list>
/// </summary>
public static class CoincidenciaDeNombres
{
    public static string Normalizar(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return string.Empty;

        var sb = new StringBuilder(nombre.Length);
        foreach (var c in nombre.ToUpperInvariant().Normalize(NormalizationForm.FormD))
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is '\'' or '’' or '`' or '´') continue; // O'Neill → ONEILL: el apóstrofo une, no separa
            else sb.Append(' ');
        }

        var tokens = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(tokens, StringComparer.Ordinal);
        return string.Join(' ', tokens);
    }

    public static string NormalizarDocumento(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento)) return string.Empty;
        var sb = new StringBuilder(documento.Length);
        foreach (var c in documento.ToUpperInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString().TrimStart('0');
    }

    /// <summary>Similitud de 0 a 1 entre dos nombres (se normalizan aquí). Un nombre vacío no se parece a nada.</summary>
    public static decimal Similitud(string? nombreA, string? nombreB) =>
        SimilitudNormalizada(Normalizar(nombreA), Normalizar(nombreB));

    /// <summary>
    /// Compara <paramref name="nombre"/> con el nombre de la lista y cada alias; devuelve la mejor similitud, contra qué
    /// nombre (normalizado) se obtuvo y si alcanza el <paramref name="umbral"/> (mayor que 0 y hasta 1).
    /// </summary>
    public static ResultadoDeCoincidencia Evaluar(string? nombre, string? nombreEnLista, IEnumerable<string?> alias, decimal umbral)
    {
        if (umbral <= 0m || umbral > 1m)
            throw new ArgumentOutOfRangeException(nameof(umbral), umbral, "El umbral de coincidencia va de más de 0 hasta 1.");
        ArgumentNullException.ThrowIfNull(alias);

        var buscado = Normalizar(nombre);
        var mejor = new ResultadoDeCoincidencia(false, 0m, Normalizar(nombreEnLista));
        foreach (var candidato in new[] { nombreEnLista }.Concat(alias).Select(Normalizar).Where(c => c.Length > 0))
        {
            var s = SimilitudNormalizada(buscado, candidato);
            if (s > mejor.Similitud) mejor = new ResultadoDeCoincidencia(false, s, candidato);
        }
        return mejor with { Coincide = mejor.Similitud > 0m && mejor.Similitud >= umbral };
    }

    private static decimal SimilitudNormalizada(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0m;
        if (a == b) return 1m;
        if (a.Length < 2 || b.Length < 2) return 0m;

        var paresA = Pares(a);
        var paresB = Pares(b);
        var comunes = 0;
        foreach (var (par, cuantos) in paresA)
            if (paresB.TryGetValue(par, out var enB)) comunes += Math.Min(cuantos, enB);

        var total = (a.Length - 1) + (b.Length - 1);
        return Math.Round(2m * comunes / total, 4, MidpointRounding.ToZero);
    }

    private static Dictionary<(char, char), int> Pares(string s)
    {
        var pares = new Dictionary<(char, char), int>();
        for (var i = 0; i < s.Length - 1; i++)
        {
            var par = (s[i], s[i + 1]);
            pares[par] = pares.TryGetValue(par, out var n) ? n + 1 : 1;
        }
        return pares;
    }
}

/// <summary>El resultado de comparar un nombre con una entrada de lista: si coincide, cuánto y contra qué nombre o alias.</summary>
public sealed record ResultadoDeCoincidencia(bool Coincide, decimal Similitud, string NombreComparado);
