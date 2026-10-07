using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_DebtorMessages</c> (feature 013, C.3; data-model §26.1; FR-122, FR-123, SC-011, I-5; T74, T75): un mensaje al deudor o
/// a un codeudor obligado (nunca a referencias). Hecho de sólo inserción (<see cref="IHechoInmutable"/>): su estado es el último
/// <see cref="DebtorMessageEvent"/>. Canal, adaptador y plantilla quedan sellados. La columna <c>CampaignId</c> nace con K4
/// (<c>CarteraCobranza</c>).
/// </summary>
public class DebtorMessage : AuditableEntity, IHechoInmutable
{
    public int PersonId { get; init; }

    /// <summary><c>Debtor</c> o <c>CoDebtor</c> (<c>Lending.DebtorMessage.RecipientNotObligated</c>).</summary>
    public ContactRole RecipientRole { get; init; }

    /// <summary>Nulo en un documento general.</summary>
    public int? LoanId { get; init; }

    public DebtorMessagePurpose Purpose { get; init; }

    public MessageChannel Channel { get; init; }

    /// <summary>Código del adaptador sellado (<c>SIMULADO</c>, proveedor, correo).</summary>
    public string ChannelCode { get; init; } = string.Empty;

    public int? TemplateId { get; init; }

    public int? TemplateVersion { get; init; }

    public string? Subject { get; init; }

    public string RenderedBody { get; init; } = string.Empty;

    public string BodySha256 { get; init; } = string.Empty;

    /// <summary>Documento enviado (<c>DocumentDelivery</c>).</summary>
    public Guid? AttachmentPublicId { get; init; }

    /// <summary><c>{origen}:{entidad}:{propósito}:{fecha}</c>; único.</summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary><c>Manual</c>, <c>Campaign</c>, <c>CollectionStage</c>, <c>PriorNotice</c>, <c>NonDeduction</c>, <c>PaymentApplied</c>, <c>PaymentFormChange</c>, <c>Document</c>, <c>CollectionFee</c>.</summary>
    public string OriginKind { get; init; } = string.Empty;

    public Guid? OriginPublicId { get; init; }

    public int? PriorNoticeId { get; init; }

    public int? IssuedDocumentId { get; init; }

    public int? InstallmentNonDeductionId { get; init; }

    /// <summary>Autorización por canal vigente al programar (T69); <c>Letter</c> no la exige (C90).</summary>
    public Guid? AuthorizationConsentPublicId { get; init; }

    /// <summary>Envío manual inmediato: fuera de ventana se rechaza en vez de diferirse.</summary>
    public bool IsManualImmediate { get; init; }

    public DateTime RequestedAt { get; init; }

    public DateTime? ScheduledFor { get; init; }

    public ActorKind RequestedByKind { get; init; }

    public int? RequestedByUserId { get; init; }
}
