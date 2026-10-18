namespace IngenIA365ERP.Domain.Enums.Lending;

// Mensajes al deudor, cobranza (K4), reclamos, documentos emitidos y archivos regulatorios (K5) (feature 013, T0201; data-model §9.1
// y parte 2 «Nombres fijados», C65). <c>DebtorMessagePurpose</c> y <c>MessageChannel</c> viven en sus propios archivos (T0045).

/// <summary>A quién va un mensaje: sólo deudor y codeudores obligados; nunca referencias personales.</summary>
public enum ContactRole
{
    Debtor = 1,
    CoDebtor = 2,
    Paymaster = 3,
}

/// <summary>Evento de la historia de un mensaje al deudor (F-04).</summary>
public enum DebtorMessageEventKind
{
    Queued = 1,
    Deferred = 2,
    Excluded = 3,
    Sent = 4,
    Delivered = 5,
    Failed = 6,
    Simulated = 7,
    Cancelled = 8,
}

/// <summary>
/// Qué originó un mensaje al deudor (<c>adaptadores-y-archivos.md</c> §4.2, fijado ahí; T0304). Se guarda como <b>texto</b> en
/// <c>LND_DebtorMessages.OriginKind</c>; los números siguen el orden del contrato.
/// </summary>
public enum DebtorMessageOrigin
{
    Operation = 1,
    Process = 2,
    PriorNotice = 3,
    Campaign = 4,
    Manual = 5,
    IssuedDocument = 6,
}

/// <summary>
/// Por qué la ventana legal de contacto (Ley 2300) excluye o difiere un mensaje (<c>adaptadores-y-archivos.md</c> §4.5, fijado
/// ahí; T0304). Va en los eventos <c>Deferred</c> y <c>Excluded</c> de <c>LND_DebtorMessageEvents</c>. <see cref="ChannelUnavailable"/>
/// es técnico: lo pone el despachador cuando el canal no responde, nunca el motor <c>VentanaLegalDeContacto</c>.
/// </summary>
public enum ContactWindowReason
{
    RecipientNotObligated = 1,
    DebtorDeceased = 2,
    Insolvency = 3,
    CollectionSuspended = 4,
    ChannelNotAuthorized = 5,
    AuthorizationWithoutEvidence = 6,
    NoDestination = 7,
    Sunday = 8,
    Holiday = 9,
    OutsideHours = 10,
    DailyLimitReached = 11,
    WeeklyChannelLocked = 12,
    NoWindow = 13,
    ChannelUnavailable = 14,
}

/// <summary>Canal de una gestión de cobranza (K4).</summary>
public enum CollectionActionChannel
{
    Phone = 1,
    InPerson = 2,
    Email = 3,
    Sms = 4,
    WhatsApp = 5,
    Letter = 6,
    Visit = 7,

    /// <summary>K4, bloque D: una gestión que deja el sistema (la salida de cobranza), no un contacto; ninguna persona la registra.</summary>
    System = 8,
}

/// <summary>Resultado de una gestión de cobranza (K4).</summary>
public enum CollectionActionResult
{
    NoAnswer = 1,
    WrongNumber = 2,
    ContactedNoCommitment = 3,
    PromiseToPay = 4,
    Refused = 5,
    RequestsAgreement = 6,
    ReportsDeceased = 7,
    Other = 8,

    /// <summary>K4, bloque D: el crédito salió de cobranza porque se puso al día (sólo en una gestión de sistema).</summary>
    LeftCollection = 9,
}

/// <summary>Reparto de cartera entre gestores (K4).</summary>
public enum AssignmentDistribution
{
    ByCount = 1,
    ByBalance = 2,
}

/// <summary>Estado de una promesa de pago: calculado (K4).</summary>
public enum PaymentPromiseStatus
{
    Pending = 1,
    Kept = 2,
    Broken = 3,
}

