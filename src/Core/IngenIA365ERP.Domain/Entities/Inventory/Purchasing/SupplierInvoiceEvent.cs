using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Purchasing;

/// <summary>
/// El estado de un evento RADIAN de una factura del proveedor (<c>INV_SupplierInvoiceEvents</c>; feature 012, T336;
/// data-model §9.3; FR-050, US9-4, T42): el acuse de recibo (030) y el recibo del bien (032). Nacen los dos al confirmar la
/// factura, <c>Pending</c> si es a crédito y <c>NotApplicable</c> si es de contado. En I1 la cooperativa los emite por fuera
/// (portal de la DIAN o del proveedor) y el ERP anota quién dijo haberlo hecho, cuándo y con qué fuente; desde I5 los emite
/// el ERP (<see cref="ElectronicDocumentPublicId"/>). Las transiciones las decide <c>TransicionesDeEventoRadian</c>.
/// </summary>
public class SupplierInvoiceEvent : AuditableEntity
{
    public const int LargoDeLaFuente = 20;
    public const int LargoDelCude = 96;
    public const int LargoDeLasNotas = 300;

    /// <summary>Emitido en el portal de la DIAN.</summary>
    public const string FuenteDian = "DianPortal";

    /// <summary>Emitido en el portal del proveedor.</summary>
    public const string FuenteProveedor = "SupplierPortal";

    /// <summary>Emitido por el ERP (I5).</summary>
    public const string FuenteErp = "Erp";

    /// <summary>La lista cerrada de fuentes.</summary>
    public static readonly IReadOnlyList<string> Fuentes = [FuenteDian, FuenteProveedor, FuenteErp];

    /// <summary>La factura del proveedor.</summary>
    public int DocumentId { get; set; }

    public SupplierInvoiceEventCode EventCode { get; set; }

    public SupplierInvoiceEventStatus Status { get; set; }

    public DateOnly? EventDate { get; set; }

    /// <summary><see cref="FuenteDian"/>, <see cref="FuenteProveedor"/> o <see cref="FuenteErp"/>.</summary>
    public string? Source { get; set; }

    public string? Cude { get; set; }

    public int? RegisteredByUserId { get; set; }

    public DateTime? RegisteredAt { get; set; }

    /// <summary>Constancia opcional (adjunto).</summary>
    public Guid? EvidenceAttachmentPublicId { get; set; }

    /// <summary>I5: el <c>COR_ElectronicDocuments</c> de <c>Kind = RadianEvent030/032</c>.</summary>
    public Guid? ElectronicDocumentPublicId { get; set; }

    public string? Notes { get; set; }
}
