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

    /// <summary>
    /// En las pantallas, una cancelación no es un error: el buscador cancela la consulta anterior cada vez que la
    /// persona escribe otra letra, y la pantalla que se cierra cancela lo que esperaba. Ignorar
    /// <c>TaskCanceledException</c> u <c>OperationCanceledException</c> no calla ninguna falla.
    /// </summary>
    private static readonly Regex EmptyCatchSinCancelacion = new(
        @"catch\s*(\((?!\s*(System\.Threading\.Tasks\.)?(TaskCanceledException|OperationCanceledException)\b)[^)]*\))?\s*\{\s*(/\*.*?\*/|//[^\n]*)?\s*\}",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Feature 013 (T0008): las pantallas también. Un <c>catch { }</c> en el <c>@code</c> de una pantalla deja a la
    /// persona mirando una grilla vacía sin saber que algo falló. Al estrenarla había once en pantallas de Asociados,
    /// Nómina, Roles y la cartera heredada; se corrigieron en la misma tarea.
    /// </summary>
    [Fact]
    public void No_production_razor_file_has_empty_catch_block()
    {
        var root = RepoPath.FindRepoRoot();
        var offenders = RepoPath.ProductionRazorFiles()
            .Where(file =>
            {
                var content = File.ReadAllText(file);
                return content.Contains("catch", StringComparison.Ordinal) && EmptyCatchSinCancelacion.IsMatch(content);
            })
            .Select(file => file.Replace(root, string.Empty))
            .ToList();

        Assert.True(offenders.Count == 0,
            "catch vacío en una pantalla (violación de Principio IX):\n  " + string.Join("\n  ", offenders));
    }
}
