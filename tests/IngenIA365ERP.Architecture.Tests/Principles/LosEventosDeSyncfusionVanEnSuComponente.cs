using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Los eventos de <c>SfGrid</c> y <c>SfTreeGrid</c> (RowSelected, OnActionBegin…) no son
/// parámetros del componente: van en <c>&lt;GridEvents&gt;</c> / <c>&lt;TreeGridEvents&gt;</c>.
/// Escritos como atributo del grid compilan igual —el componente captura los atributos que no
/// conoce y los pinta en el HTML— y el evento nunca se dispara. Así estuvo el plan de cuentas de
/// la feature 009: la fila no elegía nada y «Nueva auxiliar» nunca se habilitaba. Es una prueba
/// sobre el fuente porque ninguna de unidad ve un atributo HTML muerto.
/// </summary>
public class LosEventosDeSyncfusionVanEnSuComponente
{
    private static readonly string[] Eventos =
    [
        "RowSelected", "RowSelecting", "RowDeselected", "RowDeselecting", "OnRecordClick", "OnRecordDoubleClick",
        "OnActionBegin", "OnActionComplete", "OnActionFailure", "RowDataBound", "QueryCellInfo", "DataBound",
        "OnToolbarClick", "OnLoad", "Created", "Destroyed", "OnCellSave", "OnBeginEdit", "CellSaved", "RowUpdated",
        "Expanded", "Expanding", "Collapsed", "Collapsing",
    ];

    /// <summary>La etiqueta de apertura completa del grid, con todos sus atributos (puede ocupar varias líneas).</summary>
    private static readonly Regex Etiqueta = new(@"<Sf(?:Tree)?Grid\b[^>]*>", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex Atributo = new(@"\s(" + string.Join("|", Eventos) + @")\s*=", RegexOptions.Compiled);

    [Fact]
    public void Ningun_grid_lleva_un_evento_como_atributo_directo()
    {
        var raiz = RepoPath.FindRepoRoot();
        var infractores = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(Path.Combine(raiz, "src"), "*.razor", SearchOption.AllDirectories))
        {
            var contenido = File.ReadAllText(archivo);
            foreach (Match etiqueta in Etiqueta.Matches(contenido))
            {
                var evento = Atributo.Match(etiqueta.Value);
                if (evento.Success)
                    infractores.Add($"{Path.GetRelativePath(raiz, archivo)}: {evento.Groups[1].Value} en {etiqueta.Value[..Math.Min(60, etiqueta.Value.Length)]}…");
            }
        }

        Assert.True(infractores.Count == 0,
            "Eventos de Syncfusion escritos como atributo del grid (no se disparan nunca): " +
            "van en <GridEvents TValue=…> o <TreeGridEvents TValue=…> dentro del grid.\n  " +
            string.Join("\n  ", infractores));
    }
}
