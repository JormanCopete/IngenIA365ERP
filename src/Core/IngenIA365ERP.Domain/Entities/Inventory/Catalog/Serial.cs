using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Una serie de un producto (<c>INV_Serials</c>; feature 012, I6, T854; FR-026, US15-5; data-model §1.11): única por producto
/// entre vivos. <see cref="InStockWarehouseId"/> e <see cref="InStockLocationId"/> son su <b>proyección</b> (reconstruible desde el
/// kardex; nulas si no está en existencia): una entrada con una serie que ya está en existencia se rechaza
/// (<c>Inventory.Serial.AlreadyInStock</c>). Como las demás proyecciones, sin diferencias de auditoría; la proyección la escribe sólo
/// <c>RegistroDeKardex</c>.
/// </summary>
[SinDiffDeAuditoria]
public class Serial : AuditableEntity
{
    public const int LargoDelNumero = 60;

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public string SerialNumber { get; set; } = string.Empty;

    public int? LotId { get; set; }

    public Lot? Lot { get; set; }

    /// <summary>Proyección: la bodega donde está; nula si no está en existencia.</summary>
    public int? InStockWarehouseId { get; set; }

    /// <summary>Proyección: la ubicación donde está; nula si no está en existencia.</summary>
    public int? InStockLocationId { get; set; }
}
