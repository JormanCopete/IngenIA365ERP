using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_UserSessions].</summary>
public class UserSession : AuditableEntityLong
{
    public int UserId { get; set; }

    [MaxLength(500)]
    public string SessionToken { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public User? User { get; set; }
}
