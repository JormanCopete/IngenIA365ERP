using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_LoginAttempts].</summary>
public class LoginAttempt : AuditableEntityLong
{
    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public DateTime AttemptedAt { get; set; }
    public bool WasSuccessful { get; set; }

    [MaxLength(200)]
    public string? FailureReason { get; set; }
}
