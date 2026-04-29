using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_ListParameters] (sys_parlistas).
/// </summary>
public class ListParameter : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(120)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(4)]
    public string? ListType { get; set; }

    public bool ValidateExpiryDate { get; set; }
}
