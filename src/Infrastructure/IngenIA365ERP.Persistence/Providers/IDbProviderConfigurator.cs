using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Providers;

/// <summary>
/// A que arbol de migraciones apunta un DbContext:
///  - Application → migraciones del esquema operativo (ApplicationDbContext).
///  - Admin       → migraciones de la BD administrativa (AdminDbContext).
///  - None        → el contexto NUNCA migra (TenantDbContext comparte la BD
///                  admin y su esquema lo gobiernan las migraciones Admin).
/// </summary>
public enum MigrationsTarget
{
    Application,
    Admin,
    None
}

/// <summary>
/// Unico punto del sistema autorizado a invocar UseSqlServer/UseNpgsql
/// (blindado por Architecture.Tests, feature 004). Encapsula tambien las
/// opciones especificas del proveedor: MigrationsAssembly, historial y
/// resiliencia de conexion.
/// </summary>
public interface IDbProviderConfigurator
{
    DatabaseProvider Provider { get; }

    void Configure(DbContextOptionsBuilder options, string connectionString, MigrationsTarget target);

    /// <summary>Conexion ADO nativa del proveedor (para lock de arranque y DDL de tenant).</summary>
    System.Data.Common.DbConnection CreateConnection(string connectionString);

    /// <summary>
    /// Lock exclusivo de inicializacion sobre la conexion dada (D-04):
    /// sp_getapplock en SQL Server, pg_advisory_lock en PostgreSQL. Bloquea
    /// hasta obtenerlo (o falla con timeout claro). Se libera con
    /// <see cref="ReleaseInitializationLockAsync"/> o al cerrar la conexion.
    /// </summary>
    Task AcquireInitializationLockAsync(System.Data.Common.DbConnection connection, CancellationToken ct);

    Task ReleaseInitializationLockAsync(System.Data.Common.DbConnection connection, CancellationToken ct);
}
