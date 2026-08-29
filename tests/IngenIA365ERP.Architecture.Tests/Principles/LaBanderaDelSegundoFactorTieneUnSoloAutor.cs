using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// <c>ADM_CentralUsers.TwoFactorEnabled</c> es una columna <b>derivada</b>: vale
/// exactamente «tiene al menos una credencial activa». Sólo puede escribirla
/// <c>AspNetCoreIdentityProvider</c>, que la deriva de <c>ContarActivasAsync</c>.
///
/// <para>
/// Esta prueba no es higiene de capas. Es la forma exacta del fallo que este
/// proyecto <b>ya sufrió</b> con <c>SEC_Users.IsMfaEnabled</c>: una columna que
/// alguien dejó de escribir y que nadie notó, porque «No» tiene todo el aspecto
/// de un dato verdadero. Estuvo respondiendo «segundo factor: no» para todo el
/// mundo durante meses y ninguna prueba se puso roja.
/// </para>
///
/// <para>
/// Con esta columna el daño sería mayor, porque no es informativa: el ingreso
/// pregunta por ella. Un segundo autor que la escriba con otro criterio —o que
/// la ponga en <c>false</c> al retirar un autenticador sin mirar los demás— deja
/// gente fuera del sistema, o peor, deja entrar sin segundo factor a quien lo
/// tiene puesto.
/// </para>
///
/// <para>
/// El plan del rediseño la daba por obligatoria y no llegó a escribirse. Ésta es.
/// </para>
/// </summary>
public class LaBanderaDelSegundoFactorTieneUnSoloAutor
{
    private const string UnicoAutor = "AspNetCoreIdentityProvider.cs";

    /// <summary>Asignación a la propiedad: <c>… TwoFactorEnabled = valor</c>, nunca <c>==</c>.</summary>
    private static readonly Regex Escritura =
        new(@"TwoFactorEnabled\s*=\s*[^=]", RegexOptions.Compiled);

    /// <summary>La vía de UserManager. Hoy no la usa nadie, y así debe seguir.</summary>
    private const string ViaDeIdentity = "SetTwoFactorEnabledAsync";

    [Fact]
    public void Solo_AspNetCoreIdentityProvider_escribe_TwoFactorEnabled()
    {
        var infractores = ArchivosQueCuentan()
            .Where(f =>
            {
                var codigo = CodigoSinComentarios(File.ReadAllText(f));
                return Escritura.IsMatch(codigo) || codigo.Contains(ViaDeIdentity, StringComparison.Ordinal);
            })
            .Select(f => Path.GetFileName(f))
            .Where(n => n != UnicoAutor)
            .OrderBy(n => n)
            .ToList();

        Assert.True(infractores.Count == 0,
            $"TwoFactorEnabled es derivada y sólo puede escribirla {UnicoAutor}, que la saca de " +
            "ContarActivasAsync. La escriben también:\n  " +
            string.Join("\n  ", infractores) +
            "\n\nEs el mismo fallo que dejó SEC_Users.IsMfaEnabled diciendo «No» para todo el mundo, " +
            "pero sobre una columna que el ingreso sí consulta.");
    }

    [Fact]
    public void Y_ese_autor_sigue_escribiendola()
    {
        // Sin esto la prueba de arriba pasaría igual de verde el día que alguien
        // borre la última escritura — y entonces la columna se quedaría clavada
        // en su valor de siempre, que es exactamente el defecto que perseguimos.
        var autor = ArchivosQueCuentan()
            .FirstOrDefault(f => Path.GetFileName(f) == UnicoAutor);

        Assert.True(autor is not null, $"No se encontró {UnicoAutor} en src/.");

        var codigo = CodigoSinComentarios(File.ReadAllText(autor!));

        Assert.True(Escritura.IsMatch(codigo),
            $"{UnicoAutor} ya no escribe TwoFactorEnabled. Si la columna dejó de derivarse a " +
            "propósito, esta prueba sobra; si no, la columna quedó congelada y nadie se va a enterar.");
    }

    /// <summary>
    /// Todo <c>src/</c> menos las migraciones, donde <c>TwoFactorEnabled =
    /// table.Column&lt;bool&gt;(…)</c> es la <b>definición</b> de la columna y no
    /// una escritura de su valor.
    /// </summary>
    private static IEnumerable<string> ArchivosQueCuentan() =>
        RepoPath.ProductionCSharpFiles()
            .Where(f => !f.Contains("Persistence.Migrations", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Quita comentarios antes de buscar. Sin esto, un <c>&lt;c&gt;TwoFactorEnabled=true&lt;/c&gt;</c>
    /// dentro de un docstring cuenta como escritura — y hay uno en
    /// <c>ConfirmMfaEnrollmentCommand.cs</c>.
    /// </summary>
    private static string CodigoSinComentarios(string fuente)
    {
        var sinBloques = Regex.Replace(fuente, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return string.Join('\n', sinBloques
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }
}
