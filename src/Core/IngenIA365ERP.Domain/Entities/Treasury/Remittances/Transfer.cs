using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Remittances;

/// <summary>
/// <c>TES_Transfers</c> (feature 015, T1, fase 33, T0410; data-model §13.1; FR-104, FR-207): un traslado entre dos cuentas propias de la cooperativa. Se pide con su motivo
/// y pide la aprobación diferida por monto (sujeto <c>TreasuryTransfer</c>, permiso con monto <c>Treasury.Transfers.Approve</c>); aprobado, una persona lo marca enviado al
/// ejecutarlo en el portal del banco —sale de la cuenta de origen y queda en tránsito con <c>TrasladoEntreCuentasEnviado</c>— y llega a la de destino por el cruce del
/// extracto en la conciliación o confirmado con el soporte del banco (<c>TrasladoConfirmado</c>). Desde una cuenta del fondo de liquidez exige un uso aprobado. No es un
/// hecho: cambia por sus comandos y nunca se borra. (nuevo)
/// </summary>
public class Transfer : AuditableEntity, IngenIA365ERP.Domain.Common.IDocumentoNumerado
{
    public string Number { get; set; } = string.Empty;

    public int FromBankAccountId { get; set; }

    public int ToBankAccountId { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? SupportAttachmentPublicId { get; set; }

    public TransferState Status { get; set; } = TransferState.PendingApproval;

    public int RequestedByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateOnly RequestedOn { get; set; }

    public Guid? ApprovalRequestPublicId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionReason { get; set; }

    /// <summary>El uso aprobado del fondo de liquidez cuando la cuenta de origen es del fondo.</summary>
    public int? LiquidityFundUseId { get; set; }

    public int? SentByUserId { get; set; }

    public DateTime? SentAt { get; set; }

    public DateOnly? SentOn { get; set; }

    /// <summary>La referencia que dio el portal del banco al ejecutarlo.</summary>
    public string? BankReference { get; set; }

    public int? ConfirmedByUserId { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateOnly? ConfirmedOn { get; set; }

    /// <summary>La línea del extracto de la cuenta de destino que lo confirmó (por su <c>PublicId</c>).</summary>
    public Guid? ConfirmedByStatementLinePublicId { get; set; }

    public Guid? ConfirmationSupportAttachmentPublicId { get; set; }

    public string? VoidReason { get; set; }
}
