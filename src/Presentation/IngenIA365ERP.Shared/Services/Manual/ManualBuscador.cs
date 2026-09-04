using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Shared.Services.Manual;

/// <summary>
/// Búsqueda del manual, en el cliente y sin índice: el catálogo tiene unos
/// cientos de temas y se recorre entero en menos de lo que tarda en pintarse la
/// lista. Cada palabra de la consulta tiene que aparecer en algún sitio del tema
/// (título, palabras clave, módulo, resumen o pasos); lo que decide el orden es
/// dónde apareció.
/// </summary>
public static class ManualBuscador
{
    public static IReadOnlyList<ResultadoDeBusqueda> Buscar(string? consulta, IEnumerable<TemaDeManual> temas)
    {
        var terminos = Normalizar(consulta)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 1)
            .Distinct()
            .ToArray();

        if (terminos.Length == 0) return [];

        var resultados = new List<ResultadoDeBusqueda>();
        foreach (var tema in temas)
        {
            var titulo = Normalizar(tema.Titulo);
            var claves = Normalizar(string.Join(' ', tema.PalabrasClave));
            var modulo = Normalizar(tema.Modulo);
            var resumen = Normalizar(tema.Resumen);
            var pasos = tema.Pasos.Select(p => Normalizar(p.Titulo + " " + p.Detalle)).ToArray();

            var puntaje = 0;
            string? fragmento = null;
            var todos = true;

            foreach (var termino in terminos)
            {
                var parcial = 0;
                if (titulo.Contains(termino)) parcial += titulo.StartsWith(termino) ? 14 : 10;
                if (claves.Contains(termino)) parcial += 6;
                if (modulo.Contains(termino)) parcial += 3;
                if (resumen.Contains(termino)) parcial += 4;

                var indicePaso = Array.FindIndex(pasos, p => p.Contains(termino));
                if (indicePaso >= 0)
                {
                    parcial += 2;
                    fragmento ??= tema.Pasos[indicePaso].Titulo;
                }

                if (parcial == 0) { todos = false; break; }
                puntaje += parcial;
            }

            if (!todos) continue;
            resultados.Add(new ResultadoDeBusqueda(tema, puntaje, fragmento ?? tema.Resumen));
        }

        return resultados
            .OrderByDescending(r => r.Puntaje)
            .ThenBy(r => r.Tema.Titulo, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Minúsculas y sin tildes: «Liquidación» encuentra «liquidacion» y al revés.
    /// Los títulos del sistema mezclan las dos grafías y quien busca no tiene por
    /// qué saber cuál usó cada pantalla.
    /// </summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
