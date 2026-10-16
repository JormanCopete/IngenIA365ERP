using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_DebtorMessages</c> (feature 013, C.3; data-model §26.1; FR-122, FR-123, SC-011, I-5; T74, T75): un mensaje al deudor o
/// a un codeudor obligado (nunca a referencias). Hecho de sólo inserción (<see cref="IHechoInmutable"/>): su estado es el último
/// <see cref="DebtorMessageEvent"/>. Canal, adaptador y plantilla quedan sellados. La columna <c>CampaignId</c> nació con K4
/// (<c>CarteraCobranza</c>).
///
/// <para>
/// T0305: nace sólo por <see cref="Nacer"/>, con todo sellado de una vez —destinatario obligado (deudor o codeudor, nunca una
/// referencia: <c>Lending.DebtorMessage.RecipientNotObligated</c>), canal y adaptador, plantilla y versión, cuerpo renderizado y su
/// <see cref="BodySha256"/>, clave de idempotencia, origen, autorización de canal y quién lo pidió—; después no cambia nada: el estado
/// es el último evento. Sin columnas nuevas: las del contrato (adaptadores-y-archivos.md §4.2) caen en las que ya existen
/// (<c>RequestedFor</c> → <see cref="ScheduledFor"/>/<see cref="RequestedAt"/>, <c>Origin</c> → <see cref="OriginKind"/>,
/// <c>AuthorizationPublicId</c> → <see cref="AuthorizationConsentPublicId"/>).
/// </para>
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

    /// <summary>K4 (T0939; data-model §26.1): la campaña que lo programó (<c>LND_MessageCampaigns</c>).</summary>
    public int? CampaignId { get; init; }

    /// <summary>Autorización por canal vigente al programar (T69); <c>Letter</c> no la exige (C90).</summary>
    public Guid? AuthorizationConsentPublicId { get; init; }

    /// <summary>Envío manual inmediato: fuera de ventana se rechaza en vez de diferirse.</summary>
    public bool IsManualImmediate { get; init; }

    public DateTime RequestedAt { get; init; }

    public DateTime? ScheduledFor { get; init; }

    public ActorKind RequestedByKind { get; init; }

    public int? RequestedByUserId { get; init; }

    public const string CodigoDestinatarioNoObligado = "Lending.DebtorMessage.RecipientNotObligated";

    /// <summary>
    /// El envío de una simulación a la persona simulada (feature 013, US2, T0390, C87): informativo, por correo y sin crédito.
    /// </summary>
    public const string OrigenSimulacion = "Simulation";

    /// <summary>Los orígenes admitidos en <see cref="OriginKind"/> (data-model §26.1; US2 suma <see cref="OrigenSimulacion"/>, C87).</summary>
    public static IReadOnlyList<string> Origenes { get; } =
        ["Manual", "Campaign", "CollectionStage", "PriorNotice", "NonDeduction", "PaymentApplied", "PaymentFormChange", "Document", "CollectionFee",
            OrigenSimulacion];

    /// <summary>El largo de <see cref="IdempotencyKey"/> (nvarchar(200)).</summary>
    public const int LargoDeLaClave = 200;

    /// <summary>
    /// Lo que impide que el mensaje nazca, o nulo. Sólo deudor o codeudor obligado; un mensaje de un crédito lleva el crédito (salvo la
    /// entrega de un documento general); el cuerpo no está vacío; la clave y el origen tienen forma; lo pide una persona con usuario o
    /// el proceso sin usuario.
    /// </summary>
    public static ProblemaDeCartera? Validar(BorradorDeMensajeAlDeudor m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.RecipientRole is not (ContactRole.Debtor or ContactRole.CoDebtor))
            return new ProblemaDeCartera(CodigoDestinatarioNoObligado,
                "Los mensajes de Cartera sólo van al deudor o a un codeudor obligado del crédito.");
        if (!Enum.IsDefined(m.Purpose) || !Enum.IsDefined(m.Channel))
            throw new ArgumentException("Propósito o canal de mensaje desconocido.", nameof(m));
        if (m.LoanId is null && m.Purpose != DebtorMessagePurpose.DocumentDelivery
            && !(m.Purpose == DebtorMessagePurpose.Informative && m.OriginKind == OrigenSimulacion))
            throw new ArgumentException("Sólo la entrega de un documento general y el envío de una simulación van sin crédito.", nameof(m));
        if (string.IsNullOrWhiteSpace(m.RenderedBody)) throw new ArgumentException("El cuerpo renderizado no puede estar vacío.", nameof(m));
        if (string.IsNullOrWhiteSpace(m.ChannelCode)) throw new ArgumentException("El canal se sella con el código de su adaptador.", nameof(m));
        if (string.IsNullOrWhiteSpace(m.IdempotencyKey) || m.IdempotencyKey.Length > LargoDeLaClave)
            throw new ArgumentException($"La clave de idempotencia es obligatoria y de hasta {LargoDeLaClave} caracteres.", nameof(m));
        if (!Origenes.Contains(m.OriginKind, StringComparer.Ordinal))
            throw new ArgumentException($"«{m.OriginKind}» no es un origen de mensaje al deudor.", nameof(m));
        if (m.TemplateId is null != m.TemplateVersion is null)
            throw new ArgumentException("La plantilla se sella con su versión.", nameof(m));
        if ((m.RequestedByKind == ActorKind.Person) != m.RequestedByUserId.HasValue)
            throw new ArgumentException("Lo pide una persona con su usuario, o el proceso sin usuario.", nameof(m));
        return null;
    }

    /// <summary>El mensaje, sellado de una vez (lanza si no se validó antes). Su primer evento lo escribe la cola al guardarlo.</summary>
    public static DebtorMessage Nacer(BorradorDeMensajeAlDeudor m)
    {
        ReglaDeCarteraException.SiHay(Validar(m));
        return new DebtorMessage
        {
            PersonId = m.PersonId,
            RecipientRole = m.RecipientRole,
            LoanId = m.LoanId,
            Purpose = m.Purpose,
            Channel = m.Channel,
            ChannelCode = m.ChannelCode.Trim(),
            TemplateId = m.TemplateId,
            TemplateVersion = m.TemplateVersion,
            Subject = string.IsNullOrWhiteSpace(m.Subject) ? null : m.Subject,
            RenderedBody = m.RenderedBody,
            BodySha256 = HuellaDeCartera.DeTexto(m.RenderedBody),
            AttachmentPublicId = m.AttachmentPublicId,
            IdempotencyKey = m.IdempotencyKey.Trim(),
            OriginKind = m.OriginKind,
            OriginPublicId = m.OriginPublicId,
            PriorNoticeId = m.PriorNoticeId,
            IssuedDocumentId = m.IssuedDocumentId,
            InstallmentNonDeductionId = m.InstallmentNonDeductionId,
            CampaignId = m.CampaignId,
            AuthorizationConsentPublicId = m.AuthorizationConsentPublicId,
            IsManualImmediate = m.IsManualImmediate,
            RequestedAt = m.RequestedAt,
            ScheduledFor = m.ScheduledFor,
            RequestedByKind = m.RequestedByKind,
            RequestedByUserId = m.RequestedByUserId,
        };
    }

    /// <summary>El cuerpo guardado sigue siendo el que se selló.</summary>
    public bool CuerpoIntacto() => HuellaDeCartera.DeTexto(RenderedBody) == BodySha256;
}
