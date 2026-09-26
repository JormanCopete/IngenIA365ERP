using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Un vínculo entre líneas (<c>INV_DocumentLineLinks</c>; feature 012, T17; data-model §5.5): cuánto de la línea origen
/// (<see cref="SourceLineId"/>) consume la línea destino (<see cref="TargetLineId"/>), en unidad base y siempre &gt; 0.
/// El <c>Kind</c> lo da su <see cref="DocumentLink"/>. Lo pendiente de una línea origen es su <c>QuantityBase</c> menos
/// la suma de los vínculos cuyo destino está en aprobación o confirmado y no anulado.
/// </summary>
public class DocumentLineLink : AuditableEntity
{
    public int DocumentLinkId { get; set; }

    public DocumentLink? DocumentLink { get; set; }

    public int SourceLineId { get; set; }

    public InventoryDocumentLine? SourceLine { get; set; }

    public int TargetLineId { get; set; }

    public InventoryDocumentLine? TargetLine { get; set; }

    public decimal QuantityBase { get; set; }
}
