using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// 2026-11-19: la pantalla de Roles dice en qué opción del menú se usa cada permiso con <c>MapaDelMenu.g.cs</c>, que genera
/// <c>tools/scripts/generar-mapa-del-menu.py</c> leyendo el menú y los <c>&lt;PermissionGate&gt;</c> de cada pantalla. Un mapa
/// viejo diría que un permiso no se usa en ninguna opción, o callaría la opción nueva; y el menú cierra con un clic el módulo
/// abierto preguntándole al mapa. Si esta prueba falla: <c>python tools/scripts/generar-mapa-del-menu.py</c>.
/// </summary>
public class ElMapaDelMenuEstaAlDia
{
    private static readonly Regex EnlaceDelMenu = new("<NavLink\\s+href=\"([^\"]+)\"[^>]*class=\"nav-(?:sub)?item\"", RegexOptions.Compiled);
    private static readonly Regex Pagina = new("@page\\s+\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex Gate = new("<PermissionGate\\b([^>]*)>", RegexOptions.Compiled);
    private static readonly Regex Requerido = new("\\bRequired=\"([^\"@]+)\"", RegexOptions.Compiled);
    private static readonly Regex Alguno = new("\\bAnyOf=\"@\\(new\\[\\]\\s*\\{([^}]*)\\}", RegexOptions.Compiled);
    private static readonly Regex Literal = new("\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex Entrada = new(
        "new\\(\"[^\"]*\", (?:null|\"[^\"]*\"), \"[^\"]*\", \"(?<ruta>[^\"]+)\", \\[[^\\]]*\\], \\[(?<acceso>[^\\]]*)\\], \\[(?<acciones>[^\\]]*)\\]\\)",
        RegexOptions.Compiled);

    private const string Aviso = "El mapa del menú está viejo: corré `python tools/scripts/generar-mapa-del-menu.py`.";

    private static string Shared(string root) => Path.Combine(root, "src", "Presentation", "IngenIA365ERP.Shared");

    private static string Mapa(string root) => File.ReadAllText(Path.Combine(Shared(root), "Services", "Security", "MapaDelMenu.g.cs"));

    [Fact]
    public void Cada_opcion_del_menu_esta_en_el_mapa()
    {
        var root = RepoPath.FindRepoRoot();
        var menu = Regex.Replace(File.ReadAllText(Path.Combine(Shared(root), "Layout", "NavMenu.razor")), "@\\*.*?\\*@", "", RegexOptions.Singleline);
        var mapa = Mapa(root);

        var faltan = EnlaceDelMenu.Matches(menu).Select(m => m.Groups[1].Value)
            .Where(href => !mapa.Contains($"\"{href}\"", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.True(faltan.Count == 0, $"{Aviso}\nOpciones del menú que el mapa no conoce:\n  " + string.Join("\n  ", faltan));
    }

    [Fact]
    public void Los_permisos_de_cada_pantalla_del_mapa_son_los_de_la_pantalla()
    {
        var root = RepoPath.FindRepoRoot();
        var pantallas = new Dictionary<string, (HashSet<string> Acceso, HashSet<string> Acciones)>(StringComparer.OrdinalIgnoreCase);
        foreach (var archivo in Directory.EnumerateFiles(Path.Combine(Shared(root), "Pages"), "*.razor", SearchOption.AllDirectories))
        {
            var s = File.ReadAllText(archivo);
            var rutas = Pagina.Matches(s).Select(m => m.Groups[1].Value).ToList();
            if (rutas.Count == 0) continue;
            var acceso = new HashSet<string>(StringComparer.Ordinal);
            var acciones = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match g in Gate.Matches(s))
            {
                var destino = g.Groups[1].Value.Contains("MostrarAviso=\"true\"", StringComparison.Ordinal) ? acceso : acciones;
                var r = Requerido.Match(g.Groups[1].Value);
                if (r.Success) destino.Add(r.Groups[1].Value.Trim());
                var a = Alguno.Match(g.Groups[1].Value);
                if (a.Success) destino.UnionWith(Literal.Matches(a.Groups[1].Value).Select(x => x.Groups[1].Value));
            }
            acciones.ExceptWith(acceso);
            foreach (var ruta in rutas) pantallas[ruta] = (acceso, acciones);
        }

        static HashSet<string> Lista(string texto) => Literal.Matches(texto).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        var distintas = Entrada.Matches(Mapa(root))
            .Select(m => (Ruta: m.Groups["ruta"].Value, Acceso: Lista(m.Groups["acceso"].Value), Acciones: Lista(m.Groups["acciones"].Value)))
            .Where(e =>
            {
                var ruta = e.Ruta.Split('?')[0];
                var (acceso, acciones) = pantallas.TryGetValue(ruta, out var p) ? p : ([], []);
                return !acceso.SetEquals(e.Acceso) || !acciones.SetEquals(e.Acciones);
            })
            .Select(e => e.Ruta)
            .Distinct()
            .ToList();

        Assert.True(distintas.Count == 0, $"{Aviso}\nPantallas cuyos permisos cambiaron:\n  " + string.Join("\n  ", distintas));
    }
    /// <summary>
    /// 2026-11-23: toda opción del menú cuya pantalla pide un permiso para abrir está envuelta en un <c>&lt;PermissionGate&gt;</c>
    /// o en <c>&lt;OpcionPermitida&gt;</c>; si no, aparece en el menú de quien no puede entrar.
    /// </summary>
    [Fact]
    public void Cada_opcion_del_menu_se_oculta_sin_permiso()
    {
        var root = RepoPath.FindRepoRoot();
        var menu = Regex.Replace(File.ReadAllText(Path.Combine(Shared(root), "Layout", "NavMenu.razor")), @"@\*.*?\*@", "", RegexOptions.Singleline);
        var conAcceso = Entrada.Matches(Mapa(root))
            .Where(m => m.Groups["acceso"].Value.Trim().Length > 0)
            .Select(m => m.Groups["ruta"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var profundidad = 0;
        var envuelta = false;
        var sueltas = new List<string>();
        foreach (Match t in Regex.Matches(menu, @"<PermissionGate\b[^>]*?(/?)>|</PermissionGate>|<OpcionPermitida\b[^>]*>|</OpcionPermitida>|<NavLink\s+href=""([^""]+)"""))
        {
            var v = t.Value;
            if (v.StartsWith("<PermissionGate", StringComparison.Ordinal)) { if (t.Groups[1].Value != "/") profundidad++; }
            else if (v == "</PermissionGate>") profundidad--;
            else if (v.StartsWith("<OpcionPermitida", StringComparison.Ordinal)) envuelta = true;
            else if (v == "</OpcionPermitida>") envuelta = false;
            else if (profundidad == 0 && !envuelta && conAcceso.Contains(t.Groups[2].Value)) sueltas.Add(t.Groups[2].Value);
        }

        Assert.True(sueltas.Count == 0,
            "Opciones del menú que se ven sin permiso; envolvelas en <OpcionPermitida Ruta=\"…\">:\n  " + string.Join("\n  ", sueltas));
    }
}
