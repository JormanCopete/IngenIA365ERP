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
    private readonly AttachmentStorageSettings _settings;
    private readonly IBlobStore _store;

    public BlobStoreHealthCheck(IOptions<AttachmentStorageSettings> settings, IBlobStore store)
    {
        _settings = settings.Value;
        _store = store;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        // El store es Local; basta verificar que el root existe / se puede crear.
        try
        {
            var root = Path.GetFullPath(_settings.LocalRootPath);
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }
            // Probar un write+delete trivial.
            var probe = Path.Combine(root, $".healthcheck-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return Task.FromResult(HealthCheckResult.Healthy($"writable: {root}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("BlobStore not writable", ex));
        }
    }
}
