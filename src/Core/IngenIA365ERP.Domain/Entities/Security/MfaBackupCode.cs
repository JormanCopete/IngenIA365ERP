using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>
/// Código de respaldo MFA hashed con BCrypt cost 11. Uso único:
/// <see cref="UsedAt"/> es immutable una vez fijado. Una regeneración
/// crea un nuevo <see cref="BatchId"/> e invalida en bloque los anteriores.
/// </summary>
public class MfaBackupCode : AuditableEntity
{
    public int UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Guid BatchId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// Marca el código como usado. Idempotente: una vez fijado, no cambia.
    /// </summary>
    public void MarkUsed(DateTime now)
    {
        if (UsedAt is null)
        {
            UsedAt = now;
        }
    }
}
