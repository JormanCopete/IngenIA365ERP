using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Shared.Components.Shared;

/// <summary>
/// Dónde coincide una búsqueda en un texto (2026-11-15): término por término, sin tildes ni mayúsculas, como el servidor. La normalización va
/// carácter por carácter para que cada posición del texto normalizado sea la misma del original. (nuevo)
/// </summary>
public static class Resaltado
{
    /// <summary>Los trozos del texto, cada uno marcado si coincide con algún término.</summary>
    public static IReadOnlyList<(string Parte, bool Marcada)> Partes(string? texto, string? busqueda)
    {
        if (string.IsNullOrEmpty(texto)) return [];
        var terminos = Terminos(busqueda);
        if (terminos.Count == 0) return [(texto, false)];

        var normal = Normalizar(texto);
        var marcas = new bool[texto.Length];
        foreach (var termino in terminos)
        {
            for (var desde = normal.IndexOf(termino, StringComparison.Ordinal); desde >= 0; desde = normal.IndexOf(termino, desde + 1, StringComparison.Ordinal))
                for (var i = desde; i < desde + termino.Length; i++) marcas[i] = true;
        }

        var partes = new List<(string, bool)>();
        var inicio = 0;
        for (var i = 1; i <= texto.Length; i++)
        {
            if (i < texto.Length && marcas[i] == marcas[inicio]) continue;
            partes.Add((texto[inicio..i], marcas[inicio]));
            inicio = i;
        }
        return partes;
    }

    /// <summary>¿Algún término coincide en el texto?</summary>
    public static bool Coincide(string? texto, string? busqueda) =>
        !string.IsNullOrEmpty(texto) && Terminos(busqueda) is { Count: > 0 } terminos && terminos.Any(Normalizar(texto).Contains);

    private static List<string> Terminos(string? busqueda) =>
        string.IsNullOrWhiteSpace(busqueda) ? [] : [.. Normalizar(busqueda).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct()];

    /// <summary>Mayúsculas y sin tildes, un carácter por carácter (la longitud no cambia).</summary>
    private static string Normalizar(string texto)
    {
        var salida = new StringBuilder(texto.Length);
        foreach (var c in texto)
        {
            var base_ = c.ToString().Normalize(NormalizationForm.FormD).FirstOrDefault(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark);
            salida.Append(char.ToUpperInvariant(base_ == default ? c : base_));
        }
        return salida.ToString();
    }
}
