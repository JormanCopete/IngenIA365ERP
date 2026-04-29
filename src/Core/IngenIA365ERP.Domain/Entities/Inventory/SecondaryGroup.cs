using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_SecondaryGroups] (inv_GrupoSecundario).</summary>
public class SecondaryGroup : AuditableEntity
{
    public int GroupCode { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ShortName { get; set; }

    public int? PrimaryGroupId { get; set; }

    // Navigation
    public PrimaryGroup? PrimaryGroup { get; set; }
}
