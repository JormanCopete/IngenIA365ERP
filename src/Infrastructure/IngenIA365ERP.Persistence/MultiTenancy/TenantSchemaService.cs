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
    /// <summary>
    /// Opciones, no un contexto del contenedor. En cuanto el esquema se resuelve
    /// por peticion, el contexto que da el contenedor apunta a la cooperativa en
    /// curso — y este servicio necesita SIEMPRE el arbol de migraciones de dbo.
    /// Con el contexto ambiente, aprovisionar la cooperativa B desde una peticion
    /// de la A generaria el script ya calificado con el esquema de A: TranslateSchema
    /// busca la cadena literal "dbo" y no encontraria nada que traducir, asi que las
    /// 289 tablas de B se crearian DENTRO de A. Sin excepcion y sin log.
    /// </summary>
    private readonly DbContextOptions<ApplicationDbContext> _appDbOptions;
    private readonly IDbProviderConfigurator _configurator;
    private readonly string _operationalConnectionString;
    private readonly ILogger<TenantSchemaService> _logger;

    private readonly Seeding.SeedOrchestrator? _seedOrchestrator;

    public TenantSchemaService(
        TenantDbContext tenantDb,
        DbContextOptions<ApplicationDbContext> appDbOptions,
        IDbProviderConfigurator configurator,
        IOptions<DatabaseOptions> options,
        ILogger<TenantSchemaService>? logger = null,
        Seeding.SeedOrchestrator? seedOrchestrator = null)
    {
        _tenantDb = tenantDb;
        _appDbOptions = appDbOptions;
        _configurator = configurator;
        _operationalConnectionString = options.Value.GetActiveConnectionString();
        _logger = logger ?? NullLogger<TenantSchemaService>.Instance;
        _seedOrchestrator = seedOrchestrator;
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

        // FR-019a: el alta de tenant siembra su esquema (parametrico siempre;
        // demo segun la politica de ambiente/flag).
        if (_seedOrchestrator is not null)
        {
            await _seedOrchestrator.RunAsync(
                Seeding.SeedCategory.Parametric, Seeding.SeedScope.Tenant, identifier, CancellationToken.None);
            if (_seedOrchestrator.EffectiveRunTestSeed())
                await _seedOrchestrator.RunAsync(
                    Seeding.SeedCategory.Test, Seeding.SeedScope.Tenant, identifier, CancellationToken.None);
        }

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
        await using var dboDb = new ApplicationDbContext(_appDbOptions);
        var migrator = dboDb.Database.GetService<IMigrator>();
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
        using var dboDb = new ApplicationDbContext(_appDbOptions);
        var all = dboDb.Database.GetMigrations().ToList();

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
    /// <summary>
    /// Reescribe el script de migracion para que apunte al esquema del tenant.
    /// </summary>
    /// <remarks>
    /// La sentencia CREATE SCHEMA se trata APARTE. Los reemplazos de abajo
    /// buscan "dbo". / dbo. / 'dbo', o sea el esquema usado como CALIFICADOR,
    /// siempre seguido de un punto o entre comillas. Pero el script trae
    /// tambien `CREATE SCHEMA dbo;` —sin punto y sin comillas—, que no encaja
    /// en ningun patron y sobrevivia sin traducir. El resultado era que
    /// aprovisionar una cooperativa intentaba crear el esquema dbo, que ya
    /// existe, y fallaba con 42P06; el esquema del tenant NO se creaba nunca.
    /// Se emite con IF NOT EXISTS para que reaprovisionar sea idempotente.
    /// </remarks>
    internal static string TranslateSchema(string script, string schemaName, DatabaseProvider provider)
    {
        if (provider == DatabaseProvider.PostgreSql)
        {
            script = Regex.Replace(
                script,
                @"CREATE\s+SCHEMA\s+(IF\s+NOT\s+EXISTS\s+)?""?dbo""?",
                $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"",
                RegexOptions.IgnoreCase);

            return script
                .Replace("\"dbo\".", $"\"{schemaName}\".", StringComparison.Ordinal)
                .Replace(" dbo.", $" \"{schemaName}\".", StringComparison.Ordinal)
                .Replace("'dbo'", $"'{schemaName}'", StringComparison.Ordinal);
        }

        script = Regex.Replace(
            script,
            @"CREATE\s+SCHEMA\s+\[?dbo\]?",
            $"CREATE SCHEMA [{schemaName}]",
            RegexOptions.IgnoreCase);

        return script
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
