namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Comprobación contra listas de contraseñas conocidas comprometidas (FR-044, research D-04).
/// Implementación inicial: <c>PwnedPasswordService</c> usando HaveIBeenPwned Pwned Passwords v3
/// con k-anonymity (SHA-1 → primeros 5 chars → endpoint <c>/range/{prefix}</c>).
/// </summary>
public interface IPwnedPasswordService
{
    /// <summary>
    /// Comprueba si la contraseña aparece en la lista de comprometidas.
    /// <para><b>Fail-open</b>: si el servicio externo falla o timeout (research D-04),
    /// retorna <see cref="PwnedPasswordResult.Unavailable"/> — el caller decide si
    /// dejar pasar (caso registro/cambio) o bloquear (no recomendado).</para>
    /// </summary>
    Task<PwnedPasswordResult> IsPwnedAsync(string password, CancellationToken ct);
}

public sealed record PwnedPasswordResult(
    bool ServiceAvailable,
    bool IsPwned,
    int? OccurrenceCount = null)
{
    public static PwnedPasswordResult Available(bool isPwned, int? occurrenceCount = null) =>
        new(ServiceAvailable: true, IsPwned: isPwned, OccurrenceCount: occurrenceCount);

    public static PwnedPasswordResult Unavailable() =>
        new(ServiceAvailable: false, IsPwned: false);
}