/// <summary>Estado de un acuerdo de pago (K4).</summary>
public enum PaymentAgreementStatus
{
    Proposed = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

/// <summary>Cumplimiento de un acuerdo de pago: calculado (K4).</summary>
public enum PaymentAgreementPerformance
{
    Current = 1,
    Fulfilled = 2,
    Broken = 3,
}

/// <summary>Estado de un proceso jurídico (K4).</summary>
public enum LegalProcessStatus
{
    Assigned = 1,
    Filed = 2,
    InProgress = 3,
    Suspended = 4,
    Concluded = 5,
    Withdrawn = 6,
}

/// <summary>Actuación de un proceso jurídico (K4).</summary>
public enum LegalActionKind
{
    Filing = 1,
    Order = 2,
    Seizure = 3,
    Hearing = 4,
    Judgment = 5,
    Fee = 6,
    Expense = 7,
    StatusChange = 8,
    Other = 9,
}

/// <summary>Estado de una campaña de mensajes (K4).</summary>
public enum MessageCampaignStatus
{
    Draft = 1,
    Scheduled = 2,
    Running = 3,
    Completed = 4,
    Cancelled = 5,
}

/// <summary>Estado de un aviso previo: calculado al leer (T77).</summary>
public enum PriorNoticeStatus
{
    Pending = 1,
    InProgress = 2,
    Valid = 3,
    Closed = 4,
}

/// <summary>Clase de reclamo sobre el reporte a centrales (S10).</summary>
public enum CreditReportClaimKind
{
    Claim = 1,
    Impersonation = 2,
}

/// <summary>Estado de un reclamo sobre el reporte (S10; nombre fijado en data-model §9.1).</summary>
public enum CreditReportClaimStatus
{
    InProcess = 1,
    Answered = 2,
    TacitlyAccepted = 3,
    Withdrawn = 4,
}

/// <summary>Clase de documento emitido con código de verificación (F-13). <c>Statement</c> y <c>TaxCertificate</c> son de K5.</summary>
public enum IssuedDocumentKind
{
    PaymentPlan = 1,
    Statement = 2,
    LoanClearance = 3,
    GeneralClearance = 4,
    BalanceCertificate = 5,
    TaxCertificate = 6,

    // Feature 014 (T014; research §0.3, C27; contracts/documentos.md §1): las clases de captaciones y aportes, al final. Se emiten desde su
    // entrega (PrefijosDeDocumentosDeCartera.Disponible).
    AccountStatement = 7,
    AccountBalanceCertificate = 8,
    DepositTaxCertificate = 9,
    WithholdingCertificate = 10,
    TermDepositTitle = 11,
    ContributionCertificate = 12,
    ContributionClearance = 13,
    MembershipWithdrawalSettlement = 14,

    /// <summary>La respuesta a un juzgado por un embargo (data-model §5.2.1, nuevo en data-model).</summary>
    GarnishmentResponse = 15,
}

/// <summary>Estado de un documento emitido: calculado (F-13).</summary>
public enum IssuedDocumentStatus
{
    Valid = 1,
    SubjectToFinality = 2,
    Voided = 3,
}

/// <summary>Evento de un documento emitido. <c>Voided = 3</c> es el filtro de la única anulación.</summary>
public enum IssuedDocumentEventKind
{
    Issued = 1,
    Delivered = 2,
    Voided = 3,
}

/// <summary>Cómo se entregó un documento emitido.</summary>
public enum DocumentDeliveryMode
{
    Download = 1,
    Email = 2,
}

/// <summary>Clase de archivo regulatorio (K5, F-12).</summary>
public enum RegulatoryFileKind
{
    Sicses = 1,
    CreditBureau = 2,
}

/// <summary>Estado de un archivo regulatorio (K5, F-12).</summary>
public enum RegulatoryFileStatus
{
    Validated = 1,
    Generated = 2,
    Superseded = 3,
    Uploaded = 4,
}
