using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IngenIA365ERP.Persistence.Providers;

public sealed class PostgreSqlProviderConfigurator : IDbProviderConfigurator
{
    private const string LockResource = "ingenia365:db-init";

    public const string MigrationsAssemblyName = "IngenIA365ERP.Persistence.Migrations.PostgreSql";

    public DatabaseProvider Provider => DatabaseProvider.PostgreSql;

    public void Configure(DbContextOptionsBuilder options, string connectionString, MigrationsTarget target)
    {
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            if (target != MigrationsTarget.None)
            {
                npgsql.MigrationsAssembly(MigrationsAssemblyName);
                // Esquema explicito para paridad con SQL Server (alli el default
                // schema del modelo es dbo y el historial cae junto a las tablas).
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
            }
        });

        if (target == MigrationsTarget.Application)
            options.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory,
                MultiTenancy.SchemaModelCacheKeyFactory>();
    }

    public DbConnection CreateConnection(string connectionString) => new NpgsqlConnection(connectionString);

    public async Task AcquireInitializationLockAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_lock(hashtext(@r));";
        var p = cmd.CreateParameter();
        p.ParameterName = "@r";
        p.Value = LockResource;
        cmd.Parameters.Add(p);
        cmd.CommandTimeout = 600;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ReleaseInitializationLockAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_unlock(hashtext(@r));";
        var p = cmd.CreateParameter();
        p.ParameterName = "@r";
        p.Value = LockResource;
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
