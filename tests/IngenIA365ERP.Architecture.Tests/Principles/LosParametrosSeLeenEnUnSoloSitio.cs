using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 (decisiones-transversales T21, §2.18), FR-012: los parámetros con vigencia de
/// plataforma tienen un solo lector, <c>LectorDeParametros</c> —que cae ámbito → general → defecto
/// seguro y rechaza un valor guardado fuera de lo admitido—, y un solo escritor,
/// <c>AddParameterVersionCommandHandler</c>. Quien lee la tabla por su cuenta se salta la caída y
/// la validación.
///
/// <para>
/// Esqueleto del Setup: <see cref="SimbolosDeParametros"/> (el <c>DbSet</c> de
/// <c>ParameterVersion</c> y afines) empieza vacía y la prueba afirma la regla sobre cada elemento;
/// con la lista vacía pasa porque no hay nada que violar, no por un <c>return</c> temprano. La
/// llena la fase 2 (plataforma, T018) al crear la entidad.
/// </para>
/// </summary>
public class LosParametrosSeLeenEnUnSoloSitio
{
    /// <summary>Identificadores restringidos (el DbSet de ParameterVersion). Los agrega la plataforma.</summary>
    private static readonly string[] SimbolosDeParametros = [];

    /// <summary>Nombres de archivo (sin ruta) autorizados; el contexto y su interfaz declaran el DbSet.</summary>
    private static readonly string[] Autorizados =
    [
        "LectorDeParametros.cs",
        "AddParameterVersionCommandHandler.cs",
        "ApplicationDbContext.cs",
        "IApplicationDbContext.cs",
    ];

    [Fact]
    public void Solo_el_lector_y_el_comando_tocan_los_parametros()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var simbolo in SimbolosDeParametros)
        {
            var uso = new Regex($@"\b{Regex.Escape(simbolo)}\b", RegexOptions.Compiled);
            foreach (var (archivo, texto) in fuentes)
            {
                if (Autorizados.Contains(Path.GetFileName(archivo), StringComparer.Ordinal)) continue;
                if (uso.IsMatch(texto))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: usa {simbolo} fuera de LectorDeParametros");
            }
        }

        Assert.True(infractores.Count == 0,
            "Los parámetros con vigencia se leen en un solo sitio (FR-012, T21):\n  " + string.Join("\n  ", infractores));
    }
}
