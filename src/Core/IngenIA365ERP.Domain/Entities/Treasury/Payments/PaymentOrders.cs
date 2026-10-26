using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Payments;

/// <summary>
/// <c>TES_InboundMessages</c> (feature 015, US10, T0135; FR-151; data-model §10.1, molde de <c>LND_InboundMessages</c>): el recibo único de cada mensaje que
/// consume <c>DestinoTesoreria</c>, guardado en el mismo <c>SaveChanges</c> que su efecto. Único por <see cref="MessagePublicId"/>: repetir el mensaje no
/// cambia nada. (nuevo)
/// </summary>
public class TreasuryInboundMessage : AuditableEntity
{
    public Guid MessagePublicId { get; set; }

    public string Type { get; set; } = string.Empty;

    public short Version { get; set; }

    /// <summary>El módulo que lo emitió (<c>CAR</c>).</summary>
    public string OriginModule { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public InboundMessageOutcome Outcome { get; set; }

    public string? Code { get; set; }

    public string? Detail { get; set; }

    /// <summary>La orden que creó o anuló (<c>TES_PaymentOrders.Id</c>), si la hay.</summary>
    public int? PaymentOrderId { get; set; }
}

/// <summary>
/// <c>TES_PaymentOrders</c> (feature 015, US10, T0135; FR-107 a FR-109, FR-114 a FR-116, FR-121, FR-206, FR-210; data-model §10.2, §19.8): la bandeja única de
/// órdenes de pago —las órdenes de giro de Cartera y Captaciones y las devoluciones de Recaudo—. Lo que trae el origen queda sellado (beneficiario, cuenta
/// destino, neto, forma); <see cref="Status"/>, los motivos de pendiente y las marcas cambian por la validación, los lotes, los resultados y la anulación del
/// origen. Una orden repetida no se duplica (UK <c>(Origin, OriginOrderPublicId)</c>); el valor girado es exactamente <see cref="NetAmount"/> (FR-116).
/// Nunca se borra. (nuevo)
/// </summary>
public class PaymentOrder : AuditableEntity, IngenIA365ERP.Domain.Common.IDocumentoNumerado
{
    /// <summary>El número de Tesorería (clase <c>OP</c>).</summary>
    public string Number { get; set; } = string.Empty;

    public PaymentOrderOrigin Origin { get; set; }

    public Guid OriginOrderPublicId { get; set; }

    public string OriginOrderNumber { get; set; } = string.Empty;

    /// <summary>El <c>OrdenDeGiroEmitida</c> que la trajo (el <c>OrderMessagePublicId</c> del resultado); nulo en las órdenes de Recaudo, que no usan mensaje.</summary>
    public Guid? OriginMessagePublicId { get; set; }

    public short? OriginMessageVersion { get; set; }

    /// <summary>La clase de orden: <c>LoanDisbursement</c>, <c>DepositWithdrawal</c>… o las de Recaudo (<c>PayerCreditBalanceRefund</c>…).</summary>
    public string OrderKind { get; set; } = string.Empty;

    public PaymentForm Form { get; set; }

    public int BeneficiaryPersonId { get; set; }

    /// <summary>Sellados del mensaje (nuevo): con ellos se escribe la línea del archivo y se coteja la respuesta.</summary>
    public string BeneficiaryName { get; set; } = string.Empty;

    public string? BeneficiaryTaxIdType { get; set; }

    public string BeneficiaryTaxId { get; set; } = string.Empty;

    /// <summary>El banco destino por su código ACH, como llegó (<c>COR_Banks.TransferCode</c>).</summary>
    public string? DestinationBankCode { get; set; }

    public int? DestinationBankId { get; set; }

    public string? DestinationAccountNumber { get; set; }

    public string? DestinationAccountType { get; set; }

