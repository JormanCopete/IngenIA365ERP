using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Marca (<c>INV_Brands</c>; feature 012, T199; FR-024; data-model §1.3). Entra a la búsqueda del producto: renombrarla
/// recalcula el <c>SearchText</c> de sus productos en el mismo guardado.
/// </summary>
public class Brand : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
