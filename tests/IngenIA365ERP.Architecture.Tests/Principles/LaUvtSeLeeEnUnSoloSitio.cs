using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 (decisiones-transversales T23, §2.18), FR-012: la UVT tiene <b>un solo
/// lector</b>, <c>Application/Common/Taxation/LectorDeUvt</c> (<c>IValorUvt</c>), que lee el código
/// <c>UVT</c> de los parámetros legales a la fecha y falla visible si no hay vigencia. Fuera de
/// nómina, nadie más lee la tabla de parámetros legales: cuando la UVT se promueva a Core, cambia
/// sólo el lector.
///
/// <para>
/// Esqueleto del Setup: <see cref="SimbolosDeUvt"/> (identificadores cuyo uso está restringido)
/// empieza vacía y la prueba afirma la regla sobre cada elemento; con la lista vacía pasa porque no
/// hay nada que violar, no por un <c>return</c> temprano. La llena la tarea que crea
/// <c>LectorDeUvt</c> (fase 2, plataforma) con el <c>DbSet</c> de parámetros legales;
/// <see cref="LectoresAutorizados"/> y <see cref="CarpetasAutorizadas"/> dicen quién puede usarlos.
/// </para>
/// </summary>
public class LaUvtSeLeeEnUnSoloSitio
{
    /// <summary>Identificadores restringidos (p. ej. el DbSet de parámetros legales). Los agrega quien crea el lector.</summary>
    private static readonly string[] SimbolosDeUvt = [];

    /// <summary>Nombres de archivo (sin ruta) autorizados.</summary>
    private static readonly string[] LectoresAutorizados = ["LectorDeUvt.cs"];

    /// <summary>Carpetas (fragmento de ruta con '/') autorizadas: nómina conserva su lectura propia.</summary>
    private static readonly string[] CarpetasAutorizadas = ["/IngenIA365ERP.Application/Payroll/"];

    [Fact]
    public void Solo_el_lector_de_UVT_lee_los_parametros_legales_fuera_de_nomina()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var simbolo in SimbolosDeUvt)
        {
            var uso = new Regex($@"\b{Regex.Escape(simbolo)}\b", RegexOptions.Compiled);
            foreach (var (archivo, texto) in fuentes)
            {
                var relativo = Path.GetRelativePath(root, archivo).Replace('\\', '/');
                if (LectoresAutorizados.Contains(Path.GetFileName(archivo), StringComparer.Ordinal)) continue;
                if (CarpetasAutorizadas.Any(c => ("/" + relativo).Contains(c, StringComparison.Ordinal))) continue;
                if (uso.IsMatch(texto))
                    infractores.Add($"{relativo}: usa {simbolo} fuera de LectorDeUvt");
            }
        }

        Assert.True(infractores.Count == 0,
            "La UVT se lee en un solo sitio (T23):\n  " + string.Join("\n  ", infractores));
    }
}
