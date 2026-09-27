using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core.Payments;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// Un tipo de documento que admite el medio (<c>INV_PaymentMeansDocumentTypes</c>; feature 012, I3, T574; T25; data-model §15 «Disponibilidad de medios»). Uno de los tres conjuntos de
/// disponibilidad de un medio de pago, que viven en el módulo para que Core no apunte a Inventario; el par es único entre vivos.
/// Un conjunto vacío <b>no</b> es «todos»: eso es la marca del medio, y un conjunto explícito junto con la marca es
/// <c>Inventory.PaymentMeans.AvailabilityConflict</c>. La regla es <c>DisponibilidadDeMedio</c>.
/// </summary>
public class PaymentMeansDocumentType : AuditableEntity
{
    public int PaymentMeansId { get; set; }

    public PaymentMeans? PaymentMeans { get; set; }

    /// <summary><c>INV_DocumentTypes</c>.</summary>
    public int DocumentTypeId { get; set; }
}
