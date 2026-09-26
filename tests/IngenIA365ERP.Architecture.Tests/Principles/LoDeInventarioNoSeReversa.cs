using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T29, T18, §2.18), FR-002 y FR-008: un documento de
/// inventario confirmado no se reversa ni se edita: se corrige con un documento <b>nuevo</b>
/// (anulación, nota crédito o débito, nota de ajuste, ajuste de inventario) que deja su propio
/// rastro. La reversa en espejo es de Contabilidad (FR-038 de la 009) y la hace el módulo dueño
/// del comprobante, no Inventario.
///
/// <para>
/// Esqueleto del Setup: <see cref="CarpetasDeInventario"/> (relativas a <c>src/</c>) empieza vacía y
/// la prueba afirma la regla sobre cada elemento; con la lista vacía pasa porque no hay nada que
/// violar, no por un <c>return</c> temprano. La llena la base de inventario (fase 3) con las
/// carpetas del módulo nuevo; una carpeta que todavía no existe cuenta como vacía.
/// </para>
/// </summary>
public class LoDeInventarioNoSeReversa
{
    /// <summary>Carpetas relativas a <c>src/</c> del módulo comercial. Las agrega la base de inventario.</summary>
    private static readonly string[] CarpetasDeInventario = [];

    /// <summary>Un identificador de reversa: comando, método o propiedad (no un comentario).</summary>
    private static readonly Regex Reversa = new(@"\bRevers\w*\s*[(<{=]|\b(class|record)\s+\w*Revers\w*", RegexOptions.Compiled);

    [Fact]
    public void El_modulo_comercial_no_reversa_documentos()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var carpeta in CarpetasDeInventario)
        {
            var ruta = Path.Combine(root, "src", carpeta);
            if (!Directory.Exists(ruta)) continue; // carpeta aún no creada: no tiene nada que violar

            foreach (var archivo in Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories))
            {
                var lineas = File.ReadAllLines(archivo);
                for (var i = 0; i < lineas.Length; i++)
                {
                    var linea = lineas[i].TrimStart();
                    if (linea.StartsWith("//", StringComparison.Ordinal) || linea.StartsWith("*", StringComparison.Ordinal)) continue;
                    if (Reversa.IsMatch(linea))
                        infractores.Add($"{Path.GetRelativePath(root, archivo)}:{i + 1}: {linea}");
                }
            }
        }

        Assert.True(infractores.Count == 0,
            "Reversas en el módulo comercial: se corrige con un documento nuevo (FR-002, T29):\n  " + string.Join("\n  ", infractores));
    }
}
