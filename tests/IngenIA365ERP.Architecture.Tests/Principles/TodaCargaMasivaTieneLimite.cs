using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// El 2026-11-06 una carga de 11.949 personas tumbó las dos réplicas de producción. Toda ruta que reciba un archivo pasa por un limitador
/// de operaciones simultáneas por réplica: <c>LimiteDeCargas</c>, o <c>LimiteDeAdjuntos</c> si es un adjunto. Es una prueba sobre el fuente
/// porque una ruta nueva sin limitador no rompe ninguna prueba de unidad: sólo vuelve a dejar que dos cargas sumen memoria en el mismo pod.
/// </summary>
public class TodaCargaMasivaTieneLimite
{
    private static readonly Regex RecibeArchivo = new(@"\bIFormFile(Collection)?\b|\bReadFormAsync\b", RegexOptions.Compiled);

    private static readonly Regex TieneLimite = new(@"RequireRateLimiting\(\s*Limite[A-Za-z]+\.Politica\s*\)", RegexOptions.Compiled);

    [Fact]
    public void Toda_ruta_que_recibe_un_archivo_lleva_limitador()
    {
        var endpoints = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Endpoints");
        var infractores = Directory.EnumerateFiles(endpoints, "*.cs", SearchOption.AllDirectories)
            .Where(f => RecibeArchivo.IsMatch(File.ReadAllText(f)) && !TieneLimite.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(endpoints, f))
            .ToList();

        Assert.True(infractores.Count == 0,
            "Rutas que reciben un archivo sin limitador de concurrencia (.RequireRateLimiting(LimiteDeCargas.Politica)): " + string.Join(", ", infractores));
    }
}
