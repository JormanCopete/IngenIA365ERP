namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Contador de intentos fallidos para lockout progresivo por email (research D-11).
/// Implementación inicial: <c>RedisLoginAttemptCounter</c>. Por email normalizado,
/// NO por IP — defensa contra atacantes distribuidos sin falsos positivos por NAT.
///
/// <para>Escalado (research D-11):
/// 5 fallos → 1 min · 10 → 5 min · 15 → 15 min · 20+ → 60 min con reset administrativo.</para>
/// </summary>
public interface ILoginAttemptCounter
{
    /// <summary>Comprueba si el email está bloqueado actualmente.</summary>
    Task<LoginLockoutState> CheckAsync(string normalizedEmail, CancellationToken ct);

    /// <summary>Incrementa el contador tras un intento fallido. Retorna el veredicto
    /// (con LockSeconds si dispara bloqueo en este incremento).</summary>
    Task<LoginLockoutVerdict> RecordFailureAsync(string normalizedEmail, CancellationToken ct);

    /// <summary>Resetea el contador tras un login exitoso.</summary>
    Task ResetAsync(string normalizedEmail, CancellationToken ct);
}

public sealed record LoginLockoutState(bool IsLocked, int RetryAfterSeconds, int FailureCount);

public sealed record LoginLockoutVerdict(bool ShouldLock, int LockSeconds, int FailureCount);
