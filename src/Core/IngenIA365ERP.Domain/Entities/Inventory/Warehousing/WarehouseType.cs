using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Warehousing;

/// <summary>
/// Tipo de bodega (<c>INV_WarehouseTypes</c>; feature 012, T202; FR-032; data-model §2.1): parametrizable, con un
/// comportamiento fijo del sistema (<see cref="Behavior"/>), que no cambia. <see cref="WarehouseBehavior.Transit"/> sólo lo
/// tiene el tipo sembrado: un tipo parametrizado nunca convierte una bodega de ventas en tránsito.
/// </summary>
public class WarehouseType : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public WarehouseBehavior Behavior { get; set; } = WarehouseBehavior.Operational;

    public bool IsSeeded { get; set; }

    public bool IsActive { get; set; } = true;
}
