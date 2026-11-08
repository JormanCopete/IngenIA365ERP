using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Channel;

namespace IngenIA365ERP.Domain.Entities.Channel;

// Feature 021 (data-model.md §A): el módulo «Canal digital» en la base de cada cooperativa. El canal (sistema satélite) no
// toca estas tablas: las lee y las cambia por la API de servicio del ERP (constitución v2.1.0, «Sistemas satélite»).

/// <summary>
/// <c>CDG_ChannelSettings</c>: la configuración del canal de la cooperativa. Cambiar crea una versión nueva con su vigencia
/// (FR-014); la vigente es la de mayor <see cref="Version"/> cuya vigencia ya empezó.
/// </summary>
public class ChannelSettings : AuditableEntity
{
    public int Version { get; set; }

    public DateTime EffectiveFrom { get; set; }

    /// <summary>FR-001: apagado, nadie ve información de la cooperativa en el canal.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Código de 3 letras que asigna la plataforma al encender; prefijo de las referencias de pago (R12). Inmutable.</summary>
    public string? ChannelCode { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public Guid? LogoAttachmentPublicId { get; set; }

    /// <summary>Color primario de la marca, <c>#RRGGBB</c>.</summary>
    public string? PrimaryColor { get; set; }

    /// <summary>Color secundario de la marca, <c>#RRGGBB</c>.</summary>
    public string? SecondaryColor { get; set; }

    public string? SupportPhone { get; set; }

    public string? SupportEmail { get; set; }

    public string? SupportHours { get; set; }

    /// <summary>FR-008: vencimiento de la sesión de pago en la pasarela, entre 10 y 30 minutos.</summary>
    public int PaymentSessionMinutes { get; set; } = 15;

    /// <summary>FR-009: exigir código de un solo uso al confirmar un pago.</summary>
    public bool RequireOtpToPay { get; set; }

    /// <summary>FR-027 (aclaración 1): inactividad de la sesión del asociado, entre 5 y 15 minutos.</summary>
    public int InactivityMinutes { get; set; } = 5;

    public EnablementPolicy EnablementPolicy { get; set; } = EnablementPolicy.AllActiveAssociates;

    /// <summary>FR-010: habilitar también a personas que no son asociados (ex asociados con saldo, deudores de otros cobros).</summary>
    public bool AllowNonAssociates { get; set; }

    /// <summary>FR-011a: ofrecer códigos por SMS (con su costo para la cooperativa).</summary>
    public bool SmsEnabled { get; set; }

    /// <summary>FR-012: días de anticipación del aviso de cuota próxima (0 = sin aviso).</summary>
    public int DueReminderDays { get; set; } = 3;

    public SurplusPolicy SurplusPolicy { get; set; } = SurplusPolicy.AdvanceElseCreditBalance;
}

/// <summary><c>CDG_LegalTexts</c>: un texto legal publicado. Una versión publicada no se edita: se publica otra.</summary>
public class ChannelLegalText : AuditableEntity
{
    public LegalTextKind Kind { get; set; }

    public int Version { get; set; }

    /// <summary>El contenido en Markdown.</summary>
    public string Content { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; }
}

/// <summary>
/// <c>CDG_PaymentDestinations</c>: a dónde va el dinero de un concepto: un sitio de pasarela y el convenio de recaudo de clase
/// pasarela del que salen la cuenta propia, las comisiones y el formato del archivo de conciliación. Las credenciales del sitio
/// viven sólo en el canal (research R10); aquí queda un espejo informativo de su estado.
/// </summary>
public class ChannelPaymentDestination : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public PaymentGateway Gateway { get; set; } = PaymentGateway.Placetopay;

    /// <summary>El identificador del sitio en el canal.</summary>
    public Guid SitePublicId { get; set; } = Guid.NewGuid();

    /// <summary>FK a <c>RCD_CollectionAgreements</c> con <c>Kind = Gateway</c>.</summary>
    public int CollectionAgreementId { get; set; }

    public bool OfferPse { get; set; } = true;

    public bool OfferCard { get; set; }

    /// <summary>FR-052: hora local (Colombia) del corte diario de la pasarela; después se reversa por devolución.</summary>
    public TimeOnly ReversalCutoffLocalTime { get; set; } = new(20, 0);

    public CredentialsStatus CredentialsStatus { get; set; } = CredentialsStatus.NotSet;

    public DateTime? CredentialsCheckedAt { get; set; }

