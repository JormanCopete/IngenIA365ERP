using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Persistence.Initialization;

/// <summary>
/// Inicializador de base de datos (feature 004 — T028, data-model §5).
/// Corre como IHostedService ANTES de que Kestrel acepte trafico:
///  1. Espera la BD con reintentos (ventana configurable, D-05).
///  2. Toma el lock nativo de inicializacion (una sola instancia migra, D-04).
///  3. AutoMigrate=true → migra admin, esquema operativo y esquemas de tenant.
///     AutoMigrate=false + pendientes → fail-fast enumerandolas (FR-011).
///  4. Dispara el seeding de arranque (hook US3, si esta registrado).
/// El fallo final distingue Database.Unreachable de Database.MigrationFailed.
/// </summary>
public sealed class DatabaseInitializerHostedService(
    IServiceProvider serviceProvider,
    IDbProviderConfigurator configurator,
    IOptions<DatabaseOptions> options,
    DatabaseReadiness readiness,
    ILogger<DatabaseInitializerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;

        // 1) Esperar el SERVIDOR de BD (conexion a la base de sistema: la BD
        //    de aplicacion puede no existir aun — la crea Migrate()).
        var serverConnStr = ToServerLevelConnectionString(opts.GetActiveAdminConnectionString(), configurator.Provider);
        await WaitForServerAsync(serverConnStr, opts.Startup, cancellationToken);

        // 2) Lock de inicializacion sobre la base de sistema.
        await using var lockConn = configurator.CreateConnection(serverConnStr);
        await lockConn.OpenAsync(cancellationToken);
        await configurator.AcquireInitializationLockAsync(lockConn, cancellationToken);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var guard = scope.ServiceProvider.GetRequiredService<PendingMigrationsGuard>();

            if (!opts.AutoMigrate)
            {
                var report = await guard.ComputeAsync(cancellationToken);
                if (report.HasAny)
                {
                    var message =
                        $"[Database.MigrationsPending] Hay migraciones pendientes y Database:AutoMigrate está " +
                        $"deshabilitado — {report.Describe()}. Aplique los scripts de DBA " +
                        $"(DbMigrator script --provider {opts.ProviderKey}) o habilite Database:AutoMigrate.";
                    readiness.MarkFailed(message);
                    throw new InvalidOperationException(message);
                }
                logger.LogInformation("Migraciones al día (AutoMigrate deshabilitado — sin cambios).");
            }
            else
            {
                await MigrateAsync("admin", adminDb, cancellationToken);
                await MigrateAsync("operativa", appDb, cancellationToken);

                var tenantSchemas = scope.ServiceProvider.GetRequiredService<TenantSchemaService>();
                foreach (var tenant in await tenantSchemas.ListTenantsAsync())
                {
                    if (string.IsNullOrWhiteSpace(tenant.Schema) ||
                        tenant.Schema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                        continue;
                    await tenantSchemas.MigrateTenantSchemaAsync(tenant.Schema!, cancellationToken);
                }
            }

            // 4) Seeding de arranque (US3): opcional hasta que el orquestador exista.
            var seeding = scope.ServiceProvider.GetService<Seeding.SeedOrchestrator>();
            if (seeding is not null)
                await seeding.RunStartupAsync(cancellationToken);

            readiness.MarkReady();
            logger.LogInformation("Inicialización de base de datos completa ({Provider}).", opts.ProviderKey);
        }
        catch (InvalidOperationException)
        {
            throw; // ya trae codigo Database.*
        }
        catch (Exception ex)
        {
            var message = $"[Database.MigrationFailed] La migración falló: {ex.Message}";
            readiness.MarkFailed(message);
            throw new InvalidOperationException(message, ex);
        }
        finally
        {
            try { await configurator.ReleaseInitializationLockAsync(lockConn, CancellationToken.None); }
            catch (Exception ex) { logger.LogWarning(ex, "No se pudo liberar el lock de inicialización (se libera al cerrar la sesión)."); }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateAsync(string label, Microsoft.EntityFrameworkCore.DbContext context, CancellationToken ct)
    {
        var pending = (await context.Database.CanConnectAsync(ct)
                ? await context.Database.GetPendingMigrationsAsync(ct)
                : context.Database.GetMigrations())
            .ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("BD {Label}: sin migraciones pendientes.", label);
            return;
        }

        logger.LogInformation("BD {Label}: aplicando {Count} migración(es): {Migrations}",
            label, pending.Count, string.Join(", ", pending));
        await context.Database.MigrateAsync(ct);
        logger.LogInformation("BD {Label}: migraciones aplicadas.", label);
    }

    private async Task WaitForServerAsync(string serverConnStr, DatabaseOptions.StartupOptions startup, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(startup.RetryWindowSeconds);
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                await using var conn = configurator.CreateConnection(serverConnStr);
                await conn.OpenAsync(ct);
                if (attempt > 1)
                    logger.LogInformation("Base de datos disponible tras {Attempts} intento(s).", attempt);
                return;
            }
            catch (Exception ex) when (DateTime.UtcNow < deadline)
            {
                logger.LogWarning(
                    "Base de datos no disponible aún ({Conn}) — intento {Attempt}: {Reason}. Reintento en {Interval}s.",
                    ConnectionStringMasker.Mask(serverConnStr), attempt, ex.Message, startup.RetryIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(startup.RetryIntervalSeconds), ct);
            }
            catch (Exception ex)
            {
                var message =
                    $"[Database.Unreachable] No fue posible conectar a la base de datos " +
                    $"({ConnectionStringMasker.Mask(serverConnStr)}) tras {startup.RetryWindowSeconds}s.";
                readiness.MarkFailed(message);
                throw new InvalidOperationException(message, ex);
            }
        }
    }

    /// <summary>Cadena apuntada a la base de sistema (master / postgres) para lock y espera.</summary>
    internal static string ToServerLevelConnectionString(string connectionString, DatabaseProvider provider)
    {
        var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
        var dbKey = builder.ContainsKey("Database") ? "Database"
                  : builder.ContainsKey("Initial Catalog") ? "Initial Catalog" : null;
        if (dbKey is not null)
            builder[dbKey] = provider == DatabaseProvider.PostgreSql ? "postgres" : "master";
        return builder.ConnectionString;
    }
}
