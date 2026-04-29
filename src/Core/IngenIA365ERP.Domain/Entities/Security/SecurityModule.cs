using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_Modules] (sys_programa).</summary>
public class SecurityModule : AuditableEntity
{
    public int? UserId { get; set; }

    [MaxLength(30)]
    public string? ProgramCode { get; set; }

    [MaxLength(100)]
    public string? Description { get; set; }

    [MaxLength(2)]
    public string? ProgramType { get; set; }

    // Navigation
    public User? User { get; set; }
}
