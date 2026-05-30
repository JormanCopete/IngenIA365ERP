using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_RefreshTokens].</summary>
public class RefreshToken : AuditableEntityLong
{
    public int UserId { get; set; }

    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;

    /// <summary>Hash SHA-256 hex del token opaco. Comparación directa en la BD.</summary>
    [MaxLength(120)]
    public string? TokenHash { get; set; }

    /// <summary>Familia de tokens derivada por rotaciones sucesivas. Al detectar
    /// reuso se invalida la familia entera (FR-012, refresh-token rotation).</summary>
    public Guid FamilyId { get; set; }

    /// <summary>Razón de revocación: <c>Rotated</c>, <c>LogoutUser</c>,
    /// <c>LogoutAll</c>, <c>ReuseDetected</c>, <c>AdminRevoke</c>.</summary>
    [MaxLength(40)]
    public string? RevocationReason { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    [MaxLength(100)]
    public string? RevokedBy { get; set; }

    [MaxLength(500)]
    public string? ReplacedByToken { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    // Navigation
    public User? User { get; set; }
}
