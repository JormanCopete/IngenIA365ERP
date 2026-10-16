using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Collections;

// Feature 013 (T0305): lo que reciben las fábricas de LND_DebtorMessages y LND_DebtorMessageEvents. Viven fuera de Domain.Entities
// porque no son tablas (PrincipioVII_SoftDeleteAndAuditable). (nuevo)

/// <summary>Lo que se sella en un mensaje al deudor (columnas de data-model §26.1). (nuevo)</summary>
public sealed record BorradorDeMensajeAlDeudor(
    int PersonId,
    ContactRole RecipientRole,
    int? LoanId,
    DebtorMessagePurpose Purpose,
    MessageChannel Channel,
    string ChannelCode,
    string RenderedBody,
    string IdempotencyKey,
    string OriginKind,
    DateTime RequestedAt,
    ActorKind RequestedByKind,
    int? RequestedByUserId)
{
    public int? TemplateId { get; init; }
    public int? TemplateVersion { get; init; }
    public string? Subject { get; init; }
    public Guid? AttachmentPublicId { get; init; }
    public Guid? OriginPublicId { get; init; }
    public int? PriorNoticeId { get; init; }
    public int? IssuedDocumentId { get; init; }
    public int? InstallmentNonDeductionId { get; init; }

    /// <summary>K4: la campaña que lo programa.</summary>
    public int? CampaignId { get; init; }
    public Guid? AuthorizationConsentPublicId { get; init; }
    public bool IsManualImmediate { get; init; }
    public DateTime? ScheduledFor { get; init; }
}

/// <summary>Lo que trae un paso de la historia de un mensaje al deudor (columnas de data-model §26.2). (nuevo)</summary>
public sealed record EventoDeMensaje(DebtorMessageEventKind Kind, DateTime OccurredAt, ActorKind ActorKind, int? ActorUserId = null)
{
    /// <summary><c>Queued</c>: desde cuándo sale; <c>Deferred</c>: el instante siguiente permitido (<c>NextAttemptAt</c>).</summary>
    public DateTime? DueAt { get; init; }

    /// <summary><c>Deferred</c> y <c>Excluded</c>: los motivos de la ventana legal (<c>Reasons</c>).</summary>
    public IReadOnlyCollection<ContactWindowReason>? Motivos { get; init; }

    /// <summary>En las demás clases, un código libre (la respuesta del adaptador, la causa de la cancelación).</summary>
    public string? CodigoDeMotivo { get; init; }

    public string? TextoDelMotivo { get; init; }

    public string? DestinationAddress { get; init; }

    /// <summary><c>Attempt</c>.</summary>
    public int? AttemptNumber { get; init; }

    public string? ProviderReference { get; init; }

    public ExternalOutcome? ExternalOutcome { get; init; }

    public Guid? ProviderArtifactAttachmentPublicId { get; init; }
}
