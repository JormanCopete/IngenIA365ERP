using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// El uso de un bono de número único (<c>INV_VoucherRedemptions</c>; feature 012, I3, T577; FR-097, SC-002; data-model §16). Se
/// inserta en la transacción que confirma la venta; <c>UK_INV_VoucherRedemptions_Means_Number_Active</c> (filtrado a activos
/// vivos) garantiza la unicidad entre cajas y su colisión se traduce a <c>Payments.VoucherAlreadyUsed</c> con la venta que lo usó.
/// Liberar (la anulación o la nota que reintegra el bono) es un cambio de estado, nunca un borrado: el número vuelve a servir
/// (F11). Sólo para medios con <c>UniqueReference</c>.
/// </summary>
public class VoucherRedemption : AuditableEntity
{
    public int PaymentMeansId { get; set; }

    /// <summary>Mayúsculas, sin espacios ni guiones (<c>ValidadorDePagos.NormalizarReferencia</c>).</summary>
    public string NormalizedNumber { get; set; } = string.Empty;

    public int DocumentPaymentId { get; set; }

    public DocumentPayment? DocumentPayment { get; set; }

    /// <summary>La venta que lo usó.</summary>
    public int DocumentId { get; set; }

    public VoucherRedemptionStatus Status { get; private set; } = VoucherRedemptionStatus.Active;

    public DateTime RedeemedAt { get; set; }

    public int? ReleasedByDocumentId { get; private set; }

    public DateTime? ReleasedAt { get; private set; }

    public string? ReleaseReason { get; private set; }

    /// <summary>Libera el bono: la anulación o la nota con reintegro lo deja disponible otra vez.</summary>
    public void Liberar(int liberadoPorDocumentoId, DateTime ahoraUtc, string? motivo)
    {
        if (Status != VoucherRedemptionStatus.Active)
            throw new InvalidOperationException($"El bono {NormalizedNumber} ya está liberado.");
        Status = VoucherRedemptionStatus.Released;
        ReleasedByDocumentId = liberadoPorDocumentoId;
        ReleasedAt = ahoraUtc;
        ReleaseReason = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
    }
}
