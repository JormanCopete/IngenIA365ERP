using System.Text.RegularExpressions;

namespace IngenIA365ERP.Architecture.Tests.Helpers;

/// <summary>
/// Lee un fuente C# sin sus comentarios (bloques <c>/* */</c> y líneas que empiezan con <c>//</c> o
/// <c>///</c>), para que las reglas sobre el texto miren el código y no la documentación: un
/// «lo hacen los procesos con <c>RaiseAlertCommand</c>» en el resumen de un endpoint no es una ruta.
/// Los comentarios al final de una línea de código se conservan, porque cortar en <c>//</c> rompería
/// las cadenas con URL. Mismo criterio que <c>LaBanderaDelSegundoFactorTieneUnSoloAutor</c>
/// (feature 012, T018).
/// </summary>
internal static class FuenteSinComentarios
{
    private static readonly Regex Bloques = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);

    public static string Leer(string archivo) => Quitar(File.ReadAllText(archivo));

    public static string Quitar(string fuente)
    {
        var sinBloques = Bloques.Replace(fuente, string.Empty);
        return string.Join('\n', sinBloques
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }
}
