using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Shared.Services.Tablero;

/// <summary>
/// 2026-11-10 (pedido del dueño): las opciones que se pueden poner como acceso directo en el tablero. No hay una segunda
/// lista que mantener: se leen del propio menú (<c>Layout/NavMenu.razor</c>, incrustado en el ensamblado), con el grupo al
/// que pertenecen y los permisos de su <c>PermissionGate</c>, para ofrecer a cada quien sólo lo que puede abrir.
/// </summary>
public static partial class CatalogoDeOpciones
{
    /// <summary>Una opción del menú: ruta, nombre, grupo, el ícono del grupo y los permisos que la abren (vacío = cualquiera).</summary>
    public sealed record Opcion(string Ruta, string Nombre, string Grupo, string Icono, IReadOnlyList<string> Permisos);

    private static readonly Lazy<IReadOnlyList<Opcion>> Todas = new(() => Leer(Fuente()));

    public static IReadOnlyList<Opcion> Opciones => Todas.Value;

    public static Opcion? Buscar(string ruta) =>
        Opciones.FirstOrDefault(o => string.Equals(o.Ruta, ruta, StringComparison.OrdinalIgnoreCase));

    private static string Fuente()
    {
        using var flujo = typeof(CatalogoDeOpciones).Assembly.GetManifestResourceStream("IngenIA365ERP.Shared.NavMenu.razor");
        if (flujo is null) return "";
        using var lector = new StreamReader(flujo);
        return lector.ReadToEnd();
    }

    /// <summary>Recorre el menú línea a línea: grupos, puertas de permiso abiertas y enlaces de una línea.</summary>
    internal static IReadOnlyList<Opcion> Leer(string fuente)
    {
        var opciones = new List<Opcion>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var grupo = "General";
        var icono = "e-home";
        var puertas = new Stack<IReadOnlyList<string>>();

        foreach (var linea in fuente.Split('\n'))
        {
            if (Grupo().Match(linea) is { Success: true } g)
            {
                icono = g.Groups["icono"].Value;
                grupo = g.Groups["titulo"].Value;
                puertas.Clear();
            }

            foreach (Match puerta in Puerta().Matches(linea))
                puertas.Push(Permiso().Matches(puerta.Value).Select(m => m.Groups[1].Value).ToList());

            foreach (Match enlace in Enlace().Matches(linea))
            {
                var ruta = enlace.Groups["ruta"].Value;
                var nombre = WebUtility.HtmlDecode(Etiquetas().Replace(enlace.Groups["texto"].Value, "")).Trim();
                if (ruta == "/" || nombre.Length == 0 || !vistas.Add(ruta)) continue;
                opciones.Add(new Opcion(ruta, nombre, grupo, icono, puertas.SelectMany(p => p).Distinct().ToList()));
            }

            for (var i = 0; i < CuentaDeCierres().Matches(linea).Count && puertas.Count > 0; i++) puertas.Pop();
        }

        return opciones;
    }

    [GeneratedRegex(@"<NavMenuGroup\s+Icon=""(?<icono>e-[a-z0-9-]+)""\s+Title=""(?<titulo>[^""]+)""")]
    private static partial Regex Grupo();

    [GeneratedRegex(@"<PermissionGate\b[^>]*>")]
    private static partial Regex Puerta();

    [GeneratedRegex(@"\""([A-Z][A-Za-z]+(?:\.[A-Za-z]+)+)\""")]
    private static partial Regex Permiso();

    [GeneratedRegex(@"</PermissionGate>")]
    private static partial Regex CuentaDeCierres();

    [GeneratedRegex(@"<NavLink\s+href=""(?<ruta>/[^""]*)""[^>]*>(?<texto>.*?)</NavLink>")]
    private static partial Regex Enlace();

    [GeneratedRegex("<[^>]+>|@[^ <]+")]
    private static partial Regex Etiquetas();
}
