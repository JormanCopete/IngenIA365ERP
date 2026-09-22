using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Cada <c>NavLink</c> del menú apunta a una página con <c>@page</c>. Un enlace a una ruta sin
/// página no da error: el router muestra «Página no encontrada» con el layout mínimo y parece
/// que la app «se sale». Así estaban «Informes y estados financieros» (entrega E2, sin página
/// aún) y «Beneficiarios» (desde la primera versión) hasta el 2026-09-19. Los <c>href</c>
/// calculados (<c>@($"…")</c>) se comparan por su forma con las rutas con parámetros.
///
/// <para>
/// Desde el 2026-09-20 se revisan también las tarjetas del Centro de Reportes
/// (<c>Pages/CentroReportes.razor</c>), que navegan con <c>Navigation.NavigateTo("/…")</c> en vez
/// de <c>NavLink</c>: cinco tarjetas contables llevaban meses apuntando a rutas sin página
/// (/reportes/balance-general, /reportes/libro-mayor…) y esta prueba no las veía porque sólo
/// leía el menú. La query string (<c>?vista=</c>) no cuenta para el emparejamiento.
/// </para>
/// </summary>
public class TodoEnlaceDelMenuTieneSuPagina
{
    private static readonly Regex Enlace = new(@"<NavLink\b[^>]*\bhref=""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex Navegacion = new(@"NavigateTo\(\s*""(/[^""]*)""\s*\)", RegexOptions.Compiled);
    private static readonly Regex Pagina = new(@"@page\s+""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void Ningun_NavLink_del_menu_apunta_a_una_ruta_sin_pagina()
    {
        var shared = Shared();
        var rutas = Rutas(shared);

        var menu = File.ReadAllText(Path.Combine(shared, "Layout", "NavMenu.razor"));
        var sinPagina = new List<string>();
        foreach (Match m in Enlace.Matches(menu))
        {
            var href = m.Groups[1].Value;
            // href="@($"/admin/tenant/{_activeTenantId}/members")" → /admin/tenant/{x}/members
            if (href.StartsWith("@(", StringComparison.Ordinal))
            {
                var literal = Regex.Match(href, @"\$?""([^""]+)""");
                if (!literal.Success) continue;
                href = Regex.Replace(literal.Groups[1].Value, @"\{[^}]+\}", "{p}");
            }
            if (!Existe(rutas, href)) sinPagina.Add(href);
        }

        Assert.True(sinPagina.Count == 0,
            "Enlaces del menú sin página (@page): quitá el enlace hasta que exista la pantalla, o creá la página.\n  " + string.Join("\n  ", sinPagina));
    }

    [Fact]
    public void Ninguna_tarjeta_del_centro_de_reportes_navega_a_una_ruta_sin_pagina()
    {
        var shared = Shared();
        var rutas = Rutas(shared);

        var centro = File.ReadAllText(Path.Combine(shared, "Pages", "CentroReportes.razor"));
        var destinos = Navegacion.Matches(centro).Select(m => m.Groups[1].Value).ToList();
        Assert.True(destinos.Count > 0, "CentroReportes.razor no tiene ningún NavigateTo(\"/…\"): si cambió la forma de navegar, actualizá esta prueba.");

        var sinPagina = destinos.Where(d => !Existe(rutas, d)).Distinct().ToList();
        Assert.True(sinPagina.Count == 0,
            "Tarjetas del Centro de Reportes sin página (@page): quitá la tarjeta hasta que exista la pantalla, o apuntala a una que exista.\n  " + string.Join("\n  ", sinPagina));
    }

    private static string Shared() =>
        Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");

    /// <summary>Todas las rutas con <c>@page</c> de Shared, partidas en segmentos.</summary>
    private static List<string[]> Rutas(string shared) =>
        Directory.EnumerateFiles(Path.Combine(shared, "Pages"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(a => Pagina.Matches(File.ReadAllText(a)).Select(m => m.Groups[1].Value))
            .Select(r => r.TrimEnd('/').Split('/'))
            .ToList();

    /// <summary>La ruta (sin query string) coincide con alguna página, segmento a segmento; un segmento <c>{…}</c> empareja con cualquiera.</summary>
    private static bool Existe(List<string[]> rutas, string href)
    {
        var partes = href.Split('?')[0].TrimEnd('/').Split('/');
        return rutas.Any(r => r.Length == partes.Length && r.Zip(partes).All(par => par.First == par.Second || par.First.StartsWith('{') || par.Second.StartsWith('{')));
    }
}
