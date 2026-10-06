using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.16, T98): cada pantalla de Cartera existe con su <c>@page</c>, detrás de su permiso, con
/// <c>IndicadorDeCarga</c>, y el menú la enlaza. Molde: <c>LasPantallasDelCicloComercialEstanEnElMenu</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0007) y la llena cada historia con sus pantallas; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LasPantallasDeCarteraEstanEnElMenu
{
    private static readonly string Shared = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");

    /// <summary>Pantallas: archivo (relativo a Shared), ruta y permiso.</summary>
    private static readonly (string Archivo, string Ruta, string Permiso)[] Pantallas = [];

    [Fact]
    public void Cada_pantalla_existe_protegida_con_indicador_y_en_el_menu()
    {
        var menu = File.ReadAllText(Path.Combine(Shared, "Layout", "NavMenu.razor"));
        var infracciones = new List<string>();
        foreach (var (archivo, ruta, permiso) in Pantallas)
        {
            var camino = Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(camino)) { infracciones.Add($"{archivo}: no existe"); continue; }
            var texto = File.ReadAllText(camino);
            if (!texto.Contains($"@page \"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin @page {ruta}");
            if (!texto.Contains($"Required=\"{permiso}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin permiso {permiso}");
            if (!texto.Contains("<IndicadorDeCarga", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin IndicadorDeCarga");
            if (!menu.Contains($"href=\"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{ruta}: el menú no la enlaza");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Pantallas de Cartera fuera del contrato (T98)");
    }
}
