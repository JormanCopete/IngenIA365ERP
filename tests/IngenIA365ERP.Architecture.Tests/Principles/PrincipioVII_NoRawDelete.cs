using IngenIA365ERP.Architecture.Tests.Helpers;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio VII — Soft-delete (parte 2): ningún archivo de producción
/// ejecuta <c>DELETE FROM</c> sobre tablas auditables vía
/// <c>ExecuteSqlRaw</c>/<c>ExecuteSqlInterpolated</c>, ni invoca
/// <c>RemoveRange</c>/<c>Remove</c> con SQL crudo. El borrado siempre
/// pasa por el <c>SoftDeleteInterceptor</c>.
///
/// Excepciones:
///  * <c>Persistence/Interceptors/SoftDeleteInterceptor.cs</c> — es el
///    autorizador legítimo del borrado físico cuando aplique.
/// </summary>
public class PrincipioVII_NoRawDelete
{
    private static readonly Regex RawDelete = new(
        @"ExecuteSql(Raw|Interpolated)Async?\s*\(\s*[$@]?""\s*DELETE\s+FROM",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] Allowed =
    [
        "IngenIA365ERP.Persistence/Interceptors/SoftDeleteInterceptor.cs",
    ];

    [Fact]
    public void No_production_file_uses_ExecuteSqlRaw_with_DELETE_FROM()
    {
        var root = RepoPath.FindRepoRoot();
        var offenders = RepoPath.ProductionCSharpFiles()
            .Where(f =>
            {
                var rel = f.Replace(root, string.Empty).Replace('\\', '/').TrimStart('/');
                return !Allowed.Any(a => rel.EndsWith(a, StringComparison.OrdinalIgnoreCase));
            })
            .Where(f => RawDelete.IsMatch(File.ReadAllText(f)))
            .Select(f => f.Replace(root, string.Empty))
            .ToList();

        Assert.True(offenders.Count == 0,
            "DELETE crudo encontrado fuera del SoftDeleteInterceptor:\n  "
            + string.Join("\n  ", offenders));
    }
}
