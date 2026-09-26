using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, US7 (T533; contracts/contabilidad.md §2.8): la pantalla de la matriz reparte los roles en pestañas por familia
/// (<c>Shared/Services/Contabilidad/FamiliasDeRolDeInventario</c>). Shared no referencia Application, así que un rol nuevo en
/// <see cref="RolesDeCuenta"/> quedaría sin pestaña —sus reglas no se verían ni se podrían crear desde la pantalla— sin que nada
/// avisara. Esta prueba lee el fuente y exige que cada rol del catálogo fijo esté en alguna familia.
/// </summary>
public class LaMatrizMuestraTodosLosRoles
{
    [Fact]
    public void Cada_rol_del_catalogo_fijo_tiene_su_pestana()
    {
        var fuente = File.ReadAllText(Path.Combine(RepoPath.FindRepoRoot(),
            "src", "Presentation", "IngenIA365ERP.Shared", "Services", "Contabilidad", "FamiliasDeRolDeInventario.cs"));
        var inicio = fuente.IndexOf("Todas {", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "FamiliasDeRolDeInventario ya no declara Todas.");
        var enPantalla = Regex.Matches(fuente[inicio..], @"""([A-Za-z]+)""").Select(m => m.Groups[1].Value)
            .Where(v => char.IsUpper(v[0])).ToHashSet(StringComparer.Ordinal);

        var catalogo = RolesDeCuenta.Todos.Select(r => r.Codigo).ToHashSet(StringComparer.Ordinal);

        Assert.True(catalogo.IsSubsetOf(enPantalla), "Roles sin pestaña en la matriz: " + string.Join(", ", catalogo.Except(enPantalla)));
    }
}
