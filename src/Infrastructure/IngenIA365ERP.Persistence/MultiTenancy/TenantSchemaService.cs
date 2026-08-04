using System.Text.RegularExpressions;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// Aprovisionamiento de esquemas de tenant, multi-proveedor desde el feature
/// 004 (T029, spike D-03): crea el esquema en el motor activo y aplica el
/// script idempotente generado desde el arbol de migraciones del proveedor,
/// con traduccion controlada del esquema (dbo → tenant_*), historial
/// <c>__EFMigrationsHistory</c> incluido dentro del esquema del tenant.
/// Usado por el inicializador de arranque (FR-009/FR-019a) y por el alta de
/// tenant en runtime (FR-014).
/// </summary>
public class TenantSchemaService
{
    private readonly TenantDbContext _tenantDb;
    private readonly ApplicationDbContext _appDb;
    private readonly IDbProviderConfigurator _configurator;
    private readonly string _operationalConnectionString;
    private readonly ILogger<TenantSchemaService> _logger;

    public TenantSchemaService(
        TenantDbContext tenantDb,
        ApplicationDbContext appDb,
        IDbProviderConfigurator configurator,
        IOptions<DatabaseOptions> options,
        ILogger<TenantSchemaService>? logger = null)
    {
        _tenantDb = tenantDb;
        _appDb = appDb;
        _configurator = configurator;
        _operationalConnectionString = options.Value.GetActiveConnectionString();
        _logger = logger ?? NullLogger<TenantSchemaService>.Instance;
    }

    public async Task<ErpTenantInfo> CreateTenantAsync(string identifier, string name, string? planType = "Basic")
    {
        var schemaName = $"tenant_{identifier.Replace("-", "_")}";

        await EnsureSchemaExistsAsync(schemaName, CancellationToken.None);
        await MigrateTenantSchemaAsync(schemaName, CancellationToken.None);

        var tenant = new ErpTenantInfo
        {
            Id = Guid.NewGuid().ToString(),
            Identifier = identifier,
            Name = name,
            Schema = schemaName,
            ConnectionString = _operationalConnectionString,
            PlanType = planType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _tenantDb.Tenants.Add(tenant);
        await _tenantDb.SaveChangesAsync();

        _logger.LogInformation("Tenant registrado: {Identifier} → esquema {Schema} ({Provider})",
            identifier, schemaName, _configurator.Provider);
        return tenant;
    }

    public async Task EnsureSchemaExistsAsync(string schemaName, CancellationToken ct)
    {
        ValidateSchemaName(schemaName);
        await using var conn = _configurator.CreateConnection(_operationalConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = _configurator.Provider == DatabaseProvider.PostgreSql
            ? $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\";"
            : $"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{schemaName}') EXEC('CREATE SCHEMA [{schemaName}]');";
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Esquema {Schema} disponible", schemaName);
    }

    /// <summary>
    /// Lleva el esquema del tenant al nivel del arbol de migraciones del
    /// proveedor activo, aplicando el script idempotente con el esquema
    /// traducido. Re-ejecutable sin efectos (idempotente).
    /// </summary>
    public async Task MigrateTenantSchemaAsync(string schemaName, CancellationToken ct)
    {
        ValidateSchemaName(schemaName);
        var migrator = _appDb.Database.GetService<IMigrator>();
        var script = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        var translated = TranslateSchema(script, schemaName, _configurator.Provider);

        await using var conn = _configurator.CreateConnection(_operationalConnectionString);
        await conn.OpenAsync(ct);

        foreach (var batch in SplitBatches(translated, _configurator.Provider))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = batch;
            cmd.CommandTimeout = 600;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        _logger.LogInformation("Esquema {Schema} migrado al nivel actual ({Provider})",
            schemaName, _configurator.Provider);
    }

    /// <summary>Migraciones del arbol del proveedor que el esquema del tenant aun no tiene.</summary>
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(string schemaName, CancellationToken ct)
    {
        ValidateSchemaName(schemaName);
        var all = _appDb.Database.GetMigrations().ToList();

        await using var conn = _configurator.CreateConnection(_operationalConnectionString);
        await conn.OpenAsync(ct);

        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = _configurator.Provider == DatabaseProvider.PostgreSql
                ? $"SELECT \"MigrationId\" FROM \"{schemaName}\".\"__EFMigrationsHistory\";"
                : $"SELECT [MigrationId] FROM [{schemaName}].[__EFMigrationsHistory];";
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    applied.Add(reader.GetString(0));
            }
            catch
            {
                // Historial inexistente ⇒ esquema virgen ⇒ todo pendiente.
                return all;
            }
        }

        return all.Where(m => !applied.Contains(m)).ToList();
    }

    public async Task<bool> DropTenantAsync(string identifier)
    {
        var tenant = await _tenantDb.Tenants.FirstOrDefaultAsync(t => t.Identifier == identifier);
        if (tenant?.Schema is null) return false;
        ValidateSchemaName(tenant.Schema);

        await using var conn = _configurator.CreateConnection(_operationalConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = _configurator.Provider == DatabaseProvider.PostgreSql
            ? $"DROP SCHEMA IF EXISTS \"{tenant.Schema}\" CASCADE;"
            : $@"DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql += N'DROP TABLE [{tenant.Schema}].[' + TABLE_NAME + N'];' + CHAR(13)
                FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = N'{tenant.Schema}';
                EXEC sp_executesql @sql;
                EXEC('DROP SCHEMA [{tenant.Schema}]');";
        cmd.CommandTimeout = 600;
        await cmd.ExecuteNonQueryAsync();

        _tenantDb.Tenants.Remove(tenant);
        await _tenantDb.SaveChangesAsync();
        return true;
    }

    public async Task<List<ErpTenantInfo>> ListTenantsAsync()
        => await _tenantDb.Tenants.OrderBy(t => t.Identifier).ToListAsync();

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Traduce el esquema dbo del script generado al esquema del tenant.
    /// Cubre las tres formas en que los generadores citan el esquema:
    /// [dbo]. (SQL Server), dbo. / "dbo". (PostgreSQL) y N'dbo'/'dbo' en
    /// llamadas a procedimientos del historial.
    /// </summary>
    internal static string TranslateSchema(string script, string schemaName, DatabaseProvider provider)
    {
        return provider == DatabaseProvider.PostgreSql
            ? script
                .Replace("\"dbo\".", $"\"{schemaName}\".", StringComparison.Ordinal)
                .Replace(" dbo.", $" \"{schemaName}\".", StringComparison.Ordinal)
                .Replace("'dbo'", $"'{schemaName}'", StringComparison.Ordinal)
            : script
                .Replace("[dbo].", $"[{schemaName}].", StringComparison.Ordinal)
                .Replace("N'dbo'", $"N'{schemaName}'", StringComparison.Ordinal)
                .Replace("'dbo'", $"'{schemaName}'", StringComparison.Ordinal);
    }

    internal static IEnumerable<string> SplitBatches(string script, DatabaseProvider provider)
    {
        if (provider == DatabaseProvider.PostgreSql)
        {
            yield return script; // Npgsql ejecuta el script completo (sin GO)
            yield break;
        }

        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            yield return batch;
    }

    private static void ValidateSchemaName(string schemaName)
    {
        if (!Regex.IsMatch(schemaName, "^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new ArgumentException(
                $"Nombre de esquema inválido: '{schemaName}'. Solo letras, dígitos y guion bajo.",
                nameof(schemaName));
    }
}
