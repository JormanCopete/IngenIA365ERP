using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Una bodega permitida de un tipo de documento cuando el tipo no admite todas (<c>INV_DocumentTypeWarehouses</c>;
/// feature 012, FR-037; data-model §5.8). Una bodega de tránsito nunca es bodega permitida de un tipo. La FK a
/// <c>INV_Warehouses</c> la declara la configuración de la bodega (US1).
/// </summary>
public class DocumentTypeWarehouse : AuditableEntity
{
    public int DocumentTypeId { get; set; }

    public InventoryDocumentType? DocumentType { get; set; }

    public int WarehouseId { get; set; }
}
