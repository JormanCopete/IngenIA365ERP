using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Operations;

/// <summary>
/// <c>RCD_CashDisbursements</c> (H; feature 015, R1, T0371; FR-051 a FR-056, FR-061, FR-191; data-model §8.1): el <b>egreso de caja</b>, el dinero que la caja entrega
/// en efectivo en una sola operación con la autorización del módulo: el retiro de ahorro y la cancelación con entrega (Captaciones), el saldo a favor de Cartera, el
/// saldo a favor de un pagador de Recaudo y el egreso vario por concepto. Un documento numerado con su código de verificación, la clave de la operación y quien recibe.
/// Sólo <see cref="Status"/> cambia (a <c>Voided</c>, cuando el dinero vuelve). (nuevo)
/// </summary>
public class CashDisbursement : AuditableEntity, IHechoInmutable
{
    public string Number { get; set; } = string.Empty;

    public int DocumentTypeId { get; set; }

    public CashDisbursementKind Kind { get; set; }

    public int CashSessionId { get; set; }

    public int CashBoxId { get; set; }

    public int CollectionPointId { get; set; }

    public int BranchId { get; set; }

    public int CashierUserId { get; set; }

    /// <summary>El titular a cuyo favor sale el dinero.</summary>
    public int BeneficiaryPersonId { get; set; }

    /// <summary>Quien recibe el dinero en ventanilla, si no es el titular (FR-191); se cruza con las listas.</summary>
    public int? ReceivedByPersonId { get; set; }

    /// <summary>La cuenta, la obligación o el saldo a favor de donde sale (por su <c>PublicId</c>; los de otro módulo sin FK).</summary>
    public Guid? ProductPublicId { get; set; }

    public string? ProductNumber { get; set; }

    /// <summary>La naturaleza del producto para el puente del módulo (<c>Deposit</c>, <c>Contribution</c>, <c>Loan</c>), la misma del espejo al anular.</summary>
    public string? ProductNature { get; set; }

    /// <summary>El saldo a favor de pagador de Recaudo que se devuelve.</summary>
    public int? PayerCreditBalanceId { get; set; }

    /// <summary>El concepto de egreso vario.</summary>
    public int? MiscConceptId { get; set; }

    /// <summary>El comprobante que devolvió el módulo (la autorización de Captaciones o la resolución de Cartera).</summary>
    public Guid? ModuleAuthorizationPublicId { get; set; }

    public string? ModuleAuthorizationNumber { get; set; }

    public decimal Amount { get; set; }

    /// <summary>El GMF del egreso: el que liquidó Captaciones en el retiro (<see cref="GmfByOrigin"/>) o el de Recaudo por <c>Gmf.*</c>.</summary>
    public decimal? GmfAmount { get; set; }

    /// <summary>¿El GMF lo liquidó el módulo de origen? Entonces Recaudo no lo causa otra vez.</summary>
    public bool GmfByOrigin { get; set; }

    /// <summary>Los firmantes verificados (personas), en JSON.</summary>
    public string? SignersJson { get; set; }

    public Guid? SignatureAttachmentPublicId { get; set; }

    public Guid? ApprovalRequestPublicId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid OperationKey { get; set; }

    public DateOnly OperationDate { get; set; }

    public DateTime RegisteredAt { get; set; }

    public string? Reason { get; set; }

    public string VerificationCode { get; set; } = string.Empty;

    [ColumnaMutable] public CashDisbursementStatus Status { get; set; } = CashDisbursementStatus.Delivered;
}

/// <summary>
/// <c>RCD_CashDisbursementVoidings</c> (H; feature 015, R1, T0371; FR-061): la anulación de un egreso, sólo si el dinero vuelve a la caja: reversa el retiro o la resolución
/// por su contrato y reingresa el dinero en la sesión abierta, con la aprobación de otra persona. Una por egreso (UK). (nuevo)
/// </summary>
public class CashDisbursementVoiding : AuditableEntity, IHechoInmutable
{
    public int CashDisbursementId { get; set; }

    public string Number { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    /// <summary>La sesión abierta donde el dinero vuelve.</summary>
    public int CashSessionId { get; set; }

    public int RequestedByUserId { get; set; }

    public int? ApprovedByUserId { get; set; }

    public Guid? ApprovalRequestPublicId { get; set; }

    /// <summary>El reverso que hizo el módulo (Captaciones o Cartera), si lo hubo.</summary>
    public Guid? ModuleReversalPublicId { get; set; }

    public string? ModuleReversalNumber { get; set; }

    public DateTime VoidedAt { get; set; }

    public string VerificationCode { get; set; } = string.Empty;
}
