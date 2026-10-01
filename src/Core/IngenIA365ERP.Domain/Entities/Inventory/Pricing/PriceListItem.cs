using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pricing;

/// <summary>
/// Un precio de una lista (<c>INV_PriceListItems</c>; feature 012, I3, T576; FR-053; data-model §14). Único
/// <c>(PriceListId, ProductId, UnitId)</c> entre vivos; la unidad es la base del producto o una alterna de venta, y una plantilla
/// no tiene precio. El precio va en pesos, con o sin impuestos según la lista. Cambiarlo queda en el diff de auditoría.
/// </summary>
public class PriceListItem : AuditableEntity
{
    public int PriceListId { get; set; }

    public PriceList? PriceList { get; set; }

    /// <summary><c>INV_Products</c>.</summary>
    public int ProductId { get; set; }

    /// <summary><c>INV_UnitsOfMeasure</c>.</summary>
    public int UnitId { get; set; }

    public decimal Price { get; set; }
}