    /// <summary>Últimos cuatro caracteres del login del sitio, para reconocerlo sin exponerlo.</summary>
    public string? LoginLast4 { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary><c>CDG_PublishedConcepts</c>: un concepto que la cooperativa publica en el canal (FR-002 a FR-004).</summary>
public class ChannelPublishedConcept : AuditableEntity
{
    public ConceptKind Kind { get; set; }

    /// <summary>Línea de crédito o de captación; nula = todas las de ese tipo.</summary>
    public string? LineCode { get; set; }

    /// <summary>FK a <c>CDG_PaymentDestinations</c>; nula = sólo consulta (FR-004).</summary>
    public int? DestinationId { get; set; }

    public bool AllowDue { get; set; } = true;

    public bool AllowPartial { get; set; }

    public decimal? PartialMinimum { get; set; }

    public bool AllowPrepayment { get; set; }

    public bool AllowPayoff { get; set; }

    public bool CanCombine { get; set; } = true;

    /// <summary>Sólo créditos: la opción de prepago preseleccionada (FR-032a).</summary>
    public ChannelPrepaymentChoice? DefaultPrepaymentChoice { get; set; }

    public bool PublishStatement { get; set; }

    public bool PublishCertificate { get; set; }

    public bool PublishTaxCertificate { get; set; }
}

/// <summary>
/// <c>CDG_ChannelEnablements</c>: la habilitación de una persona para el canal. Apunta a <c>COR_People</c> y no copia ningún
/// dato personal (Principio V). <see cref="EnabledAt"/> decide la entidad validadora (D5) y <see cref="ChangeSequence"/> es el
/// cursor con que el canal arrastra los cambios (research R8).
/// </summary>
public class ChannelEnablement : AuditableEntity
{
    public int PersonId { get; set; }

    public EnablementStatus Status { get; set; } = EnablementStatus.Enabled;

    public DateTime EnabledAt { get; set; }

    public EnablementSource Source { get; set; } = EnablementSource.Manual;

    public string? DisabledReason { get; set; }

    public DateTime? DisabledAt { get; set; }

    /// <summary>Creciente por cooperativa en cada cambio; el canal pide «lo que cambió después de N».</summary>
    public long ChangeSequence { get; set; }
}

/// <summary>
/// <c>CDG_ContactUpdates</c>: el asociado cambió su correo o su celular en el canal (FR-024, D7). La cooperativa lo ve como
/// novedad; nunca se aplica sola a <c>COR_People</c>.
/// </summary>
public class ChannelContactUpdate : AuditableEntity
{
    public int PersonId { get; set; }

    public ContactField Field { get; set; }

    public string NewValue { get; set; } = string.Empty;

    public DateTime ReportedAt { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public string? AcknowledgedBy { get; set; }
}

/// <summary>
/// <c>CDG_PaymentNotices</c>: un aviso de pago aprobado que llegó del canal. Único por <see cref="Reference"/> (FR-044). No se
/// edita salvo el bloque de reverso; lo contable vive en Recaudo y Contabilidad (Principio XI).
/// </summary>
public class ChannelPaymentNotice : AuditableEntity
{
    /// <summary>La referencia del intento en el canal (≤ 32 caracteres).</summary>
    public string Reference { get; set; } = string.Empty;

    public int PersonId { get; set; }

    public int DestinationId { get; set; }

    public decimal Total { get; set; }

    public long GatewayRequestId { get; set; }

    public long? InternalReference { get; set; }

    public string? Authorization { get; set; }

    public string? GatewayReceipt { get; set; }

    public string? Franchise { get; set; }

    public string? PaymentMethodName { get; set; }

    public string? IssuerName { get; set; }

    public DateTime ApprovedAt { get; set; }

    public string? PayerIp { get; set; }

    public string? PayerUserAgent { get; set; }

    /// <summary>La cuenta del canal que pagó (UUID del canal).</summary>
    public Guid? ChannelAccountId { get; set; }

    public NoticeOutcome Outcome { get; set; } = NoticeOutcome.Applied;

    public string? OutcomeReason { get; set; }

    /// <summary>FK a <c>RCD_Receipts</c>.</summary>
    public long? ReceiptId { get; set; }

    public DateTime? ReversedAt { get; set; }

    public long? ReversalReceiptId { get; set; }

    public string? ReversalReason { get; set; }

    public List<ChannelPaymentNoticeItem> Items { get; set; } = [];
}

/// <summary><c>CDG_PaymentNoticeItems</c>: cada concepto de un aviso de pago con su valor.</summary>
public class ChannelPaymentNoticeItem : AuditableEntity
{
    public int PaymentNoticeId { get; set; }

    public ConceptKind ConceptKind { get; set; }

    /// <summary>La obligación, la cuenta o la factura (por su <c>PublicId</c>).</summary>
    public Guid ProductPublicId { get; set; }

    /// <summary>La modalidad con que se pidió aplicar (nombre de la modalidad del contrato).</summary>
    public string Modality { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public ChannelPrepaymentChoice? PrepaymentChoice { get; set; }
}
