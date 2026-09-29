using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Un valor de un atributo de variante —AZUL, M— (<c>INV_VariantAttributeValues</c>; feature 012, I6, T853; data-model §1.11):
/// único <c>(VariantAttributeId, Code)</c> entre vivos; <see cref="SortOrder"/> ordena la matriz de variantes.
/// </summary>
public class VariantAttributeValue : AuditableEntity
{
    public int VariantAttributeId { get; set; }

    public VariantAttribute? VariantAttribute { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
