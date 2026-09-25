using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.Integration;

/// <summary>
/// La entrega de un mensaje a un destino (<c>COR_IntegrationMessageDeliveries</c>; feature 012, T7, T9, T10;
/// data-model §19; contracts/mensajes.md §11). A diferencia del mensaje, es <b>mutable</b> con
/// <c>RowVersion</c>: si dos réplicas registran el resultado de la misma entrega, una pierde y relee.
///
/// <para>
/// Nace con el mensaje, en la misma transacción, con el estado inicial que fija el modo sellado:
/// <c>Online</c> → <c>Pending</c>; <c>Batch</c> → <c>InBatch</c> con <see cref="ScheduleKey"/> y
/// <see cref="BatchScopeKey"/> y sin <see cref="BatchId"/>; <c>NotPosted</c> → <c>NotApplicable</c>;
/// informativos y Cartera → <c>Always</c>/<c>Pending</c>. Después sólo la escriben
/// <c>RegisterDeliveryResultCommand</c>, <c>OrderIntegrationBatchCommand</c>, <c>ReprocessMessagesCommand</c>
/// y <c>SendNotApplicableMessagesCommand</c> (I2). El consumidor nunca la toca.
/// </para>
///
/// <para>
/// <c>[SinDiffDeAuditoria]</c>: cada intento la cambia y no es un hecho de negocio. <see cref="BatchId"/> no
/// tiene FK todavía: entra con <c>COR_IntegrationBatches</c> en I2.
/// </para>
/// </summary>
[SinDiffDeAuditoria]
public class IntegrationMessageDelivery : AuditableEntity
{
    public long MessageId { get; set; }

    public IntegrationMessage? Message { get; set; }

    /// <summary><c>IntegrationDestinations.Accounting</c> o <c>.Lending</c>.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Sellado al emitir; relacionados y derivados lo copian de la entrega de su original (T9).</summary>
    public DeliveryMode Mode { get; set; }

    public DeliveryStatus Status { get; set; }

    /// <summary><c>{DocumentTypeCode raíz}|{DisparadorDeLote}|{HoraDeLote}|{Granularidad}</c>, sellados al confirmar.</summary>
    public string? ScheduleKey { get; set; }

    /// <summary><c>CashSession:{publicId}</c> (<c>CierreDeTurno</c>) o <c>Period:{aaaa-mm}</c> (<c>CierreDePeriodo</c>).</summary>
    public string? BatchScopeKey { get; set; }

    /// <summary>El lote que la tomó; nulo hasta que uno la toma. Sin FK hasta I2.</summary>
    public int? BatchId { get; set; }

    public int Attempts { get; set; }

    public DateTime? NextAttemptAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public string? LastErrorCode { get; set; }

    public string? LastErrorMessage { get; set; }

    /// <summary><c>data.errors[] { lineNumber, account, rule, whoFixes }</c> tal como lo respondió el destino.</summary>
    public string? LastErrorDataJson { get; set; }

    public DateTime? ProcessedAt { get; set; }

    /// <summary><c>PublicId</c> del comprobante, o «informativo» / «sin comprobante (valor cero)».</summary>
    public string? ResultReference { get; set; }

    /// <summary>Tipo del comprobante que devolvió el destino; la bandeja lo lee de aquí, nunca de <c>ACC_</c>.</summary>
    public string? ResultVoucherTypeCode { get; set; }

    /// <summary>Número del comprobante que devolvió el destino.</summary>
    public string? ResultVoucherNumber { get; set; }
}
