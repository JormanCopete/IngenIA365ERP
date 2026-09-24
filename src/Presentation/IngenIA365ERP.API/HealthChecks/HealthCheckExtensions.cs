using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;

namespace IngenIA365ERP.API.HealthChecks;

/// <summary>
/// T136 — Healthchecks separados <c>live</c> / <c>ready</c> (US10 Polish).
///
/// <list type="bullet">
///   <item><b>live</b>: el proceso responde — sin chequeos externos. Usado
///     por orquestadores para decidir si reiniciar el container.</item>
///   <item><b>ready</b>: dependencias externas (SQL Server, MongoDB, Redis,
///     BlobStore) están sanas. Si alguna falla, el orquestador deja de
///     enrutar tráfico al pod hasta que se recupere.</item>
/// </list>
/// </summary>
public static class HealthCheckExtensions
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    public static IServiceCollection AddIngenIaHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddHealthChecks();

        // live: trivial — confirma que el proceso responde y el .NET runtime está vivo.
        builder.AddCheck("self", () => HealthCheckResult.Healthy("alive"), tags: [LiveTag]);

        // ready: dependencias externas.
        // Feature 004 (T031): el chequeo de BD es del PROVEEDOR ACTIVO
        // (PostgreSQL o SQL Server segun Database:Provider) + gate del
        // inicializador (no-listo hasta migrar/sembrar). Reemplaza al viejo
        // check fijo de SQL Server sobre DefaultConnection.
        builder.AddCheck<DatabaseInitializationHealthCheck>("db-init", tags: [ReadyTag]);
        builder.AddCheck<ActiveDatabaseHealthCheck>("database", tags: [ReadyTag]);
        var mongoConn = configuration["MongoDb:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(mongoConn))
        {
            builder.AddTypeActivatedCheck<MongoDbHealthCheck>(
                "mongodb", failureStatus: null, tags: [ReadyTag], args: [mongoConn]);
        }
        builder.AddCheck<RedisHealthCheck>("redis", tags: [ReadyTag]);
        // Singleton a proposito: el chequeo recuerda su ultimo veredicto para no escribir en el
        // almacen en cada sonda (ver BlobStoreHealthCheck). AddCheck<T> resuelve de DI, asi que sin
        // este registro seria una instancia nueva cada vez y el recuerdo no serviria de nada.
        services.AddSingleton<BlobStoreHealthCheck>();
        builder.AddCheck<BlobStoreHealthCheck>("blobstore", tags: [ReadyTag]);

        return services;
    }

    public static WebApplication MapIngenIaHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
            ResponseWriter = WriteJsonAsync
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteJsonAsync
        });
        // Ruta legacy Fase 0 (texto plano "Healthy") — equivalente a live.
        app.MapHealthChecks("/api/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag)
        });
        return app;
    }

    private static Task WriteJsonAsync(HttpContext http, HealthReport report)
    {
        http.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        };
        return http.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

internal sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    public SqlServerHealthCheck(string connectionString) => _connectionString = connectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy("SELECT 1 OK");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server unreachable", ex);
        }
    }
}

internal sealed class MongoDbHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    public MongoDbHealthCheck(string connectionString) => _connectionString = connectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var client = new MongoClient(_connectionString);
            var db = client.GetDatabase("admin");
            await db.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: ct);
            return HealthCheckResult.Healthy("ping OK");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB unreachable", ex);
        }
    }
}

internal sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redis;
    public RedisHealthCheck(IServiceProvider sp)
    {
        // Redis es opcional en dev (se reemplaza por MemoryCacheService).
        _redis = sp.GetService(typeof(IConnectionMultiplexer)) as IConnectionMultiplexer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        if (_redis is null)
        {
            return HealthCheckResult.Degraded("Redis no configurado (modo dev memory cache).");
        }
        try
        {
            var db = _redis.GetDatabase();
            var latency = await db.PingAsync();
            return HealthCheckResult.Healthy($"PING {latency.TotalMilliseconds:N1} ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable", ex);
        }
    }
}

internal sealed class BlobStoreHealthCheck : IHealthCheck
{
    /// <summary>
    /// Cada cuánto se escribe de verdad en el almacén. La <c>readinessProbe</c> pega a
    /// <c>/health/ready</c> cada 5 s <b>por pod</b>: escribir y borrar un objeto en cada una serían
    /// ~600.000 PUT al mes por réplica —y otras tantas versiones no vigentes, porque el bucket tiene
    /// versionado— para no enterarse de nada nuevo. Con este intervalo son ~9.000.
    /// </summary>
    private static readonly TimeSpan IntervaloDeSonda = TimeSpan.FromMinutes(5);

    private readonly AttachmentStorageSettings _settings;
    private readonly IBlobStore _store;
    private readonly SemaphoreSlim _puerta = new(1, 1);
    private readonly TimeProvider _reloj;
    private HealthCheckResult? _ultimo;
    private DateTimeOffset _cuando;

    public BlobStoreHealthCheck(IOptions<AttachmentStorageSettings> settings, IBlobStore store, TimeProvider? reloj = null)
    {
        _settings = settings.Value;
        _store = store;
        _reloj = reloj ?? TimeProvider.System;
    }

    /// <summary>
    /// Se lo pregunta al almacén (<c>IBlobStore.ProbarAsync</c>): escribe y borra algo, sea disco o
    /// bucket. Desde 2026-09-22 el health check no sabe cuál de los dos hay detrás —antes daba
    /// «writable» mirando un directorio que con el proveedor S3 no se usa para nada—.
    ///
    /// <para>
    /// La escritura se hace como mucho una vez cada <see cref="IntervaloDeSonda"/> y entre medio se
    /// repite el último veredicto. Lo que importa no se pierde: <b>el primer arranque siempre prueba</b>,
    /// que es cuando un despliegue con la credencial mal puesta tiene que quedarse sin pasar a Ready.
    /// Un fallo posterior tarda a lo sumo ese intervalo en verse, y para eso no está la sonda: el ERP
    /// sigue funcionando con el bucket caído —sólo fallan los adjuntos, y fallan a la vista—.
    /// </para>
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var ahora = _reloj.GetUtcNow();
        if (_ultimo is { } vigente && ahora - _cuando < IntervaloDeSonda) return vigente;

        await _puerta.WaitAsync(ct);
        try
        {
            // Otra sonda pudo haber probado mientras ésta esperaba la puerta.
            if (_ultimo is { } reciente && _reloj.GetUtcNow() - _cuando < IntervaloDeSonda) return reciente;

            HealthCheckResult resultado;
            try
            {
                var donde = await _store.ProbarAsync(ct);
                resultado = HealthCheckResult.Healthy($"{_settings.Provider}: {donde}");
            }
            catch (Exception ex)
            {
                resultado = HealthCheckResult.Unhealthy($"Almacén de adjuntos ({_settings.Provider}) no disponible o de sólo lectura", ex);
            }
            _ultimo = resultado;
            _cuando = _reloj.GetUtcNow();
            return resultado;
        }
        finally
        {
            _puerta.Release();
        }
    }
}
