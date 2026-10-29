using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Payments;

/// <summary>
/// <c>TES_ManualOrderConcepts</c> (feature 015, T1, fase 34, T0416; data-model §13.1; FR-211): el catálogo cerrado de las órdenes de pago manuales de Tesorería. Una orden
/// manual sólo se prepara por uno de estos conceptos, con su tope (<see cref="MaxAmount"/>) y, si <see cref="RequiresWithholding"/>, con la retención en la fuente que
/// corresponda; sin ella, se deriva a una causación previa en Contabilidad. <see cref="Code"/> es la nomenclatura de la cooperativa (<c>CodigoDeCatalogo</c>) y viaja
/// como <c>OrderKind</c> de la orden, el que resuelve el puente en la matriz. Siempre exige dos aprobadores y soporte. Nunca se borra: se inactiva. (nuevo)
/// </summary>
public class ManualOrderConcept : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>El tope del valor bruto de una orden de este concepto.</summary>
    public decimal MaxAmount { get; set; }

    /// <summary>La orden exige la retención en la fuente que corresponda; sin ella, se deriva a una causación previa en Contabilidad.</summary>
    public bool RequiresWithholding { get; set; }

    /// <summary>Qué retención corresponde (texto para quien prepara: el concepto de retención y su base).</summary>
    public string? WithholdingNote { get; set; }

    public TreasuryCatalogStatus Status { get; set; } = TreasuryCatalogStatus.Active;
}
