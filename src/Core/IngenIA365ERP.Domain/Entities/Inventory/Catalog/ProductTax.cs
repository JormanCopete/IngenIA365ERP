using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Impuesto de un producto (<c>INV_ProductTaxes</c>; feature 012, T201; FR-013, FR-027; data-model §1.9). La tarifa va por
/// su <b>código estable</b> (<see cref="TaxRateCode"/>), no por la fila: cada vigencia de <c>COR_TaxRates</c> es una fila
/// nueva y el motor toma la vigente a la fecha del documento. Nulo = el motor la elige por condiciones. Las retenciones no
/// se vinculan aquí: salen del concepto de retención del producto.
/// </summary>
public class ProductTax : AuditableEntity
{
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int TaxDefinitionId { get; set; }

    public TaxDefinition? TaxDefinition { get; set; }

    public string? TaxRateCode { get; set; }

    public TaxAppliesTo AppliesTo { get; set; } = TaxAppliesTo.Both;

    /// <summary>Sólo en impuestos por unidad: bolsa = 1; bebida de 1,5 L con IBUA por 100 ml = 15.</summary>
    public decimal? TaxableUnitsPerBaseUnit { get; set; }
}
