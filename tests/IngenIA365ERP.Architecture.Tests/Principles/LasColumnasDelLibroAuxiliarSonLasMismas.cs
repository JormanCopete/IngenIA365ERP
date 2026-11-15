using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Accounting;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// 2026-11-15: las columnas del libro auxiliar viven en dos sitios —el catálogo cerrado del dominio, que valida la preferencia del usuario y arma
/// la tabla, y el de la pantalla (Shared), que pinta las casillas—. Tienen que ser los mismos códigos: uno que sólo esté en la pantalla no se
/// puede guardar, y uno que sólo esté en el dominio no se puede elegir. Molde: <see cref="LasColumnasDelEstadoDeCuentaSonLasMismas"/>.
/// </summary>
public class LasColumnasDelLibroAuxiliarSonLasMismas
{
    private static readonly Regex Codigo = new(@"new\(""([a-zA-Z]+)"",", RegexOptions.Compiled);

    [Fact]
    public void La_pantalla_y_el_dominio_tienen_los_mismos_codigos_en_el_mismo_orden()
    {
        var root = RepoPath.FindRepoRoot();
        var archivo = Path.Combine(root, "src", "Presentation", "IngenIA365ERP.Shared", "Services", "Contabilidad", "ColumnasDelLibroAuxiliar.cs");
        var deLaPantalla = Codigo.Matches(File.ReadAllText(archivo)).Select(m => m.Groups[1].Value).ToList();

        Assert.Equal(ColumnasDelLibroAuxiliar.Todas.Select(c => c.Codigo).ToList(), deLaPantalla);
    }
}
