using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Unidad alterna de un producto (<c>INV_ProductUnits</c>; feature 012, T201; FR-017, FR-025; data-model §1.7): distinta
/// de la base (que tiene factor 1 implícito), con <see cref="Factor"/> unidades base por una de ésta (caja × 12 → 12). La
/// línea del documento copia el factor al guardarse; por eso, con movimientos, el factor ya no cambia.
/// </summary>
public class ProductUnit : AuditableEntity
{
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int UnitId { get; set; }

    public UnitOfMeasure? Unit { get; set; }

    public decimal Factor { get; set; } = 1m;

    public bool UsedForPurchase { get; set; }

    public bool UsedForSale { get; set; }

    /// <summary>La que proponen las compras (a lo sumo una por producto).</summary>
    public bool IsDefaultPurchase { get; set; }

    /// <summary>La que propone el POS (a lo sumo una por producto).</summary>
    public bool IsDefaultSale { get; set; }

    /// <summary>Las dos marcas como el enum del contrato.</summary>
    public ProductUnitUsage Usage => (UsedForPurchase, UsedForSale) switch
    {
        (true, true) => ProductUnitUsage.Both,
        (true, false) => ProductUnitUsage.Purchase,
        _ => ProductUnitUsage.Sale,
    };

    /// <summary>Fija el uso; si deja de servir para compra o venta, deja de ser la de por defecto de ese lado.</summary>
    public void FijarUso(ProductUnitUsage uso)
    {
        UsedForPurchase = uso is ProductUnitUsage.Purchase or ProductUnitUsage.Both;
        UsedForSale = uso is ProductUnitUsage.Sale or ProductUnitUsage.Both;
        if (!UsedForPurchase) IsDefaultPurchase = false;
        if (!UsedForSale) IsDefaultSale = false;
    }
}
