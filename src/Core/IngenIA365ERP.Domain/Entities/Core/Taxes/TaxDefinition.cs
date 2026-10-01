using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Core.Taxes;

/// <summary>
/// <c>COR_TaxDefinitions</c> (feature 012, T22, T161; data-model §17): un impuesto o una retención. El código, la clase
/// y la forma de cálculo no cambian después de creada; sus tarifas con vigencia viven en <see cref="Rates"/>. ReteIVA
/// es <see cref="TaxCalculationForm.PercentOfTax"/> sobre el IVA (<see cref="TaxedOnDefinitionId"/>). <c>Ica</c> es
/// informativo (declaración por municipio): el motor no lo liquida.
/// </summary>
public class TaxDefinition : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TaxKind Kind { get; set; }

    public TaxCalculationForm CalculationForm { get; set; }

    /// <summary>Obligatorio si <see cref="TaxCalculationForm.PercentOfTax"/>: el impuesto sobre el que se calcula.</summary>
    public int? TaxedOnDefinitionId { get; set; }

    public TaxDefinition? TaxedOnDefinition { get; set; }

    /// <summary>Verdadero en <c>ReteFuente</c>, <c>ReteIva</c> y <c>ReteIca</c>; en <c>Other</c>, lo dice quien lo crea.</summary>
    public bool IsWithholding { get; set; }

    /// <summary>Tributo DIAN (01 IVA, 04 INC, 22 bolsas, 05 ReteIVA, 06 ReteFuente, 07 ReteICA, ZZ…).</summary>
    public string? DianTaxCode { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public ICollection<TaxRate> Rates { get; set; } = [];
}
