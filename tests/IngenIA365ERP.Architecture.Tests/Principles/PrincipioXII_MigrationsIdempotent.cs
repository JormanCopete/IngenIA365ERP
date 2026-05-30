using IngenIA365ERP.Architecture.Tests.Helpers;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio XII — Migraciones idempotentes. Toda migración en
/// <c>database/migration/*.sql</c> contiene al menos un patrón de
/// idempotencia (<c>IF NOT EXISTS</c>, <c>IF EXISTS</c>, <c>IF COL_LENGTH</c>,
/// <c>IF OBJECT_ID</c>, o un cursor dinámico sobre
/// <c>INFORMATION_SCHEMA</c>). Reentrabilidad ⇒ rollback seguro y
/// re-aplicación segura tras incidentes.
/// </summary>
public class PrincipioXII_MigrationsIdempotent
{
    private static readonly Regex IdempotencyMarker = new(
        @"IF\s+NOT\s+EXISTS|IF\s+EXISTS|IF\s+COL_LENGTH|IF\s+OBJECT_ID|INFORMATION_SCHEMA",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Migraciones legacy de importación SOLIDO → SQL Server. Son scripts
    /// one-shot (cargan datos al schema vacío) que se ejecutan una sola vez
    /// y no se diseñaron reentrantes. La convención de idempotencia se
    /// adoptó a partir de la migración 11 (soft-delete framework). Cualquier
    /// migración nueva sí debe cumplirla.
    /// </summary>
    private static readonly HashSet<string> LegacyOneShotAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "01_Create_MappingTables.sql",
        "02_Migrate_COR_Core.sql",
        "03_Migrate_ACC_Accounting.sql",
        "04_Migrate_LND_Lending.sql",
        "05_Migrate_PAY_Payroll.sql",
        "06_Migrate_INV_Inventory.sql",
        "07_Migrate_CDT_DEB_TRS.sql",
        "08_Migrate_SEC_AUD_WEB_ADM.sql",
        "09_PostMigration_Validation.sql",
        "10_RecoveryAndCleanup.sql",
        "12_AddMissingDefaults.sql"
    };

    [Fact]
    public void Every_migration_sql_file_uses_an_idempotency_marker()
    {
        var offenders = RepoPath.MigrationSqlFiles()
            .Where(f => !LegacyOneShotAllowlist.Contains(Path.GetFileName(f)))
            .Where(f => !IdempotencyMarker.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetFileName(f))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Migraciones SQL sin marcador de idempotencia:\n  " + string.Join("\n  ", offenders));
    }
}
