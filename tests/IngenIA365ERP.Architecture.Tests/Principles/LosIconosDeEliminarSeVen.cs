using IngenIA365ERP.Architecture.Tests.Helpers;
using Xunit;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// 2026-11-10, pedido del dueño desde Compras: el botón de quitar una línea no se veía —sólo el título al pasar el mouse—. En el tema
/// fluent2 de Syncfusion la clase <c>e-delete</c> sólo dibuja dentro de la grilla, el Gantt y la hoja de cálculo; fuera de ellos el
/// botón queda vacío. El ícono de papelera de toda la suite es <c>e-trash</c>.
/// </summary>
public class LosIconosDeEliminarSeVen
{
    [Fact]
    public void Ninguna_pantalla_usa_el_icono_de_eliminar_que_solo_dibuja_la_grilla()
    {
        var shared = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");
        var conElIconoInvisible = Directory.EnumerateFiles(shared, "*.razor", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("e-icons e-delete", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(shared, f))
            .ToList();

        Assert.True(conElIconoInvisible.Count == 0,
            "Use «e-icons e-trash»: «e-delete» no se dibuja fuera de la grilla en fluent2. " + string.Join(", ", conElIconoInvisible));
    }
}
