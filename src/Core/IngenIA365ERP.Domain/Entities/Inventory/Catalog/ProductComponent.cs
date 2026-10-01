using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Un componente de un combo o de un kit (<c>INV_ProductComponents</c>; feature 012, I6, T853; FR-023, US15; data-model §1.11):
/// <see cref="Quantity"/> en la unidad base del componente, mayor que cero. Único <c>(ProductId, ComponentProductId)</c> entre vivos.
/// El componente es inventariable o variante (ni combo, ni kit, ni plantilla, ni servicio) y no hay ciclos: lo decide el comando
/// de US15, no la tabla. El combo vendido saca sus componentes a su costo; el kit entra por el documento <c>Assembly</c>.
/// </summary>
public class ProductComponent : AuditableEntity
{
    /// <summary>El combo o el kit.</summary>
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int ComponentProductId { get; set; }

    public Product? ComponentProduct { get; set; }

    /// <summary>En la unidad base del componente (<c>Cantidad</c>, 18,4).</summary>
    public decimal Quantity { get; set; }
}
