namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>
/// Hash de contraseñas pasadas para impedir reuso (FR-010). Append-only:
/// no hereda <c>AuditableEntity</c> — la retención FIFO está controlada por
/// <c>PasswordPolicy.HistorySize</c>.
/// </summary>
public class PasswordHistory
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime SetAt { get; set; }

    public User? User { get; set; }
}
