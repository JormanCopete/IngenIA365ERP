using IngenIA365ERP.Application.Common.Interfaces.Identity;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Identity;

/// <summary>
/// Contador de fallos por email con escalado progresivo (T040, research D-11).
/// Key: <c>intentos:{ambito}:{email}</c> con INCR + EXPIRE atómicos. El bloqueo
/// en sí se escribe en <c>bloqueo:{ambito}:{email}</c> con TTL igual al
/// LockSeconds del nivel disparado.
/// </summary>
internal sealed class RedisLoginAttemptCounter : ILoginAttemptCounter
{
    private const string CounterKeyPrefix = "intentos:";
    private const string LockKeyPrefix = "bloqueo:";


    // El contador EXPIRA si no hay actividad por este tiempo (ventana de "olvido").
    private static readonly TimeSpan CounterTtl = TimeSpan.FromHours(24);

    private readonly IConnectionMultiplexer _redis;

    public RedisLoginAttemptCounter(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<LoginLockoutState> CheckAsync(
        AmbitoDeIntentos ambito, string normalizedEmail, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var lockKey = LockKey(ambito, normalizedEmail);
        var counterKey = CounterKey(ambito, normalizedEmail);

        var ttl = await db.KeyTimeToLiveAsync(lockKey);
        if (ttl is { TotalSeconds: > 0 })
        {
            var failures = (int?)await db.StringGetAsync(counterKey) ?? 0;
            return new LoginLockoutState(IsLocked: true, RetryAfterSeconds: (int)ttl.Value.TotalSeconds, FailureCount: failures);
        }

        var current = (int?)await db.StringGetAsync(counterKey) ?? 0;
        return new LoginLockoutState(IsLocked: false, RetryAfterSeconds: 0, FailureCount: current);
    }

    public async Task<LoginLockoutVerdict> RecordFailureAsync(
        AmbitoDeIntentos ambito, string normalizedEmail, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var counterKey = CounterKey(ambito, normalizedEmail);

        var newCount = (int)await db.StringIncrementAsync(counterKey);
        await db.KeyExpireAsync(counterKey, CounterTtl);

        // La politica vive en EscaladoDeBloqueo, no aqui: el fallo que costo
        // caro era una condicion enterrada entre dos llamadas a Redis, y asi no
        // se podia comprobar sin levantar uno.
        var veredicto = EscaladoDeBloqueo.Decidir(newCount);
        if (!veredicto.ShouldLock) return veredicto;

        await db.StringSetAsync(
            LockKey(ambito, normalizedEmail), "1", TimeSpan.FromSeconds(veredicto.LockSeconds));
        return veredicto;
    }

    public async Task ResetAsync(
        AmbitoDeIntentos ambito, string normalizedEmail, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(CounterKey(ambito, normalizedEmail));
        await db.KeyDeleteAsync(LockKey(ambito, normalizedEmail));
    }

    private static string CounterKey(AmbitoDeIntentos ambito, string normalizedEmail) =>
        $"{CounterKeyPrefix}{Nombre(ambito)}:{normalizedEmail}";

    private static string LockKey(AmbitoDeIntentos ambito, string normalizedEmail) =>
        $"{LockKeyPrefix}{Nombre(ambito)}:{normalizedEmail}";

    /// <summary>Nombre explícito y no <c>ToString()</c>: renombrar el enum no puede mover las claves de Redis.</summary>
    private static string Nombre(AmbitoDeIntentos ambito) => ambito switch
    {
        AmbitoDeIntentos.Password => "password",
        AmbitoDeIntentos.Mfa => "mfa",
        AmbitoDeIntentos.RecuperacionMfa => "recuperacion-mfa",
        _ => throw new ArgumentOutOfRangeException(nameof(ambito), ambito, "Ámbito de intentos desconocido."),
    };
}
