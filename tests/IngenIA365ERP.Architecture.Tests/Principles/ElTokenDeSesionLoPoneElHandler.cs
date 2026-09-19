using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Ningún cliente ni pantalla de Shared pone <c>Authorization: Bearer</c> con el access de la
/// sesión: lo pone <c>RenovacionDeSesionHandler</c>, que es el único que renueva antes de que
/// venza y reintenta tras un 401. Hasta el 2026-09-18 quince clientes tipados lo ponían a mano
/// con <c>CurrentAccessToken</c>, el handler se apartaba al ver la cabecera, y a los quince
/// minutos del último canje Nómina, Personas o Contabilidad respondían «La sesión expiró»
/// mientras las pantallas con <c>Http.GetAsync</c> a secas seguían andando.
///
/// Se permiten sólo: los dos handlers, el renovador (canje y cierre por inactividad, con
/// <c>SinSesion</c>), <c>CentralAuthClient</c> (tokens de desafío y el cierre de sesión) y
/// <c>ProfileClient</c> (elige desafío o sesión en tiempo de ejecución; el handler reconoce
/// la de sesión y la reemplaza).
/// </summary>
public class ElTokenDeSesionLoPoneElHandler
{
    private static readonly string[] Permitidos =
    [
        "Services/AuthBearerHandler.cs",
        "Services/RenovacionDeSesionHandler.cs",
        "Services/Security/RenovadorDeSesion.cs",
        "Services/Security/CentralAuthClient.cs",
        "Services/Security/ProfileClient.cs",
    ];

    private static readonly Regex Cabecera = new(@"Headers\.Authorization\s*=\s*new\s+(?:System\.Net\.Http\.Headers\.)?AuthenticationHeaderValue\(\s*""Bearer""", RegexOptions.Compiled);

    [Fact]
    public void Solo_los_handlers_y_el_flujo_de_ingreso_ponen_la_cabecera_Bearer()
    {
        var raiz = RepoPath.FindRepoRoot();
        var shared = Path.Combine(raiz, "src", "Presentation", "IngenIA365ERP.Shared");
        var infractores = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(shared, "*.*", SearchOption.AllDirectories)
                     .Where(a => a.EndsWith(".cs", StringComparison.Ordinal) || a.EndsWith(".razor", StringComparison.Ordinal))
                     .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var relativo = Path.GetRelativePath(shared, archivo).Replace(Path.DirectorySeparatorChar, '/');
            if (Permitidos.Contains(relativo)) continue;
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
                if (Cabecera.IsMatch(lineas[i])) infractores.Add($"{relativo}:{i + 1}");
        }

        Assert.True(infractores.Count == 0,
            "Estos archivos ponen Authorization: Bearer a mano; hay que quitarlo y dejar que RenovacionDeSesionHandler " +
            "ponga el token vigente (y reintente tras un 401):\n  " + string.Join("\n  ", infractores));
    }
}
