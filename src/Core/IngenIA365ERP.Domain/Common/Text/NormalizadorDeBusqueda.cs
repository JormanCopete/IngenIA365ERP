using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Domain.Common.Text;

/// <summary>
/// Normalización del texto de búsqueda de productos (feature 012, decisiones-transversales T43, FR-020). Se usa en los
/// dos lados: al guardar <c>INV_Products.SearchText</c> y al armar la consulta, así «cafe» encuentra «CAFÉ» y
/// «pina» no confunde «PIÑA» con «PINA» (la <c>Ñ</c> es otra letra y se conserva).
///
/// <list type="bullet">
/// <item><see cref="Normalizar"/>: mayúsculas, sin tildes ni diéresis (salvo la <c>Ñ</c>), espacios colapsados.</item>
/// <item><see cref="Terminos"/>: la consulta partida en términos; todos son obligatorios («contiene» cada uno).</item>
/// <item><see cref="EscaparParaLike"/> y <see cref="PatronesContiene"/>: cada término escapado para
/// <c>EF.Functions.Like(SearchText, patrón, CaracterDeEscape)</c>, portable entre SQL Server y PostgreSQL.</item>
/// </list>
/// Puro y sin estado: no conoce EF.
/// </summary>
public static class NormalizadorDeBusqueda
{
    /// <summary>Mínimo de caracteres (ya normalizados) para buscar mientras se escribe.</summary>
    public const int MinimoDeCaracteres = 2;

    /// <summary>Carácter de escape que se le pasa a <c>LIKE … ESCAPE</c>; vale igual en los dos motores.</summary>
    public const char CaracterDeEscape = '\\';

    /// <summary>
    /// Mayúsculas invariantes, sin marcas diacríticas (tildes, diéresis) salvo la <c>Ñ</c>, con los espacios (y
    /// tabuladores, saltos) colapsados a uno y sin espacios al borde. Nulo o vacío → <c>""</c>.
    /// </summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var sb = new StringBuilder(texto.Length);
        var espacioPendiente = false;
        foreach (var original in texto.ToUpperInvariant())
        {
            if (char.IsWhiteSpace(original))
            {
                espacioPendiente = sb.Length > 0;
                continue;
            }

            if (espacioPendiente)
            {
                sb.Append(' ');
                espacioPendiente = false;
            }

            if (original == 'Ñ')
            {
                sb.Append('Ñ');
                continue;
            }

            foreach (var c in original.ToString().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Los términos de la consulta, normalizados y sin repetir, en el orden en que se escribieron. Si la consulta
    /// normalizada no llega a <see cref="MinimoDeCaracteres"/>, ninguno (no se busca).
    /// </summary>
    public static IReadOnlyList<string> Terminos(string? consulta)
    {
        var normalizada = Normalizar(consulta);
        if (normalizada.Length < MinimoDeCaracteres) return [];
        return normalizada.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// El término con los comodines de <c>LIKE</c> escapados con <see cref="CaracterDeEscape"/>: <c>%</c>, <c>_</c>,
    /// <c>[</c> (comodín de SQL Server) y el propio carácter de escape.
    /// </summary>
    public static string EscaparParaLike(string termino)
    {
        var sb = new StringBuilder(termino.Length + 4);
        foreach (var c in termino)
        {
            if (c is '%' or '_' or '[' or CaracterDeEscape) sb.Append(CaracterDeEscape);
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Un patrón <c>%término%</c> escapado por cada término de la consulta (vacío si no alcanza el mínimo).</summary>
    public static IReadOnlyList<string> PatronesContiene(string? consulta) =>
        Terminos(consulta).Select(t => "%" + EscaparParaLike(t) + "%").ToList();
}
