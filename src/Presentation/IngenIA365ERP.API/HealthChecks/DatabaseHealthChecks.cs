using IngenIA365ERP.Persistence.Initialization;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.HealthChecks;

/// <summary>
/// Feature 004 (T031, D-09) — "ready" reporta no-listo mientras el
/// DatabaseInitializerHostedService no haya terminado (migraciones + seeds).
/// </summary>
public sealed class DatabaseInitializationHealthCheck(DatabaseReadiness readiness) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        Task.FromResult(readiness.IsReady
            ? HealthCheckResult.Healthy(readiness.Status)
            : HealthCheckResult.Unhealthy(readiness.Status));
}

/// <summary>
/// Feature 004 (T031) — conectividad con el motor ACTIVO (Database:Provider),
/// sin acoplarse a un proveedor concreto.
/// </summary>
public sealed class ActiveDatabaseHealthCheck(
    IDbProviderConfigurator configurator,
    IOptions<DatabaseOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var conn = configurator.CreateConnection(options.Value.GetActiveConnectionString());
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy($"{options.Value.ProviderKey} accesible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"{options.Value.ProviderKey} inaccesible: {ex.Message}");
        }
    }
}
