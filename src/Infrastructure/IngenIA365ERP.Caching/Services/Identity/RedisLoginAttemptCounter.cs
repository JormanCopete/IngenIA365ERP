using IngenIA365ERP.Application.Common.Interfaces.Identity;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Identity;

/// <summary>
/// Contador de fallos de login por email con escalado progresivo (T040, research D-11).
/// Key: <c>login-attempts:{normalizedEmail}</c> con INCR + EXPIRE atómicos.
/// El bloqueo en sí se escribe en <c>login-locked:{normalizedEmail}</c> con TTL
/// igual al LockSeconds del nivel disparado.
/// </summary>
internal sealed class RedisLoginAttemptCounter : ILoginAttemptCounter
{
    private const string CounterKeyPrefix = "login-attempts:";
    private const string LockKeyPrefix = "login-locked:";

    // Escalado por research D-11. Umbrales SIEMPRE en orden ascendente.
    private static readonly (int AfterFailures, int LockSeconds)[] Thresholds =
    [
        (5,  60),     // 1 min
        (10, 300),    // 5 min
        (15, 900),    // 15 min
        (20, 3600),   // 60 min — requiere reset administrativo si vuelve a dispararse
    ];

    // El contador EXPIRA si no hay actividad por este tiempo (ventana de "olvido").
    private static readonly TimeSpan CounterTtl = TimeSpan.FromHours(24);

    private readonly IConnectionMultiplexer _redis;

    public RedisLoginAttemptCounter(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<LoginLockoutState> CheckAsync(string normalizedEmail, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var lockKey = LockKey(normalizedEmail);
        var counterKey = CounterKey(normalizedEmail);

        var ttl = await db.KeyTimeToLiveAsync(lockKey);
        if (ttl is { TotalSeconds: > 0 })
        {
            var failures = (int?)await db.StringGetAsync(counterKey) ?? 0;
            return new LoginLockoutState(IsLocked: true, RetryAfterSeconds: (int)ttl.Value.TotalSeconds, FailureCount: failures);
        }

        var current = (int?)await db.StringGetAsync(counterKey) ?? 0;
        return new LoginLockoutState(IsLocked: false, RetryAfterSeconds: 0, FailureCount: current);
    }

    public async Task<LoginLockoutVerdict> RecordFailureAsync(string normalizedEmail, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var counterKey = CounterKey(normalizedEmail);

        var newCount = (int)await db.StringIncrementAsync(counterKey);
        await db.KeyExpireAsync(counterKey, CounterTtl);

        // Determinar el lock más alto que el contador supera (ascendente → último que aplica).
        (int after, int seconds)? hit = null;
        foreach (var (after, seconds) in Thresholds)
        {
            if (newCount >= after) hit = (after, seconds);
        }

        if (hit is null)
        {
            return new LoginLockoutVerdict(ShouldLock: false, LockSeconds: 0, FailureCount: newCount);
        }

        // Aplicar lock solo si el contador acaba de cruzar el umbral (es decir, igual exacto).
        // Esto evita re-bloquear en cada fallo posterior dentro de la misma ventana.
        if (newCount == hit.Value.after)
        {
            await db.StringSetAsync(LockKey(normalizedEmail), "1", TimeSpan.FromSeconds(hit.Value.seconds));
            return new LoginLockoutVerdict(ShouldLock: true, LockSeconds: hit.Value.seconds, FailureCount: newCount);
        }

        // Entre umbrales (ej. 6º fallo cuando el lock de 1min ya está activo) — no re-bloquear.
        return new LoginLockoutVerdict(ShouldLock: false, LockSeconds: 0, FailureCount: newCount);
    }

    public async Task ResetAsync(string normalizedEmail, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(CounterKey(normalizedEmail));
        await db.KeyDeleteAsync(LockKey(normalizedEmail));
    }

    private static string CounterKey(string normalizedEmail) => $"{CounterKeyPrefix}{normalizedEmail}";
    private static string LockKey(string normalizedEmail) => $"{LockKeyPrefix}{normalizedEmail}";
}