    /// <summary>La cuenta registrada del beneficiario que casa (FR-109, FR-210).</summary>
    public int? PersonBankAccountId { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal NetAmount { get; set; }

    public DateOnly PayDate { get; set; }

    public string Concept { get; set; } = string.Empty;

    /// <summary>El documento que la origina en su módulo (retiro, CDAT, saldo a favor, partida), si lo informa.</summary>
    public Guid? SourceDocumentPublicId { get; set; }

    /// <summary>El beneficiario es el deudor del desembolso (nuevo): el tercero principal del sobre es el beneficiario. Lo usa la exención del GMF (FR-206).</summary>
    public bool BeneficiaryIsDebtor { get; set; }

    /// <summary>La sucursal del sobre (nuevo): el resultado del giro sale con ella.</summary>
    public Guid? BranchPublicId { get; set; }

    /// <summary><c>PaidByFile</c> de Cartera: no entra a lotes (FR-108).</summary>
    public bool PaidByLendingFile { get; set; }

    public int? LendingDisbursementFileId { get; set; }

    /// <summary>La orden fallida que esta regira (FR-121).</summary>
    public int? ReplacesOrderId { get; set; }

    public bool GmfExempt { get; set; }

    public string? GmfExemptionReason { get; set; }

    public decimal? GmfEstimate { get; set; }

    public PaymentOrderState Status { get; set; } = PaymentOrderState.Received;

    /// <summary>Los motivos de pendiente (<c>DestinationNotRegistered</c>, <c>BankWithoutTransferCode</c>, <c>BindingListMatch</c>, <c>DestinationWaiting</c>…).</summary>
    public string PendingReasonsJson { get; set; } = "[]";

    /// <summary>Las marcas antifraude de la última validación (<see cref="PaymentBatchItemFlags"/>).</summary>
    public PaymentBatchItemFlags Flags { get; set; }

    public bool AccountChangedRecently { get; set; }

    /// <summary>Llegó la anulación del origen con la orden ya enviada (FR-115).</summary>
    public bool VoidRequested { get; set; }

    public string? VoidReason { get; set; }

    /// <summary>Quien anuló en el origen (sellado del sobre): el resultado <c>Voided</c> sale a su nombre (mensajes.md §5.2).</summary>
    public Guid? VoidedByCentralUserId { get; set; }

    public string? VoidedByName { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ValidatedAt { get; set; }

    // ---- T1 (fase 34, T0417; FR-211): la orden manual de Tesorería, con su concepto del catálogo cerrado, su soporte, la retención y sus dos aprobadores. ----

    /// <summary>El concepto del catálogo cerrado (<c>TES_ManualOrderConcepts</c>) de una orden manual; nulo en las demás. (nuevo)</summary>
    public int? ManualOrderConceptId { get; set; }

    /// <summary>El soporte obligatorio de una orden manual (factura, cuenta de cobro, acta). (nuevo)</summary>
    public Guid? SupportAttachmentPublicId { get; set; }

    /// <summary>La retención en la fuente practicada en una orden manual: bruto = neto + retención. (nuevo)</summary>
    public decimal? WithholdingAmount { get; set; }

    /// <summary>Quien preparó la orden manual (<c>SEC_Users.Id</c>). (nuevo)</summary>
    public int? PreparedByUserId { get; set; }

    /// <summary>En qué va la aprobación de una orden manual; mientras no esté <c>Approved</c> la orden no queda lista. (nuevo)</summary>
    public ManualOrderApprovalState? ManualApprovalState { get; set; }

    public int? FirstApprovedByUserId { get; set; }

    public DateTime? FirstApprovedAt { get; set; }

    public int? SecondApprovedByUserId { get; set; }

    public DateTime? SecondApprovedAt { get; set; }

    public string? ManualRejectionReason { get; set; }

    public bool EsManualSinAprobar => Origin == PaymentOrderOrigin.Manual && ManualApprovalState != ManualOrderApprovalState.Approved;

    public bool EsDeGiro => Origin is PaymentOrderOrigin.LendingDisbursement or PaymentOrderOrigin.DepositsPayout;
}
