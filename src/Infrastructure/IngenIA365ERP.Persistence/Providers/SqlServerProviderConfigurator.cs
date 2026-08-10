using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Providers;

public sealed class SqlServerProviderConfigurator : IDbProviderConfigurator
{
    private const string LockResource = "ingenia365:db-init";

    public const string MigrationsAssemblyName = "IngenIA365ERP.Persistence.Migrations.SqlServer";

    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    public void Configure(DbContextOptionsBuilder options, string connectionString, MigrationsTarget target)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 3);
            if (target != MigrationsTarget.None)
            {
                sql.MigrationsAssembly(MigrationsAssemblyName);
                // Esquema explicito: el script idempotente para tenants traduce
                // [dbo] al esquema del tenant, historial incluido (D-03).
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
            }
        });

        if (target == MigrationsTarget.Application)
            options.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory,
                MultiTenancy.SchemaModelCacheKeyFactory>();
    }

    public DbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);

    public async Task AcquireInitializationLockAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "sp_getapplock";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 600;
        cmd.Parameters.Add(new SqlParameter("@Resource", LockResource));
        cmd.Parameters.Add(new SqlParameter("@LockMode", "Exclusive"));
        cmd.Parameters.Add(new SqlParameter("@LockOwner", "Session"));
        cmd.Parameters.Add(new SqlParameter("@LockTimeout", 600_000));
        var result = new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
        cmd.Parameters.Add(result);
        await cmd.ExecuteNonQueryAsync(ct);
        if ((int)(result.Value ?? -999) < 0)
            throw new InvalidOperationException(
                "[Database.MigrationFailed] No fue posible adquirir el lock de inicialización (sp_getapplock).");
    }

    public async Task ReleaseInitializationLockAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "EXEC sp_releaseapplock @Resource = @r, @LockOwner = 'Session';";
        cmd.Parameters.Add(new SqlParameter("@r", LockResource));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
