using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core.Taxes;

/// <summary>
/// <c>COR_WithholdingConcepts</c> (feature 012, T22, T161; data-model §17): concepto de retención (compras, servicios,
/// honorarios, arrendamientos, transporte, otros). Lo cita la tarifa de ReteFuente y el producto (su concepto en
/// compras). <see cref="Code"/> es la nomenclatura de la cooperativa (<c>CodigoDeCatalogo</c>), único entre vivos.
/// </summary>
public class WithholdingConcept : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}
