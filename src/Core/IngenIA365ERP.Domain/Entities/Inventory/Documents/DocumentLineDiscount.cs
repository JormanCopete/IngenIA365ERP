using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Un descuento de una línea de venta (<c>INV_DocumentLineDiscounts</c>; feature 012, I3, T577; FR-054, FR-017, T51; data-model
/// §14). El descuento por total se prorratea (una fila por línea con <see cref="FromDocumentDiscount"/>) para que baje la base
/// gravable línea a línea; cambiar el precio de lista a mano es un descuento <see cref="IsPriceOverride"/>. Sobre el tope del
/// usuario no se rechaza: queda <see cref="RequiresApproval"/> con su solicitud <c>DiscountOverCap</c>, que vale mientras la
/// línea no cambie (la huella <c>ContentSha256</c>, T33). Único <c>(DocumentLineId, Sequence)</c> entre vivos. Con una promoción
/// en la línea no entra un manual (F9); <see cref="PromotionId"/> llega en I6 (<c>ComercioAmpliado</c>, T856). En un documento
/// confirmado, inmutable.
/// </summary>
public class DocumentLineDiscount : AuditableEntity
{
    public int DocumentLineId { get; set; }

    public InventoryDocumentLine? DocumentLine { get; set; }

    /// <summary>Desnormalizado para la vista <c>discount-approvals</c>.</summary>
    public int DocumentId { get; set; }

    /// <summary>Orden de aplicación en la línea.</summary>
    public byte Sequence { get; set; }

    public DiscountSource Source { get; set; } = DiscountSource.Manual;

    public bool FromDocumentDiscount { get; set; }

    public bool IsPriceOverride { get; set; }

    /// <summary>Fracción, cuando se expresó en porcentaje.</summary>
    public decimal? Rate { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Copia del tope del usuario al aplicarlo (el mayor de sus roles; sin fila, 0).</summary>
    public decimal CapRateApplied { get; set; }

    public bool RequiresApproval { get; set; }

    /// <summary><c>COR_ApprovalRequests</c> con sujeto <c>DiscountOverCap</c>.</summary>
    public int? ApprovalRequestId { get; set; }

    /// <summary>Copia del aprobador (informe).</summary>
    public int? ApprovedByUserId { get; set; }

    public ApprovalMethod? ApprovalMethod { get; set; }

    /// <summary>Obligatorio si <see cref="RequiresApproval"/>.</summary>
    public string? Reason { get; set; }

    /// <summary>I6: la promoción que lo produjo; obligatoria si <see cref="Source"/> es <c>Promotion</c>.</summary>
    public int? PromotionId { get; set; }

    public Pricing.Promotion? Promotion { get; set; }
}
