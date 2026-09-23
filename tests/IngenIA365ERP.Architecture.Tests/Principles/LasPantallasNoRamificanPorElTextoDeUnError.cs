using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Una pantalla decide por el <b>código</b> del error, nunca por su texto. El mensaje está escrito
/// para quien lo lee: se reescribe, se traduce, se le corrige una tilde, y nada de eso debería
/// cambiar el comportamiento de la aplicación.
///
/// <para>
/// El 2026-09-22 costó justamente eso. Tres razones distintas compartían el código
/// <c>Payroll.ConfirmationRequired</c> (falta <c>confirm</c>, corrida vacía, y aprobar sin
/// segregación de funciones), así que Liquidación, Prima y Cesantías distinguían la tercera buscando
/// «segregaci» en el mensaje. Esa palabra no aparecía en él —lo más parecido era
/// <c>confirmWithoutSegregation</c>, que en inglés lleva «segregati», no «segregaci»—, de modo que la
/// casilla de la segunda confirmación no se mostraba nunca y <b>aprobar la propia nómina era
/// imposible desde la aplicación</b>, aunque la cooperativa lo permitiera por política. El arreglo no
/// fue corregir la búsqueda sino darle a ese caso su propio código.
/// </para>
///
/// <para>
/// Mostrar el mensaje está bien y es lo normal; lo que esta regla prohíbe es <b>mirarlo dentro de una
/// condición</b>. Si hacen falta dos comportamientos, hacen falta dos códigos.
/// </para>
/// </summary>
public class LasPantallasNoRamificanPorElTextoDeUnError
{
    /// <summary><c>ErrorMessage</c> (o <c>Message</c>, sobre un error) dentro de un Contains/StartsWith/IndexOf.</summary>
    private static readonly Regex PorElTexto = new(
        @"(?:ErrorMessage|Error\??\.Message)\b[^;]{0,80}?\.(?:Contains|StartsWith|EndsWith|IndexOf)\s*\(",
        RegexOptions.Compiled);

    [Fact]
    public void Ninguna_pantalla_de_Shared_mira_dentro_del_mensaje_para_decidir()
    {
        var raiz = RepoPath.FindRepoRoot();
        var shared = Path.Combine(raiz, "src", "Presentation", "IngenIA365ERP.Shared");
        var infractores = new List<string>();

        foreach (var archivo in Directory.EnumerateFiles(shared, "*.*", SearchOption.AllDirectories)
                     .Where(a => a.EndsWith(".cs", StringComparison.Ordinal) || a.EndsWith(".razor", StringComparison.Ordinal))
                     .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
                if (PorElTexto.IsMatch(lineas[i]))
                    infractores.Add($"{Path.GetRelativePath(shared, archivo).Replace(Path.DirectorySeparatorChar, '/')}:{i + 1}");
        }

        Assert.True(infractores.Count == 0,
            "Estas pantallas deciden leyendo el texto de un mensaje de error. Si el servidor tiene que\n" +
            "distinguir dos situaciones, que devuelva dos códigos; el texto es para quien lo lee:\n  "
            + string.Join("\n  ", infractores));
    }
}
