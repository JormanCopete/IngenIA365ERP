using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Operations;

/// <summary>
/// <c>RCD_ReceiptVoidings</c> (H; feature 015, US5, T0174; FR-057, FR-058, FR-194; data-model §4.1): la anulación de un recibo entero, un documento numerado con
/// motivo y aprobación de otra persona. Una por recibo (UK <c>ReceiptId</c>). Lo que decide la aprobación (quién aprobó, cuándo, el saldo a favor si la sesión ya
/// cerró, las aplicaciones omitidas porque el módulo ya las había reversado) se escribe una vez; <see cref="Status"/> cambia hasta <c>Applied</c> o <c>Rejected</c>.
/// (nuevo)
/// </summary>
public class ReceiptVoiding : AuditableEntity, IHechoInmutable
{
    /// <summary>El número del documento de anulación: se asigna al aplicarla (lo que queda en aprobación no consume número).</summary>
    [EscrituraUnica] public string? Number { get; set; }

    public long ReceiptId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int RequestedByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public Guid? ApprovalRequestPublicId { get; set; }

    /// <summary>Se pidió en la sesión abierta del recibo y el mismo día (FR-058).</summary>
    public bool InSameSession { get; set; }

    [ColumnaMutable] public ReceiptVoidingState Status { get; set; } = ReceiptVoidingState.PendingApproval;

    [EscrituraUnica] public int? ApprovedByUserId { get; set; }

    [EscrituraUnica] public DateTime? DecidedAt { get; set; }

    [EscrituraUnica] public string? DecisionReason { get; set; }

    /// <summary>Las aplicaciones que el módulo ya había reversado y la anulación no toca.</summary>
    [EscrituraUnica] public string? OmittedApplicationsJson { get; set; }

    /// <summary>El saldo a favor del pagador que nace si la sesión ya cerró (o el dinero llegó por un medio electrónico).</summary>
    [EscrituraUnica] public int? PayerCreditBalanceId { get; set; }

    /// <summary>El código de verificación del documento de anulación (nuevo).</summary>
    public string VerificationCode { get; set; } = string.Empty;
}

/// <summary>
/// <c>RCD_ModuleReversals</c> (H; feature 015, US5, T0174; FR-059, FR-195; data-model §4.2): una aplicación de un recibo que Cartera o Captaciones reversó y avisó
/// (<c>PagoReversadoPorCartera</c>, <c>ConsignacionReversadaPorCartera</c>). Sin causa en el aviso queda <c>PendingConfirmation</c>: una persona confirma si el dinero se
/// recibió (saldo a favor del pagador) o no (baja del medio). Nunca devuelve ni reaplica solo. (nuevo)
/// </summary>
public class ModuleReversal : AuditableEntity, IHechoInmutable
{
    public int InboundMessageId { get; set; }

    public long ReceiptApplicationId { get; set; }

    public long? PortionId { get; set; }

    /// <summary>El módulo que reversó (<c>CAR</c>).</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>El tipo del aviso (<c>PagoReversadoPorCartera</c> o <c>ConsignacionReversadaPorCartera</c>) (nuevo).</summary>
    public string MessageType { get; set; } = string.Empty;

    public Guid ModuleReversalPublicId { get; set; }

    public string ModuleReversalNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    [ColumnaMutable] public ModuleReversalCause Cause { get; set; } = ModuleReversalCause.PendingConfirmation;

    [EscrituraUnica] public int? ConfirmedByUserId { get; set; }

    [EscrituraUnica] public DateTime? ConfirmedAt { get; set; }

    [EscrituraUnica] public string? ConfirmationNote { get; set; }

    [EscrituraUnica] public int? PayerCreditBalanceId { get; set; }
}

/// <summary>
/// <c>RCD_PayerCreditBalances</c> (feature 015, US5, T0174; FR-054; data-model §4.3): el saldo a favor de un pagador por resolver —se devuelve por orden de pago a
/// Tesorería (R0), por caja (R1) o se aplica con un recibo nuevo—. <see cref="Balance"/> y <see cref="Status"/> cambian con sus movimientos; la antigüedad alerta
/// <c>Recaudo.SaldoAFavorAntiguo</c>. (nuevo)
/// </summary>
public class PayerCreditBalance : AuditableEntity
{
    public int PersonId { get; set; }

    public PayerCreditBalanceKind Kind { get; set; } = PayerCreditBalanceKind.Payer;

    public PayerCreditBalanceOrigin Origin { get; set; }

    public Guid OriginDocumentPublicId { get; set; }

    /// <summary>El número del documento que lo originó (nuevo).</summary>
    public string OriginDocumentNumber { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public PayerCreditBalanceState Status { get; set; } = PayerCreditBalanceState.Open;

    public Guid? OwnBankAccountPublicId { get; set; }

    public DateOnly OpenedOn { get; set; }
}

/// <summary><c>RCD_PayerCreditBalanceMovements</c> (H): reconocido, devuelto por orden, por caja, aplicado con recibo o reabierto por la orden rechazada. (nuevo)</summary>
public class PayerCreditBalanceMovement : AuditableEntity, IHechoInmutable
{
    public int PayerCreditBalanceId { get; set; }

    public PayerCreditBalanceMovementKind Kind { get; set; }

    public decimal Amount { get; set; }

    /// <summary>La orden de pago, el egreso o el recibo.</summary>
    public Guid? DocumentPublicId { get; set; }

    public string? DocumentNumber { get; set; }

    public int? RecordedByUserId { get; set; }

    public DateTime RecordedAt { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// <c>RCD_PayerReceivables</c> (feature 015, T0174; FR-060; data-model §4.5): la cuenta por cobrar al pagador cuando el dinero que respaldaba una aplicación ya
/// entregada no llegó (un cheque devuelto, US15; un medio dado de baja). <see cref="Balance"/> y <see cref="Status"/> cambian al cobrarla o castigarla. (nuevo)
/// </summary>
public class PayerReceivable : AuditableEntity
{
    public int PersonId { get; set; }

    public Guid OriginDocumentPublicId { get; set; }

    public string OriginDocumentNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public PayerReceivableState Status { get; set; } = PayerReceivableState.Open;

    public DateOnly OpenedOn { get; set; }
}

/// <summary>
/// <c>RCD_InboundMessages</c> (feature 015, US5, T0174; FR-151; data-model §6.1, molde de <c>LND_InboundMessages</c> y <c>TES_InboundMessages</c>): el recibo
/// único de cada mensaje que consume <c>DestinoRecaudo</c>, en el mismo <c>SaveChanges</c> que su efecto. Único por <see cref="MessagePublicId"/>. (nuevo)
/// </summary>
public class ReceiptsInboundMessage : AuditableEntity
{
    public Guid MessagePublicId { get; set; }

    public string Type { get; set; } = string.Empty;

    public short Version { get; set; }

    public string OriginModule { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public InboundMessageOutcome Outcome { get; set; }

    public string? Code { get; set; }

    public string? Detail { get; set; }
}
