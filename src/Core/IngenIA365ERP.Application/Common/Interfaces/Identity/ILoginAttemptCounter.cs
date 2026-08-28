namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Qué se está intentando adivinar. Cada ámbito lleva su propio contador.
///
/// <para>
/// <b>No es cosmético.</b> Un login correcto llama a <c>ResetAsync</c>, y a
/// <c>/api/auth/mfa/verify</c> sólo se llega DESPUÉS de acertar la contraseña.
/// Con un contador compartido, quien ya tiene la contraseña y va a por el
/// segundo factor se regalaría un reset del contador cada pocos intentos
/// simplemente volviendo a iniciar sesión: el límite no limitaría nada.
/// </para>
/// </summary>
public enum AmbitoDeIntentos
{
    /// <summary>Contraseña: login y cualquier otra ruta que la valide.</summary>
    Password,

    /// <summary>Segundo factor: TOTP y códigos de recuperación.</summary>
    Mfa,

    /// <summary>
    /// Confirmación de una recuperación del segundo factor por correo, que valida
    /// la contraseña con el enlace ya en la mano.
    ///
    /// <para>
    /// Ámbito propio y no <see cref="Mfa"/> ni <see cref="Password"/>, por el mismo
    /// motivo por el que <see cref="Mfa"/> no es <see cref="Password"/>: aquellos
    /// los resetea un ingreso correcto, y este endpoint es anónimo —se llega desde
    /// un enlace del correo—, así que compartir ámbito regalaría reintentos a quien
    /// pueda provocar un reset por otra vía.
    /// </para>
    /// </summary>
    RecuperacionMfa,
}

/// <summary>
/// Contador de intentos fallidos para lockout progresivo por email (research D-11).
/// Implementación inicial: <c>RedisLoginAttemptCounter</c>. Por email normalizado,
/// NO por IP — defensa contra atacantes distribuidos sin falsos positivos por NAT.
///
/// <para>Escalado (research D-11):
/// 5 fallos → 1 min · 10 → 5 min · 15 → 15 min · 20+ → 60 min por cada intento.</para>
/// </summary>
public interface ILoginAttemptCounter
{
    /// <summary>Comprueba si el email está bloqueado actualmente en ese ámbito.</summary>
    Task<LoginLockoutState> CheckAsync(
        AmbitoDeIntentos ambito, string normalizedEmail, CancellationToken ct);

    /// <summary>Incrementa el contador tras un intento fallido. Retorna el veredicto
    /// (con LockSeconds si dispara bloqueo en este incremento).</summary>
    Task<LoginLockoutVerdict> RecordFailureAsync(
        AmbitoDeIntentos ambito, string normalizedEmail, CancellationToken ct);

    /// <summary>Resetea el contador de ese ámbito tras un intento exitoso.</summary>
    Task ResetAsync(AmbitoDeIntentos ambito, string normalizedEmail, CancellationToken ct);
}

public sealed record LoginLockoutState(bool IsLocked, int RetryAfterSeconds, int FailureCount);

public sealed record LoginLockoutVerdict(bool ShouldLock, int LockSeconds, int FailureCount);
