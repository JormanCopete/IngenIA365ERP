using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_DebtorMessageEvents</c> (feature 013, C.3; data-model §26.2; T74, T75): cada paso de un <see cref="DebtorMessage"/>
/// (encolado, diferido, excluido, enviado, entregado, fallido, simulado, cancelado). Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>) de alto volumen (<c>bigint</c>). La dirección usada vive aquí y no en el mensaje (C57).
/// </summary>
public class DebtorMessageEvent : AuditableEntityLong, IHechoInmutable
{
    public int DebtorMessageId { get; init; }

    public int Sequence { get; init; }

    public DebtorMessageEventKind Kind { get; init; }

    public DateTime OccurredAt { get; init; }

    /// <summary><c>Queued</c> y <c>Deferred</c>: instante desde el que puede salir.</summary>
    public DateTime? DueAt { get; init; }

    /// <summary><c>OutsideHours</c>, <c>Holiday</c>, <c>DailyCap</c>, <c>WeeklyChannelUsed</c>, <c>ChannelNotAuthorized</c>…</summary>
    public string? ReasonCode { get; init; }

    public string? ReasonText { get; init; }

    /// <summary>Copia del correo o número usado al enviar (I-1); en <c>Sent</c>, <c>Simulated</c> y <c>Failed</c>.</summary>
    public string? DestinationAddress { get; init; }

    public int? AttemptNumber { get; init; }

    public string? ProviderReference { get; init; }

    public ExternalOutcome? ExternalOutcome { get; init; }

    /// <summary>Constancia del proveedor o guía de una carta (<c>LendingPriorNoticeEvidence</c>).</summary>
    public Guid? ProviderArtifactAttachmentPublicId { get; init; }

    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }
}
