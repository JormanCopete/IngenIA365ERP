using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Aplica la política de contraseñas del tenant (FR-007/008/009/010): longitud
/// mínima, complejidad, no reuso (BCrypt), expiración. Los códigos retornados
/// usan el namespace <c>Auth.PasswordPolicyViolation</c>.
/// </summary>
public interface IPasswordPolicyEnforcer
{
    /// <summary>Valida complejidad y longitud según política activa.</summary>
    Task<Result> ValidateAsync(string newPassword, int? tenantId, CancellationToken ct);

    /// <summary>
    /// Verifica que la contraseña en claro no aparezca en el historial
    /// (FIFO de <c>PasswordPolicy.HistorySize</c>).
    /// </summary>
    Task<Result> EnsureNotReusedAsync(int userId, string newPassword, int? tenantId, CancellationToken ct);

    /// <summary>Devuelve true si la última contraseña ya expiró por edad.</summary>
    Task<bool> IsExpiredAsync(int userId, int? tenantId, DateTime? lastChangedAt, CancellationToken ct);

    /// <summary>Hashea la contraseña con BCrypt cost ≥ 11.</summary>
    string Hash(string plainPassword);

    /// <summary>Verifica un plain text contra un hash BCrypt.</summary>
    bool Verify(string plainPassword, string hash);

    /// <summary>
    /// Recorta el historial de contraseñas del usuario al límite definido por
    /// la política (HistorySize). Llamado tras añadir una nueva entrada.
    /// </summary>
    Task TrimHistoryAsync(int userId, int? tenantId, CancellationToken ct);
}
