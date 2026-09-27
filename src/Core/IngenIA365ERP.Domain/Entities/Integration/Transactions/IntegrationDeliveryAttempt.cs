using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.Integration.Transactions;

/// <summary>
/// Un intento de entregar un mensaje a un destino (<c>COR_IntegrationDeliveryAttempts</c>; feature 012, T10, T476;
/// data-model §19). Lo agrega <c>RegisterDeliveryResultCommand</c> con lo que respondió el destino
/// (<see cref="Outcome"/>), quién actuó —el proceso, o la persona que ordenó el lote o el reproceso— y en qué réplica.
/// Único <c>(DeliveryId, AttemptNumber)</c>.
///
/// <para>
/// Es un hecho (<see cref="IHechoInmutable"/>): la bitácora no se corrige. <c>[SinDiffDeAuditoria]</c> porque es
/// técnico y abundante: la auditoría de negocio está en el comprobante y en la entrega.
/// </para>
/// </summary>
[SinDiffDeAuditoria]
public class IntegrationDeliveryAttempt : AuditableEntityLong, IHechoInmutable
{
    public int DeliveryId { get; init; }

    public IntegrationMessageDelivery? Delivery { get; init; }

    /// <summary>Desnormalizado de la entrega.</summary>
    public long MessageId { get; init; }

    public int AttemptNumber { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime FinishedAt { get; init; }

    public int DurationMs { get; init; }

    public DeliveryAttemptOutcome Outcome { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }

    public string ActorName { get; init; } = string.Empty;

    /// <summary>El lote por el que pasó, si pasó por uno.</summary>
    public int? BatchId { get; init; }

    /// <summary>La réplica que lo procesó.</summary>
    public string Instance { get; init; } = string.Empty;
}
