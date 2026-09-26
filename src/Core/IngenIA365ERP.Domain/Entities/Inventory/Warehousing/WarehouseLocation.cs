using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Warehousing;

/// <summary>
/// Ubicación interna de una bodega (<c>INV_WarehouseLocations</c>; feature 012, T202; FR-032, FR-039; data-model §2.3). Toda
/// bodega nace con la suya por defecto (<see cref="CodigoPorDefecto"/>), que es la que toma una línea sin ubicación: por eso
/// el kardex lleva ubicación obligatoria. La de por defecto no se elimina ni se inactiva; ninguna se inactiva con existencia.
/// </summary>
public class WarehouseLocation : AuditableEntity
{
    public const string CodigoPorDefecto = "GENERAL";

    public const string NombrePorDefecto = "General";

    public int WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}
