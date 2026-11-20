using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using Xunit;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// 2026-11-19 (pedido del dueño): la barra «Ha ocurrido un error inesperado» (<c>#blazor-error-ui</c>) sólo se ve cuando algo se rompió —Blazor
/// le pone <c>display:block</c> en línea—. Hasta ese día cada diseño la escondía en su <c>.razor.css</c> y <c>PosLayout</c> no tenía uno: en la
/// Caja y el POS la barra quedaba siempre al final de la página, sin error, y quien bajaba hasta allá la reportaba como una falla. Ahora la regla
/// vive una sola vez en <c>componentes.css</c> (que cargan la Web y la app MAUI) y esta prueba fija que (1) esa regla la esconde, (2) ninguna otra
/// hoja la redefine y (3) todo diseño que emite la barra es uno de los que cargan <c>componentes.css</c>. Es una prueba sobre el fuente porque no
/// hay pruebas de navegador en el repositorio. (nuevo)
/// </summary>
public class ElAvisoDeErrorSoloAparecePorUnError
{
    private static string Presentacion => Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation");

    private static string Componentes => Path.Combine(Presentacion, "IngenIA365ERP.Shared", "wwwroot", "css", "componentes.css");

    private static IEnumerable<string> Fuentes(params string[] patrones) =>
        patrones.SelectMany(p => Directory.EnumerateFiles(Presentacion, p, SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static readonly Regex ReglaDeLaBarra = new(@"#blazor-error-ui\s*\{(?<cuerpo>[^}]*)\}", RegexOptions.Compiled);

    [Fact]
    public void La_hoja_comun_esconde_la_barra()
    {
        var m = ReglaDeLaBarra.Match(File.ReadAllText(Componentes));
        Assert.True(m.Success, "componentes.css ya no declara #blazor-error-ui { … }: sin esa regla la barra de error se ve siempre.");
        Assert.Matches(@"display\s*:\s*none\s*;", m.Groups["cuerpo"].Value);
    }

    [Fact]
    public void Ninguna_otra_hoja_redefine_la_barra()
    {
        var otras = Fuentes("*.css")
            .Where(f => !string.Equals(Path.GetFullPath(f), Path.GetFullPath(Componentes), StringComparison.OrdinalIgnoreCase))
            .Where(f => ReglaDeLaBarra.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(Presentacion, f))
            .ToList();
        Assert.True(otras.Count == 0, $"La barra de error se define sólo en componentes.css; también la definen: {string.Join(", ", otras)}");
    }

    [Fact]
    public void La_barra_la_emiten_solo_los_disenos_de_Shared()
    {
        var emisores = Fuentes("*.razor", "*.html", "*.cshtml")
            .Where(f => File.ReadAllText(f).Contains("id=\"blazor-error-ui\"", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(Presentacion, f).Replace('\\', '/'))
            .ToList();
        Assert.NotEmpty(emisores);
        // Los diseños de Shared van en la Web y en la app MAUI, que cargan componentes.css (App.razor e index.html).
        Assert.All(emisores, e => Assert.StartsWith("IngenIA365ERP.Shared/Layout/", e, StringComparison.Ordinal));
    }
}
