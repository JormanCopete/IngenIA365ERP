using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Sequences] (sys_consecu).
/// </summary>
public class Sequence : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? ShortName { get; set; }

    [MaxLength(10)]
    public string? DocumentType { get; set; }

    public bool IsAutomatic { get; set; }

    public long NextSequence { get; set; } = 1;
}
