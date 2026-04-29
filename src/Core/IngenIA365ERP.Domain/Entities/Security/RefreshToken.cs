using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_RefreshTokens].</summary>
public class RefreshToken : AuditableEntityLong
{
    public int UserId { get; set; }

    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;

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
