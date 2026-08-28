using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Los literales del discriminador están escritos dos veces —en Application y en
/// la copia de cliente de <c>IngenIA365ERP.Shared</c>— porque ese proyecto no
/// referencia ninguno: todos sus DTO son espejo, como los de cualquier cliente
/// HTTP.
///
/// <para>
/// Que estén duplicados no es el problema; el problema sería que se separaran sin
/// que nada fallase. Y se separarían en silencio: la pantalla seguiría
/// compilando, seguiría pintando la tabla, y sólo mostraría el literal crudo en
/// la columna «Tipo» — que es justo lo que <c>NombreDelTipo</c> hace con un tipo
/// que no reconoce, para no dejar la celda vacía. Nadie relacionaría eso con un
/// cambio de constante.
/// </para>
/// </summary>
public class LosTiposDeCredencialNoSeDesincronizan
{
    private static readonly string ArchivoEspejo = Path.Combine(
        "src", "Presentation", "IngenIA365ERP.Shared", "Services", "Security", "ProfileClient.cs");

    [Fact]
    public void La_copia_de_cliente_dice_lo_mismo_que_el_servidor()
    {
        var ruta = Path.Combine(RepoPath.FindRepoRoot(), ArchivoEspejo);
        Assert.True(File.Exists(ruta), $"No se encontró {ArchivoEspejo}.");

        var espejo = File.ReadAllText(ruta);

        var esperados = new[]
        {
            $"public const string Totp = \"{TiposDeCredencialMfa.Totp}\";",
            $"public const string WebAuthn = \"{TiposDeCredencialMfa.WebAuthn}\";",
        };

        var faltan = esperados.Where(e => !espejo.Contains(e, StringComparison.Ordinal)).ToList();

        Assert.True(faltan.Count == 0,
            $"La copia de cliente en {ArchivoEspejo} ya no coincide con " +
            "TiposDeCredencialMfa. Falta:\n  " + string.Join("\n  ", faltan));
    }
}
