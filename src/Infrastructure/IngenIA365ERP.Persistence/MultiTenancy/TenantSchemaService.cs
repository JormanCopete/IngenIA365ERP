using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.MultiTenancy;

public class TenantSchemaService
{
    private readonly TenantDbContext _tenantDb;
    private readonly string _connectionString;
    private readonly ILogger<TenantSchemaService> _logger;

    public TenantSchemaService(TenantDbContext tenantDb, IConfiguration config, ILogger<TenantSchemaService> logger)
    {
        _tenantDb = tenantDb;
        _connectionString = config.GetConnectionString("DefaultConnection")!;
        _logger = logger;
    }

    /// <summary>
    /// Constructor for CLI tools (DbMigrator) that don't use IConfiguration/ILogger.
    /// </summary>
    public TenantSchemaService(TenantDbContext tenantDb, string connectionString)
    {
        _tenantDb = tenantDb;
        _connectionString = connectionString;
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantSchemaService>.Instance;
    }

    public async Task<ErpTenantInfo> CreateTenantAsync(string identifier, string name, string? planType = "Basic")
    {
        // 1. Create schema
        var schemaName = $"tenant_{identifier.Replace("-", "_")}";
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = @schema) EXEC('CREATE SCHEMA [' + @schema + ']')";
        cmd.Parameters.AddWithValue("@schema", schemaName);
        await cmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Created schema [{Schema}]", schemaName);

        // 2. Register tenant
        var tenant = new ErpTenantInfo
        {
            Id = Guid.NewGuid().ToString(),
            Identifier = identifier,
            Name = name,
            Schema = schemaName,
            ConnectionString = _connectionString,
            PlanType = planType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _tenantDb.Tenants.Add(tenant);
        await _tenantDb.SaveChangesAsync();

        _logger.LogInformation("Tenant registered: {Identifier} -> schema [{Schema}]", identifier, schemaName);
        return tenant;
    }

    public async Task ApplySchemaAsync(string schemaName)
    {
        // Execute DDL files against the tenant schema
        // This would read the Schema SQL files and replace [dbo] with [schemaName]
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Apply tables (simplified - in production, use EF migrations or SQL scripts)
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            -- Set default schema for this session
            -- In production, read and execute the DDL files with schema replacement
            PRINT N'Schema {schemaName} ready for migration';
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> DropTenantAsync(string identifier)
    {
        var tenant = await _tenantDb.Tenants.FirstOrDefaultAsync(t => t.Identifier == identifier);
        if (tenant == null) return false;

        // Drop schema (dangerous - only for dev)
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        // Drop all objects in schema first, then schema
        cmd.CommandText = @"
            DECLARE @sql NVARCHAR(MAX) = N'';
            SELECT @sql += N'DROP TABLE [' + @schema + N'].[' + TABLE_NAME + N'];' + CHAR(13)
            FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema;
            EXEC sp_executesql @sql;
            EXEC('DROP SCHEMA [' + @schema + ']');
        ";
        cmd.Parameters.AddWithValue("@schema", tenant.Schema);
        await cmd.ExecuteNonQueryAsync();

        _tenantDb.Tenants.Remove(tenant);
        await _tenantDb.SaveChangesAsync();

        return true;
    }

    public async Task<List<ErpTenantInfo>> ListTenantsAsync()
    {
        return await _tenantDb.Tenants.OrderBy(t => t.Identifier).ToListAsync();
    }
}
