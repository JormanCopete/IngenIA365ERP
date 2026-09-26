using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Canal de venta (<c>INV_SalesChannels</c>; feature 012, T199; data-model §1.5). Lo referencian el tipo de documento, el
/// documento y, desde I3, el punto de venta y las listas de precios.
/// </summary>
public class SalesChannel : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
