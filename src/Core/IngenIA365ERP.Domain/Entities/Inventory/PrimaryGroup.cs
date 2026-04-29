using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_PrimaryGroups] (inv_Grupo_Primario).</summary>
public class PrimaryGroup : AuditableEntity
{
    public int GroupCode { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ShortName { get; set; }

    // Navigation
    public ICollection<SecondaryGroup> SecondaryGroups { get; set; } = [];
}
