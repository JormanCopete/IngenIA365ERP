using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 004 (T054, SC-008): toda migracion EF debe existir EN PAR — mismo
/// nombre logico (sufijo tras el timestamp) y mismo DbContext — en los dos
/// ensamblados de proveedor. Una migracion generada para un solo motor rompe
/// este test (y el gate de CI via tools/scripts/check-migration-parity.ps1).
/// </summary>
public class Feature004_MigrationParity
{
    private static readonly Assembly SqlServerAssembly =
        typeof(Persistence.Migrations.SqlServer.DesignTime.ApplicationDbContextFactory).Assembly;

    private static readonly Assembly PostgreSqlAssembly =
        typeof(Persistence.Migrations.PostgreSql.DesignTime.ApplicationDbContextFactory).Assembly;

    private static Dictionary<string, HashSet<string>> LogicalMigrationsByContext(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => new
            {
                Context = t.GetCustomAttribute<Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute>()?
                    .ContextType.Name ?? "(sin contexto)",
                Id = t.GetCustomAttribute<MigrationAttribute>()?.Id ?? t.Name
            })
            .GroupBy(x => x.Context)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Id.Length > 15 ? x.Id[15..] : x.Id).ToHashSet(StringComparer.Ordinal));

    [Fact]
    public void Toda_migracion_existe_en_ambos_proveedores()
    {
        var sql = LogicalMigrationsByContext(SqlServerAssembly);
        var pg = LogicalMigrationsByContext(PostgreSqlAssembly);

        Assert.Equal(sql.Keys.Order(), pg.Keys.Order());

        foreach (var context in sql.Keys)
        {
            Assert.True(sql[context].SetEquals(pg[context]),
                $"Las migraciones del contexto {context} deben existir en par. " +
                $"Solo SqlServer: [{string.Join(", ", sql[context].Except(pg[context]))}] · " +
                $"Solo PostgreSql: [{string.Join(", ", pg[context].Except(sql[context]))}]. " +
                "Genera la faltante con tools/scripts/add-migration.ps1.");
        }
    }

    [Fact]
    public void Ambos_proveedores_tienen_al_menos_las_migraciones_iniciales()
    {
        Assert.Contains("InitialSchema",
            LogicalMigrationsByContext(SqlServerAssembly).Values.SelectMany(v => v));
        Assert.Contains("InitialSchema",
            LogicalMigrationsByContext(PostgreSqlAssembly).Values.SelectMany(v => v));
    }
}
