using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_LegalAdvisors] (sys_asejuri).
/// </summary>
public class LegalAdvisor : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? ShortName { get; set; }

    [MaxLength(120)]
    public string? Address { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(30)]
    public string? TaxId { get; set; }
}
