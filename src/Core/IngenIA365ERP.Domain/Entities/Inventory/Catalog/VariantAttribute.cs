using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Un atributo de variante —talla, color— (<c>INV_VariantAttributes</c>; feature 012, I6, T853; FR-023, US15-1; data-model §1.11):
/// código de 10 único entre vivos (<c>CodigoDeCatalogo</c>) y sus valores en <see cref="Values"/>.
/// </summary>
public class VariantAttribute : AuditableEntity
{
    public const int LargoDelCodigo = 10;

    public const int LargoDelNombre = 60;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<VariantAttributeValue> Values { get; set; } = new List<VariantAttributeValue>();
}
