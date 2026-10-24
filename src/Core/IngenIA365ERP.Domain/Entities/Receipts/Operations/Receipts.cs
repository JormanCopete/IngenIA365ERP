using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Operations;

/// <summary>
/// <c>RCD_CashOperations</c> (feature 015, T0110; R-07; FR-038 a FR-041, FR-189, FR-191; data-model §3.2): la operación de caja «en preparación». Su
/// <see cref="BaseEntity.PublicId"/> es estable y <b>será el del recibo</b>. No es un hecho: cambia mientras prepara (lo elegido, los medios, la
/// imputación) y queda <c>Confirmed</c> con su recibo, o <c>Abandoned</c>. (nuevo)
/// </summary>
public class CashOperation : AuditableEntity
{
    public int CashSessionId { get; set; }

    /// <summary>Quien paga (<c>COR_People.Id</c>).</summary>
    public int PayerPersonId { get; set; }

    /// <summary>El titular cuando paga un tercero.</summary>
    public int? BeneficiaryPersonId { get; set; }

    /// <summary>Quien efectúa el pago en la ventanilla (FR-191).</summary>
    public int? DeliveredByPersonId { get; set; }

    public CashOperationState Status { get; set; } = CashOperationState.Preparing;

    /// <summary>Las aplicaciones elegidas con su modalidad, valor, elección del deudor y última simulación.</summary>
    public string ItemsJson { get; set; } = "[]";

    /// <summary>Los medios con su valor y referencia.</summary>
    public string MeansJson { get; set; } = "[]";

    /// <summary>Las porciones propuestas por <c>ImputacionDeMedios</c> o fijadas por el cajero.</summary>
    public string ImputationJson { get; set; } = "[]";

    /// <summary>Si la imputación la fijó el cajero (no la propuesta).</summary>
    public bool ImputationFixed { get; set; }

    /// <summary>La fecha valor de la operación (Colombia); anterior a hoy exige la regla de FR-192.</summary>
    public DateOnly ValueDate { get; set; }

    public string? BackdateReason { get; set; }

    public Guid? BackdateSupportAttachmentPublicId { get; set; }

    /// <summary>La <c>Idempotency-Key</c> de la confirmación.</summary>
    public Guid? OperationKey { get; set; }

    public ScreeningOutcomeR ScreeningOutcome { get; set; } = ScreeningOutcomeR.Clear;

    public int? CustodyMovementId { get; set; }

    public int? ReleasedByUserId { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public long? ReceiptId { get; set; }

    public int? SourceOfFundsDeclarationId { get; set; }
}

/// <summary>
/// <c>RCD_Receipts</c> (H) (feature 015, T0110; FR-042, FR-044, FR-170, FR-172; data-model §3.3): el recibo. Toma el <c>PublicId</c> de su operación, se
/// numera después de los cerrojos y antes de los contratos (decisión del dueño del 2026-10-23) y no se edita: sólo su <see cref="Status"/> cambia por la
/// anulación. (nuevo)
/// </summary>
public class Receipt : AuditableEntityLong, IHechoInmutable
{
    public string Number { get; set; } = string.Empty;

    public int DocumentTypeId { get; set; }

    public ReceiptChannel Channel { get; set; } = ReceiptChannel.CashDesk;

    public int BranchId { get; set; }

    public int? CollectionPointId { get; set; }

    public int? CashBoxId { get; set; }

    public int? CashSessionId { get; set; }

    public int? CashierUserId { get; set; }

    public Guid? OwnBankAccountPublicId { get; set; }

    public int PayerPersonId { get; set; }

    public int? DeliveredByPersonId { get; set; }

    public DateOnly ValueDate { get; set; }

    public DateOnly ProposedAccountingDate { get; set; }

    /// <summary>El instante del registro (UTC; se muestra en hora de Colombia, FR-008).</summary>
    public DateTime RegisteredAt { get; set; }

    public decimal TotalApplied { get; set; }

    public decimal TotalMeans { get; set; }

    public decimal ChangeGiven { get; set; }

    [ColumnaMutable] public ReceiptState Status { get; set; } = ReceiptState.Valid;

    public int? SourceOfFundsDeclarationId { get; set; }

    public bool IsContingency { get; set; }

    public int? ContingencyBookId { get; set; }

    public int? ContingencyNumber { get; set; }

    public Guid? ContingencyImageAttachmentPublicId { get; set; }

    public Guid? BackdatedApprovalRequestPublicId { get; set; }

    public string? BackdatedReason { get; set; }

    public bool PayerIsThirdParty { get; set; }

    public string InputsHash { get; set; } = string.Empty;

    public Guid OperationKey { get; set; }

    public Guid? BankStatementLinePublicId { get; set; }

    public int? PaymasterRemittanceId { get; set; }

