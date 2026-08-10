namespace IngenIA365ERP.Application.Common.Interfaces.Database;

/// <summary>
/// Estado operativo de la base de datos para la consola master (feature 004 —
/// contracts/database-admin.md). Nunca expone cadenas de conexion.
/// </summary>
public interface IDatabaseStatusReader
{
    Task<DatabaseStatusDto> GetStatusAsync(CancellationToken ct);
}

public sealed record DatabaseStatusDto(
    string Provider,
    bool AutoMigrate,
    DatabaseScopeStatusDto Admin,
    DatabaseScopeStatusDto Application,
    IReadOnlyList<TenantSchemaStatusDto> Tenants,
    bool RunParametricSeed,
    bool? RunTestSeed);

public sealed record DatabaseScopeStatusDto(int MigrationsApplied, IReadOnlyList<string> MigrationsPending);

public sealed record TenantSchemaStatusDto(Guid TenantPublicId, string Name, string Schema, IReadOnlyList<string> MigrationsPending);
