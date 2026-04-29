using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_UserAssignments] (sys_ComAsigna).</summary>
public class UserAssignment : AuditableEntity
{
    [MaxLength(10)]
    public string? VoucherTypeCode { get; set; }

    public int? UserId { get; set; }

    // Navigation
    public User? User { get; set; }
}
