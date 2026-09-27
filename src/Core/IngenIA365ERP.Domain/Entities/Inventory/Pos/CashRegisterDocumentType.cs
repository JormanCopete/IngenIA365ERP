using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// El tipo de documento que una caja usa en un rol (<c>INV_CashRegisterDocumentTypes</c>; feature 012, I3, T574; FR-058,
/// FR-067; data-model §15). Único <c>(CashRegisterId, Role)</c> entre vivos. Cambiar el tipo de un rol da de baja la fila y crea
/// otra; los documentos ya emitidos guardan su tipo. La compatibilidad rol ↔ clase la decide <see cref="ClasesDelRol"/>.
/// </summary>
public class CashRegisterDocumentType : AuditableEntity
{
    public int CashRegisterId { get; set; }

    public CashRegister? CashRegister { get; set; }

    public CashRegisterDocumentRole Role { get; set; }

    public int DocumentTypeId { get; set; }

    public InventoryDocumentType? DocumentType { get; set; }

    /// <summary>
    /// Las clases de tipo que admite cada rol (<c>Inventory.CashRegister.RoleClassMismatch</c>). Las contingencias admiten el
    /// tipo fiscal de la venta que respaldan; que su prefijo tenga la resolución de contingencia lo verifica el comando.
    /// </summary>
    public static IReadOnlySet<DocumentClass> ClasesDelRol(CashRegisterDocumentRole rol) => rol switch
    {
        CashRegisterDocumentRole.PosSale => new HashSet<DocumentClass> { DocumentClass.PosEquivalentDocument, DocumentClass.NonElectronicSalesReceipt },
        CashRegisterDocumentRole.InvoiceOnRequest => new HashSet<DocumentClass> { DocumentClass.SalesInvoice },
        CashRegisterDocumentRole.PosAdjustmentNote => new HashSet<DocumentClass> { DocumentClass.PosAdjustmentNote, DocumentClass.NonElectronicSalesNote },
        CashRegisterDocumentRole.InvoiceCreditNote => new HashSet<DocumentClass> { DocumentClass.CreditNote },
        CashRegisterDocumentRole.PosSaleContingency => new HashSet<DocumentClass> { DocumentClass.PosEquivalentDocument },
        CashRegisterDocumentRole.InvoiceContingency => new HashSet<DocumentClass> { DocumentClass.SalesInvoice },
        _ => new HashSet<DocumentClass>(),
    };
}
