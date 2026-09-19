using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Cada <c>NavLink</c> del menú apunta a una página con <c>@page</c>. Un enlace a una ruta sin
/// página no da error: el router muestra «Página no encontrada» con el layout mínimo y parece
/// que la app «se sale». Así estaban «Informes y estados financieros» (entrega E2, sin página
/// aún) y «Beneficiarios» (desde la primera versión) hasta el 2026-09-19. Los <c>href</c>
/// calculados (<c>@($"…")</c>) se comparan por su forma con las rutas con parámetros.
/// </summary>
public class TodoEnlaceDelMenuTieneSuPagina
{
    private static readonly Regex Enlace = new(@"<NavLink\b[^>]*\bhref=""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex Pagina = new(@"@page\s+""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void Ningun_NavLink_del_menu_apunta_a_una_ruta_sin_pagina()
    {
        var raiz = RepoPath.FindRepoRoot();
        var shared = Path.Combine(raiz, "src", "Presentation", "IngenIA365ERP.Shared");
        var rutas = Directory.EnumerateFiles(Path.Combine(shared, "Pages"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(a => Pagina.Matches(File.ReadAllText(a)).Select(m => m.Groups[1].Value))
            .Select(r => r.TrimEnd('/').Split('/'))
            .ToList();

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
            var partes = href.Split('?')[0].TrimEnd('/').Split('/');
            var existe = rutas.Any(r => r.Length == partes.Length && r.Zip(partes).All(par => par.First == par.Second || par.First.StartsWith('{') || par.Second.StartsWith('{')));
            if (!existe) sinPagina.Add(href);
        }

        Assert.True(sinPagina.Count == 0,
            "Enlaces del menú sin página (@page): quitá el enlace hasta que exista la pantalla, o creá la página.\n  " + string.Join("\n  ", sinPagina));
    }
}
