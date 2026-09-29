using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// El valor que una variante toma en un atributo (<c>INV_ProductVariantValues</c>; feature 012, I6, T853; US15-1; data-model §1.11).
/// <see cref="VariantAttributeId"/> va desnormalizado para el único <c>(ProductId, VariantAttributeId)</c> entre vivos: una variante
/// tiene un solo valor por atributo. Todas las variantes de una plantilla llevan valores para los mismos atributos, y
/// <c>INV_Products.VariantKey</c> impide repetir la combinación.
/// </summary>
public class ProductVariantValue : AuditableEntity
{
    /// <summary>La variante.</summary>
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Desnormalizado del valor, para el índice.</summary>
    public int VariantAttributeId { get; set; }

    public VariantAttribute? VariantAttribute { get; set; }

    public int VariantAttributeValueId { get; set; }

    public VariantAttributeValue? VariantAttributeValue { get; set; }
}
