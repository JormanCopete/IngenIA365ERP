using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Impresos;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// 2026-11-15: el selector de Administración › Usuarios (Shared) guarda en <c>Core.PrintedUserDisplay</c> uno de los modos que el servidor
/// entiende (<see cref="UsuarioEnImpresos.Modos"/>). Uno que sólo esté en la pantalla se guardaría y el servidor lo leería como «Nombre».
/// </summary>
public class LosModosDelUsuarioEnImpresosSonLosMismos
{
    private static readonly Regex Opcion = new(@"\(""([A-Za-z]+)"",\s*""", RegexOptions.Compiled);

    [Fact]
    public void La_pantalla_ofrece_exactamente_los_modos_del_servidor()
    {
        var archivo = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared", "Components", "Identidad", "UsuarioEnImpresosAjuste.razor");
        var texto = File.ReadAllText(archivo);
        var opciones = texto[texto.IndexOf("Opciones =", StringComparison.Ordinal)..];
        var deLaPantalla = Opcion.Matches(opciones).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(UsuarioEnImpresos.Modos.ToHashSet(StringComparer.Ordinal), deLaPantalla);
    }
}
