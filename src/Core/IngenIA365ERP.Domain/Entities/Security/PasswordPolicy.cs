using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>
/// Política de contraseñas por tenant (FR-008/009/010/011). Una política activa
/// por tenant — el invariante lo aplica EF Core con índice único filtrado.
/// </summary>
public class PasswordPolicy : AuditableEntity
{
    public int? TenantId { get; set; }

    public int MinLength { get; set; } = 12;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSymbol { get; set; } = true;
    public int ExpiryDays { get; set; } = 90;
    public int HistorySize { get; set; } = 5;
    public int LockoutThreshold { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;

    public Tenant? Tenant { get; set; }
}
