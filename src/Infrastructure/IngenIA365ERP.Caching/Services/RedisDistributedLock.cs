using IngenIA365ERP.Application.Common.Interfaces.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services;

/// <summary>
/// Implementación de <see cref="IDistributedLock"/> sobre Redis usando el
/// patrón "SET NX PX + Lua release atómico". Cada adquisición genera un
/// token GUID único que se valida al liberar — esto evita la condición de
/// carrera clásica en la que un lock expira por TTL y otra sesión lo toma
/// antes de que el dueño original llame DisposeAsync.
///
/// <para>
/// Single-node Redis. Para alta disponibilidad cross-AZ con Redlock real
/// (N nodos + quorum) ver research D-06; está fuera del alcance del MVP
/// — Redis del docker-compose es standalone y el riesgo está acotado.
/// </para>
/// </summary>
internal sealed class RedisDistributedLock(
    IConnectionMultiplexer redis,
    ILogger<RedisDistributedLock> logger) : IDistributedLock
{
    // KEYS[1] = lock key, ARGV[1] = expected token
    // Borra la key solo si su valor coincide con el token; si expiró y otro
    // tomó el lock, esta llamada es no-op.
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string key, TimeSpan ttl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Lock key no puede estar vacía.", nameof(key));
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL debe ser positivo.");

        ct.ThrowIfCancellationRequested();

        var db = redis.GetDatabase();
        var token = Guid.NewGuid().ToString("N");

        var acquired = await db.StringSetAsync(
            key,
            token,
            expiry: ttl,
            when: When.NotExists);

        if (!acquired)
        {
            logger.LogDebug("Lock {Key} no adquirido — ya está tomado.", key);
            return null;
        }

        logger.LogDebug("Lock {Key} adquirido con TTL {Ttl}.", key, ttl);
        return new Handle(db, key, token, logger);
    }

    private sealed class Handle(
        IDatabase db,
        string key,
        string token,
        ILogger logger) : IDistributedLockHandle
    {
        private int _disposed;

        public string Key { get; } = key;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            try
            {
                var result = (long)await db.ScriptEvaluateAsync(
                    ReleaseScript,
                    keys: [key],
                    values: [token]);

                if (result == 0)
                {
                    // El lock ya expiró o fue liberado por otro — no es error,
                    // solo señal de que el trabajo tomó más que el TTL.
                    logger.LogWarning(
                        "Lock {Key} ya no contenía nuestro token al liberar — TTL expirado o ya borrado.",
                        key);
                }
                else
                {
                    logger.LogDebug("Lock {Key} liberado.", key);
                }
            }
            catch (Exception ex)
            {
                // Liberar nunca debe tumbar al caller — el TTL fail-safe expirará
                // el lock naturalmente.
                logger.LogError(ex, "Error liberando lock {Key}; quedará expirando por TTL.", key);
            }
        }
    }
}
