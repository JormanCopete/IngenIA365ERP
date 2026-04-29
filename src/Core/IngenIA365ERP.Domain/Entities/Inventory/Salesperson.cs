using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_Salespeople] (inv_Vendedor).</summary>
public class Salesperson : AuditableEntity
{
    [MaxLength(20)]
    public string IdNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(100)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(30)]
    public string? Mobile { get; set; }

    public int? CityId { get; set; }
    public int? SalespersonType { get; set; }
    public bool AppliesCommission { get; set; }
}
