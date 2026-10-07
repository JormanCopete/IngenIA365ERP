using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// El recibo de Comercial de <c>ObligacionCreada</c> y <c>ObligacionRechazada</c> (<c>INV_LendingObligationResponses</c>; feature 013,
/// T0174, T18, DE-14; data-model parte 3 §8.3). Hecho inmutable: lo escribe sólo <c>DestinoInventario</c> (consumidor del destino
/// <c>Inventory</c>, US6) en el mismo guardado del recibo, y la colisión del único <c>MessagePublicId</c> es <c>AlreadyProcessed</c>.
/// Guarda número y estado de la obligación, <b>nunca</b> saldos ni cuotas (enmienda de FR-062 de la 012). La pestaña «Crédito» de la
/// venta toma la última respuesta del pago. (nuevo)
/// </summary>
public class LendingObligationResponse : AuditableEntity, IHechoInmutable
{
    /// <summary>El pago de crédito de la venta (la venta es la raíz de la cadena).</summary>
    public int DocumentPaymentId { get; init; }

    public DocumentPayment? DocumentPayment { get; init; }

    /// <summary>El mensaje recibido: único sin filtro (recibo).</summary>
    public Guid MessagePublicId { get; init; }

    /// <summary><c>ObligacionCreada</c> u <c>ObligacionRechazada</c>.</summary>
    public string MessageType { get; init; } = string.Empty;

    public short MessageVersion { get; init; }

    /// <summary>La <c>VentaACreditoRegistrada</c> (o el ajuste) que responde.</summary>
    public Guid OriginalMessagePublicId { get; init; }

    public LendingObligationOutcome Outcome { get; init; }

    /// <summary>En <see cref="LendingObligationOutcome.Created"/>.</summary>
    public Guid? ObligationPublicId { get; init; }

    /// <summary>En <see cref="LendingObligationOutcome.Created"/>; Comercial lo muestra en la venta y en sus consultas.</summary>
    public string? ObligationNumber { get; init; }

    public LendingObligationState? ValidationStatus { get; init; }

    /// <summary>En <see cref="LendingObligationOutcome.Rejected"/>: el código del motivo (p. ej. <c>SinObligacion</c>).</summary>
    public string? ReasonCode { get; init; }

    public string? Reason { get; init; }

    /// <summary>Si el rechazo queda para reproceso.</summary>
    public bool Retryable { get; init; }

    public DateTime ReceivedAt { get; init; }
}
