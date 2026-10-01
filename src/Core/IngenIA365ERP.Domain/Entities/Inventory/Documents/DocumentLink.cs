using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Un vínculo entre documentos (<c>INV_DocumentLinks</c>; feature 012, T17; data-model §5.5): el <see cref="TargetDocumentId"/>
/// (el nuevo o derivado) nace de <see cref="SourceDocumentId"/> (el original u origen) con la relación <see cref="Kind"/>.
/// No se guardan contadores de pendiente: se calculan sumando <see cref="DocumentLineLink"/>. Al descartar el borrador
/// destino, sus vínculos se dan de baja lógica.
/// </summary>
public class DocumentLink : AuditableEntity
{
    public int SourceDocumentId { get; set; }

    public InventoryDocument? SourceDocument { get; set; }

    public int TargetDocumentId { get; set; }

    public InventoryDocument? TargetDocument { get; set; }

    public DocumentLinkKind Kind { get; set; }

    public ICollection<DocumentLineLink> LineLinks { get; set; } = new List<DocumentLineLink>();
}
