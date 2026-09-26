using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// La foto de un impuesto o retención que aplicó el motor tributario al confirmar (<c>INV_DocumentTaxLines</c>; feature
/// 012, T22; FR-013, FR-044; data-model §5.7). Hecho inmutable: el borrador los calcula al vuelo y guarda sólo los
/// totales; al confirmar se escriben estas filas y ya no cambian. Notas y devoluciones reutilizan la foto del
/// original. <see cref="DocumentLineId"/> nulo = renglón del documento (retenciones). Las FK al catálogo tributario
/// (<c>COR_TaxDefinitions</c>, <c>COR_TaxRates</c>, <c>COR_WithholdingConcepts</c>) las declara su configuración.
/// </summary>
public class DocumentTaxLine : AuditableEntity, IHechoInmutable
{
    public int DocumentId { get; init; }

    public int? DocumentLineId { get; init; }

    public int TaxDefinitionId { get; init; }

    /// <summary>La <b>fila</b> (vigencia) de la tarifa que aplicó.</summary>
    public int TaxRateId { get; init; }

    public string TaxRateCode { get; init; } = string.Empty;

    public TaxKind Kind { get; init; }

    public TaxTreatment Treatment { get; init; }

    public int? WithholdingConceptId { get; init; }

    public string? MunicipalityDaneCode { get; init; }

    /// <summary>Fracción (0,19).</summary>
    public decimal? Rate { get; init; }

    public decimal? AmountPerUnit { get; init; }

    public decimal? TaxableUnits { get; init; }

    public decimal Base { get; init; }

    /// <summary>Redondeado por línea; el total es la suma.</summary>
    public decimal Amount { get; init; }

    public string? DianTaxCode { get; init; }

    /// <summary>Pasos del motor: UVT y fecha, base mínima en pesos, condición que decidió.</summary>
    public string ExplanationJson { get; init; } = "[]";
}
