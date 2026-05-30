using IngenIA365ERP.Architecture.Tests.Helpers;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio IV — Multi-tenancy estricta. Como check mecánico mínimo,
/// prohibimos ejecutar SQL crudo con el wildcard <c>*</c> cruzando
/// schemas (típico smell de cross-tenant join), salvo dentro del
/// proyecto <c>Persistence</c> donde la convención multi-tenant
/// ya está encapsulada.
/// </summary>
public class PrincipioIV_MultiTenancy
{
    private static readonly Regex CrossSchemaSelect = new(
        @"FROM\s+\[?[A-Za-z0-9_]+\]?\.\[?dbo\]?\.\[?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void No_production_file_should_select_from_other_database_dot_dbo()
    {
        var offenders = RepoPath.ProductionCSharpFiles()
            .Where(f => !f.Contains("IngenIA365ERP.Persistence", StringComparison.OrdinalIgnoreCase))
            .Where(f => CrossSchemaSelect.IsMatch(File.ReadAllText(f)))
            .Select(f => f.Replace(RepoPath.FindRepoRoot(), string.Empty))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Posibles cross-tenant joins:\n  " + string.Join("\n  ", offenders));
    }
}
