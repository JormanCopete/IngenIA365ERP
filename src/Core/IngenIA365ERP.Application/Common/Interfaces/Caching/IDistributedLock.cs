namespace IngenIA365ERP.Application.Common.Interfaces.Caching;

/// <summary>
/// Lock distribuido para serializar trabajo crítico entre instancias de la
/// API (research D-06 — single-use estricto de invitaciones, password reset
/// tokens, MFA enrollment confirmations).
///
/// <para>
/// Patrón de uso típico:
/// <code>
/// await using var handle = await _lock.TryAcquireAsync(
///     $"lock:invitation:{tokenHash}", TimeSpan.FromSeconds(30), ct);
/// if (handle is null) return Result.Failure("Lock.Busy", "Operación en curso por otra sesión.");
/// // ... trabajo crítico ...
/// // dispose auto-libera el lock (release atómico vía token único)
/// </code>
/// </para>
///
/// <para>
/// El TTL del lock es un fail-safe: si el proceso cae sin liberar, Redis
/// expira la key y otra sesión puede adquirir. El handle valida el token
/// al liberar para evitar liberar un lock que ya expiró y fue tomado por
/// otro.
/// </para>
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Intenta adquirir el lock <paramref name="key"/> con un TTL fail-safe.
    /// Returns <c>null</c> si el lock ya está tomado por otra sesión.
    /// </summary>
    Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        CancellationToken ct);
}

/// <summary>
/// Handle del lock adquirido. El <see cref="IAsyncDisposable.DisposeAsync"/>
/// libera el lock de forma atómica: solo borra la key si todavía contiene
/// el token original (evita liberar un lock que ya expiró).
/// </summary>
public interface IDistributedLockHandle : IAsyncDisposable
{
    /// <summary>Key bajo la que se adquirió el lock (útil para logs).</summary>
    string Key { get; }
}
