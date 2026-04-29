using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_UserMenuAccess] (sys_menusu).</summary>
public class UserMenuAccess : AuditableEntity
{
    public int UserId { get; set; }
    public int? MenuType { get; set; }

    [MaxLength(2)]
    public string? ProgramType { get; set; }

    [MaxLength(30)]
    public string? ProgramCode { get; set; }

    [MaxLength(100)]
    public string? ProgramName { get; set; }

    public bool HasAccess { get; set; } = true;

    [MaxLength(30)]
    public string? MenuCode { get; set; }

    [MaxLength(30)]
    public string? SubMenuCode { get; set; }

    // Navigation
    public User? User { get; set; }
}
