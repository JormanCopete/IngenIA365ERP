using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Un lote de un producto (<c>INV_Lots</c>; feature 012, I6, T854; FR-026, US15; data-model §1.11): <see cref="Code"/> es el número
/// del proveedor o del fabricante, único por producto entre vivos. <see cref="ExpiryDate"/> es obligatoria si el producto controla
/// vencimiento (lo decide el comando). Nace con la primera entrada que lo cita; las salidas proponen el que vence primero.
/// </summary>
public class Lot : AuditableEntity
{
    public const int LargoDelCodigo = 40;

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public string Code { get; set; } = string.Empty;

    public DateOnly? ExpiryDate { get; set; }

    public DateOnly? ManufactureDate { get; set; }
}
