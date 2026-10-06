using System.Text.RegularExpressions;

namespace IngenIA365ERP.Architecture.Tests.Helpers;

/// <summary>
/// Feature 013 (Setup, T0001–T0007): el recorrido común de las pruebas de arquitectura de Cartera que
/// revisan el fuente. Cada prueba declara sus listas (carpetas, archivos o nombres) y sus patrones; este
/// ayudante sólo recorre. Una carpeta que todavía no existe no tiene archivos: la prueba pasa porque no hay
/// nada que violar, nunca por un <c>return</c> temprano (CLAUDE.md: siete métodos «pasaban» sin comprobar nada).
/// </summary>
internal static class ReglaSobreFuente
{
    /// <summary>Archivos <c>.cs</c> de producción bajo cada carpeta (relativa a <c>src/</c>), recursivo.</summary>
    public static IEnumerable<string> ArchivosEn(IEnumerable<string> carpetasRelativasASrc, string patron = "*.cs")
    {
        var src = Path.Combine(RepoPath.FindRepoRoot(), "src");
        foreach (var relativa in carpetasRelativasASrc)
        {
            var carpeta = Path.Combine(src, relativa.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(carpeta)) continue;
            foreach (var archivo in Directory.EnumerateFiles(carpeta, patron, SearchOption.AllDirectories))
            {
                if (EsGenerado(archivo)) continue;
                yield return archivo;
            }
        }
    }

    /// <summary>Archivo relativo a <c>src/</c>; si no existe, no hay nada que revisar.</summary>
    public static IEnumerable<string> ArchivosNombrados(IEnumerable<string> archivosRelativosASrc)
    {
        var src = Path.Combine(RepoPath.FindRepoRoot(), "src");
        foreach (var relativo in archivosRelativosASrc)
        {
            var archivo = Path.Combine(src, relativo.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(archivo), $"La lista nombra {relativo}, que no existe: si se movió, actualizá la lista.");
            yield return archivo;
        }
    }

    /// <summary>
    /// Líneas que contienen algún patrón prohibido, sin comentarios. <paramref name="exento"/> recibe la ruta
    /// relativa a la raíz con barras normales.
    /// </summary>
    public static List<string> Infracciones(
        IEnumerable<string> archivos,
        IReadOnlyList<(Regex Patron, string Que)> prohibidos,
        Func<string, bool>? exento = null)
    {
        var root = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        foreach (var archivo in archivos)
        {
            var relativo = Relativo(root, archivo);
            if (exento?.Invoke(relativo) == true) continue;
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].Trim();
                if (EsComentario(linea)) continue;
                foreach (var (patron, que) in prohibidos)
                    if (patron.IsMatch(linea))
                        infracciones.Add($"{relativo}:{i + 1}: {que}");
            }
        }
        return infracciones;
    }

    /// <summary>Archivos que no contienen el patrón obligatorio.</summary>
    public static List<string> Faltantes(IEnumerable<string> archivos, Regex requerido)
    {
        var root = RepoPath.FindRepoRoot();
        return archivos
            .Where(a => !requerido.IsMatch(File.ReadAllText(a)))
            .Select(a => Relativo(root, a))
            .ToList();
    }

    public static string Relativo(string root, string archivo) =>
        archivo.Replace(root, string.Empty).Replace('\\', '/').TrimStart('/');

    public static bool EsComentario(string lineaRecortada) =>
        lineaRecortada.StartsWith("//", StringComparison.Ordinal)
        || lineaRecortada.StartsWith("*", StringComparison.Ordinal)
        || lineaRecortada.StartsWith("/*", StringComparison.Ordinal);

    public static void SinInfracciones(List<string> infracciones, string mensaje) =>
        Assert.True(infracciones.Count == 0, mensaje + ":\n  " + string.Join("\n  ", infracciones));

    private static bool EsGenerado(string archivo) =>
        archivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || archivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
