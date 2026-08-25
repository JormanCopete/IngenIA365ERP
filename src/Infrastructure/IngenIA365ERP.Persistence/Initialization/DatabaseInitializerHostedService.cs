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

                await PonerAlDiaLasCooperativasAsync(scope, adminDb, cancellationToken);
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
        return Providers.ConnectionStringTargeting.ANivelDeServidor(connectionString, provider);
    }

    /// <summary>
    /// Lleva la base de cada cooperativa al nivel actual, y le asigna ranura de
    /// caché si le falta.
    ///
    /// <para>
    /// <b>Antes migraba ESQUEMAS, y eso se volvió destructivo en silencio.</b> Con
    /// base por cooperativa, <c>SchemaName</c> pasó a valer el nombre de la base
    /// —<c>coop_alfa</c>—, así que este bucle creaba un esquema con ese nombre
    /// <i>dentro de la base plantilla</i>: 277 tablas por cooperativa, en cada
    /// arranque, que nadie lee. Medido: dos cooperativas dejaron 554 tablas de
    /// basura. No filtraba datos, pero dejaba la plantilla con una copia por
    /// cooperativa y hacía creer a quien la mirara que el modelo seguía siendo por
    /// esquema.
    /// </para>
    ///
    /// <para>
    /// Y era la misma asimetría de siempre: el arranque hacía una cosa y el
    /// aprovisionamiento otra para la misma cooperativa. Ahora los dos caminos
    /// llaman al mismo aprovisionador, que es idempotente.
    /// </para>
    /// </summary>
    private async Task PonerAlDiaLasCooperativasAsync(
        IServiceScope scope, DbContext.AdminDbContext adminDb, CancellationToken ct)
    {
        var aprovisionador = scope.ServiceProvider
            .GetService<Application.Common.Interfaces.ITenantDatabaseProvisioner>();
        var ranuras = scope.ServiceProvider
            .GetService<Application.Common.Interfaces.ITenantCacheSlotAllocator>();

        if (aprovisionador is null) return;

        var cooperativas = await adminDb.Tenants
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Name, t.DatabaseName, t.SchemaName, t.ConnectionString,
                               t.Subdomain, t.RedisDbIndex })
            .ToListAsync(ct);

        foreach (var c in cooperativas)
        {
            var baseDeDatos = c.DatabaseName ?? c.SchemaName;
            if (string.IsNullOrWhiteSpace(baseDeDatos) ||
                baseDeDatos.Equals("dbo", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                await aprovisionador.AprovisionarAsync(
                    baseDeDatos, c.Subdomain ?? baseDeDatos, c.ConnectionString, ct);

                // Reparación: una cooperativa registrada antes de que existiera el
                // reparto de ranuras se queda sin ella, y su caché cae al espacio
                // global. No es fuga —la clave lleva la cooperativa— pero es
                // inconsistente, y sin esto haría falta tocar la fila a mano.
                if (c.RedisDbIndex is null && ranuras is not null)
                {
                    var fila = await adminDb.Tenants.FirstAsync(t => t.Id == c.Id, ct);
                    fila.RedisDbIndex = await ranuras.ReservarAsync(c.Name, ct);
                    await adminDb.SaveChangesAsync(ct);
                    logger.LogInformation(
                        "Ranura de caché {Ranura} asignada a {Cooperativa} (le faltaba).",
                        fila.RedisDbIndex, c.Name);
                }
            }
            catch (Exception ex)
            {
                // Una cooperativa rota NO impide arrancar ni afecta a las demas.
                logger.LogError(ex,
                    "No se pudo poner al día la cooperativa {Cooperativa} (base {Base}). " +
                    "El resto sigue.", c.Name, baseDeDatos);
            }
        }
    }
}
