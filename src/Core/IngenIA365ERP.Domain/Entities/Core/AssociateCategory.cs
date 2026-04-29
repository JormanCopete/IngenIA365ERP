using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_AssociateCategories] — one-to-one with Person for category rating data.
/// Legacy: extracted from sys_maenit CATEGORIA* fields.
/// </summary>
public class AssociateCategory : AuditableEntity
{
    public int PersonId { get; set; }

    [MaxLength(2)]
    public string? Category1 { get; set; }

    [MaxLength(2)]
    public string? Category2 { get; set; }

    [MaxLength(2)]
    public string? Category3 { get; set; }

    [MaxLength(2)]
    public string? Category4 { get; set; }

    [MaxLength(2)]
    public string? Category5 { get; set; }

    public short DaysCategory { get; set; }

    // Navigation
    public Person Person { get; set; } = null!;
}
