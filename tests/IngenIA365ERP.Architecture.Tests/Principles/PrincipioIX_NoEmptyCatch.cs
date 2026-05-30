using IngenIA365ERP.Architecture.Tests.Helpers;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio IX — Errores visibles. Ningún archivo de producción contiene
/// un <c>catch</c> con cuerpo vacío (silencia la excepción). Patrones
/// detectados:
///  * <c>catch (X) { }</c>
///  * <c>catch { }</c>
/// El test ignora comentarios dentro del bloque vacío.
/// </summary>
public class PrincipioIX_NoEmptyCatch
{
    // Matches: catch (Optional<Type ident>) { /* whitespace and/or comments */ }
    private static readonly Regex EmptyCatch = new(
        @"catch\s*(\([^)]*\))?\s*\{\s*(/\*.*?\*/|//[^\n]*)?\s*\}",
        RegexOptions.Singleline | RegexOptions.Compiled);

    [Fact]
    public void No_production_file_has_empty_catch_block()
    {
        var root = RepoPath.FindRepoRoot();
        var offenders = new List<string>();
        foreach (var file in RepoPath.ProductionCSharpFiles())
        {
            var content = File.ReadAllText(file);
            // Optimización: descarta archivos sin la palabra `catch`.
            if (!content.Contains("catch", StringComparison.Ordinal)) continue;
            if (EmptyCatch.IsMatch(content))
                offenders.Add(file.Replace(root, string.Empty));
        }

        Assert.True(offenders.Count == 0,
            "catch vacío detectado (violación de Principio IX):\n  " + string.Join("\n  ", offenders));
    }
}
