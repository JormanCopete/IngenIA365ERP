using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Lending.PersonStatement;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// 2026-11-13: las columnas del estado de cuenta viven en dos sitios —el catálogo cerrado del dominio, que valida la preferencia del usuario, y el
/// de la pantalla (Shared), que pinta cada valor—. Tienen que ser los mismos códigos: uno que sólo esté en la pantalla no se puede guardar, y uno
/// que sólo esté en el dominio no se ve.
/// </summary>
public class LasColumnasDelEstadoDeCuentaSonLasMismas
{
    private static readonly Regex Codigo = new(@"new\(""([a-zA-Z]+)"",", RegexOptions.Compiled);

    [Fact]
    public void La_pantalla_y_el_dominio_tienen_los_mismos_codigos_en_el_mismo_orden()
    {
        var root = RepoPath.FindRepoRoot();
        var archivo = Path.Combine(root, "src", "Presentation", "IngenIA365ERP.Shared", "Services", "Cartera", "EstadoDeCuentaDeLaPersonaClient.cs");
        var texto = File.ReadAllText(archivo);
        var catalogo = texto[texto.IndexOf("public static class ColumnasDelEstadoDeCuenta", StringComparison.Ordinal)..];
        var deLaPantalla = Codigo.Matches(catalogo).Select(m => m.Groups[1].Value).ToList();

        Assert.Equal(ColumnasDelEstadoDeCuenta.Todas.Select(c => c.Codigo).ToList(), deLaPantalla);
    }
}
