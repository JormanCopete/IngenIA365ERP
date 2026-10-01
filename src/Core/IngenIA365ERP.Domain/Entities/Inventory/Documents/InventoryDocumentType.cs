using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Un tipo de documento (<c>INV_DocumentTypes</c>; feature 012, FR-037; data-model §5.8): elige su clase (fija e
/// inmutable) y parametriza campos obligatorios, bodegas permitidas y canal. El consecutivo vive en
/// <see cref="DocumentSequence"/> (tipos no fiscales y notas) o en la resolución DIAN (fiscales, por
/// <see cref="FiscalPrefix"/>, I4). La política de aprobación y el modo de paso son parámetros, no columnas.
/// <see cref="Code"/> no cambia una vez creado: la matriz contable puede mapear por él (T27, T28).
/// </summary>
public class InventoryDocumentType : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DocumentClass Class { get; set; }

    /// <summary>Sólo en tipos fiscales con resolución: mayúsculas y dígitos, ≤ 4.</summary>
    public string? FiscalPrefix { get; set; }

    /// <summary>Tipo de contingencia del facturador (resolución <c>Contingency</c>).</summary>
    public bool IsContingency { get; set; }

    public bool RequiresCounterparty { get; set; }

    public bool RequiresCostCenter { get; set; }

    public bool RequiresReason { get; set; }

    public bool RequiresExternalReference { get; set; }

    public int? SalesChannelId { get; set; }

    /// <summary>Sólo <c>InternalConsumption</c>: retiro gravado.</summary>
    public bool IsTaxableWithdrawal { get; set; }

    /// <summary>Sólo clases de compra: el IVA va al costo (FR-044).</summary>
    public bool VatNonDeductible { get; set; }

    public bool AllowsFutureDate { get; set; }

    /// <summary>1 = cualquier bodega que admita la clase; 0 = sólo las de <see cref="Warehouses"/>.</summary>
    public bool AllWarehouses { get; set; } = true;

    public bool IsSeeded { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<DocumentTypeWarehouse> Warehouses { get; set; } = new List<DocumentTypeWarehouse>();

    public ICollection<DocumentSequence> Sequences { get; set; } = new List<DocumentSequence>();
}