    public string VerificationCode { get; set; } = string.Empty;
}

/// <summary>
/// <c>RCD_ReceiptMeans</c> (H) (data-model §3.4): un medio del recibo con su valor, referencia, datáfono y lote, código de aprobación, cheque o bono. Nunca el
/// número de la tarjeta (<c>LosPagosNoGuardanElNumeroDeTarjeta</c>). El estado se castiga por cheque devuelto o reverso sin dinero. (nuevo)
/// </summary>
public class ReceiptMeans : AuditableEntityLong, IHechoInmutable
{
    public long ReceiptId { get; set; }

    public int Ordinal { get; set; }

    public int PaymentMeansId { get; set; }

    /// <summary>La clase del medio (los números de <c>ClaseDeMedioDeRecaudo</c> 1 a 9 y 99 son los de <see cref="PaymentMeansClass"/>).</summary>
    public PaymentMeansClass MeansClass { get; set; }

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public int? CardTerminalId { get; set; }

    public string? TerminalBatch { get; set; }

    public string? ApprovalCode { get; set; }

    public int? ReceivedCheckId { get; set; }

    public string? VoucherNumber { get; set; }

    [ColumnaMutable] public ReceiptMeansState Status { get; set; } = ReceiptMeansState.Active;
}

/// <summary>
/// <c>RCD_ReceiptApplications</c> (H) (data-model §3.5): una aplicación del recibo —un crédito, una cuenta o un concepto vario— con su modalidad, la elección
/// del deudor, la aceptación del aviso, el comprobante que devolvió el módulo y la firmeza. El estado cambia si el módulo la reversa o se anula. (nuevo)
/// </summary>
public class ReceiptApplication : AuditableEntityLong, IHechoInmutable
{
    public long ReceiptId { get; set; }

    public int Ordinal { get; set; }

    public ApplicationTarget Target { get; set; }

    public Guid? ProductPublicId { get; set; }

    /// <summary>El número del producto; en un crédito o una cuenta lo informa el contrato al aplicar (escritura única).</summary>
    [EscrituraUnica] public string? ProductNumber { get; set; }

    public int BeneficiaryPersonId { get; set; }

    /// <summary><c>ModalidadDePago</c> como número.</summary>
    public int? LoanModality { get; set; }

    /// <summary><c>ModalidadDeConsignacion</c> como número.</summary>
    public int? AccountModality { get; set; }

    /// <summary><c>EleccionDeAbono</c> como número (Ley 2032).</summary>
    public int? PrepaymentChoice { get; set; }

    public int? InstallmentNumber { get; set; }

    public string? InstallmentPublicIdsJson { get; set; }

    public bool AdvanceNoticeAccepted { get; set; }

    public int? MiscConceptId { get; set; }

    public string? CrossDocumentReference { get; set; }

    public Guid? InvoicePublicId { get; set; }

    public decimal Amount { get; set; }

    [EscrituraUnica] public Guid? ModuleVoucherPublicId { get; set; }

    [EscrituraUnica] public string? ModuleVoucherNumber { get; set; }

    [EscrituraUnica] public DateOnly? FinalityUntil { get; set; }

    [ColumnaMutable] public ApplicationState Status { get; set; } = ApplicationState.Applied;
}

/// <summary>
/// <c>RCD_ReceiptApplicationPortions</c> (H) (FR-188; data-model §3.6): la parte de una aplicación que cubre un medio; es el ítem que viaja al contrato con
/// su clave <c>REC:{recibo:N}:{aplicación:N}:{clase}</c> (UK). El estado y la clave del reverso cambian si se reversa. (nuevo)
/// </summary>
public class ReceiptApplicationPortion : AuditableEntityLong, IHechoInmutable
{
    public long ReceiptApplicationId { get; set; }

    public long ReceiptMeansId { get; set; }

    public PaymentMeansClass MeansClass { get; set; }

    public decimal Amount { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    [EscrituraUnica] public Guid? ModuleOperationPublicId { get; set; }

    [EscrituraUnica] public string? ModuleOperationNumber { get; set; }

    [ColumnaMutable] public PortionState Status { get; set; } = PortionState.Applied;

    [EscrituraUnica] public string? ReversalKey { get; set; }
}

/// <summary>
/// <c>RCD_SourceOfFundsDeclarations</c> (H) (FR-094; data-model §3.7): la declaración de origen de fondos de una operación de caja, con su soporte y la versión
/// del umbral que la exigió. (nuevo)
/// </summary>
public class SourceOfFundsDeclaration : AuditableEntity, IHechoInmutable
{
    public int PersonId { get; set; }

    public Guid ReceiptOperationPublicId { get; set; }

    public decimal CashAmount { get; set; }

    public string Origin { get; set; } = string.Empty;

    public Guid SupportAttachmentPublicId { get; set; }

    public DateTime DeclaredAt { get; set; }

    public Guid? ThresholdVersionPublicId { get; set; }

    public SourceOfFundsRequiredBy RequiredBy { get; set; }
}
