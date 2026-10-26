using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Sessions;

/// <summary>
/// <c>RCD_CashMovements</c> (H) (feature 015, T0089; FR-029, FR-030, FR-182; data-model §2.4): un movimiento de la caja que no es un recibo —la base de
/// apertura, abastecer desde la bóveda, retirar a la bóveda, trasladar entre cajas, reclasificar un medio, retener dinero en custodia y liberarlo—, con su
/// número del tipo <c>MC</c>. La reclasificación <b>no toca el recibo</b>: mueve lo esperado de un medio a otro y entra al informe de excepciones
/// (US2-7). (nuevo)
/// </summary>
public class CashMovement : AuditableEntity, IHechoInmutable, IngenIA365ERP.Domain.Common.IDocumentoNumerado
{
    public string Number { get; set; } = string.Empty;

    public int DocumentTypeId { get; set; }

    public CashMovementKindR Kind { get; set; }

    public int CashSessionId { get; set; }

    /// <summary>La otra sesión de un traslado entre cajas.</summary>
    public int? CounterpartSessionId { get; set; }

    public int? VaultId { get; set; }

    public int? FromMeansId { get; set; }

    public int? ToMeansId { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Obligatorio en la reclasificación desde efectivo (FR-030).</summary>
    public Guid? SupportAttachmentPublicId { get; set; }

    /// <summary>El recibo de una reclasificación (<c>RCD_Receipts.Id</c>; la FK llega con el recibo).</summary>
    public long? ReceiptId { get; set; }

    public Guid? ApprovalRequestPublicId { get; set; }

    /// <summary>El segundo custodio con <c>Caja.DobleCustodia</c> (FR-182).</summary>
    public int? SecondCustodianUserId { get; set; }

    public DateTime MovedAt { get; set; }

    public int RegisteredByUserId { get; set; }
}

/// <summary><c>RCD_CashMovementDenominations</c> (H): el efectivo de un movimiento por denominación. (nuevo)</summary>
public class CashMovementDenomination : AuditableEntity, IHechoInmutable
{
    public int CashMovementId { get; set; }

    public int CashDenominationId { get; set; }

    public int Quantity { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// <c>RCD_CashSessionAdjustments</c> (H) (feature 015, T0089; FR-033, US2-5; data-model §2.5): el ajuste de una sesión cerrada, aprobado por otra persona y
/// con soporte, con su número del tipo <c>AS</c> y su hecho <c>AjusteDeSesionRegistrado</c>. La sesión cerrada no se reabre. (nuevo)
/// </summary>
public class CashSessionAdjustment : AuditableEntity, IHechoInmutable, IngenIA365ERP.Domain.Common.IDocumentoNumerado
{
    public string Number { get; set; } = string.Empty;

    public int CashSessionId { get; set; }

    public SessionAdjustmentKind Kind { get; set; }

    public int PaymentMeansId { get; set; }

    /// <summary>Con signo: positivo sube lo contado (aparece dinero), negativo lo baja.</summary>
    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid ApprovalRequestPublicId { get; set; }

    public int ApprovedByUserId { get; set; }

    public Guid? SupportAttachmentPublicId { get; set; }

    public DateTime AdjustedAt { get; set; }

    public int RegisteredByUserId { get; set; }
}

/// <summary>
/// <c>RCD_DayCloses</c> (H) (feature 015, T0089; FR-033, FR-156; data-model §2.7): el cierre del día de un punto con su total de sesiones, recibos y
/// anulaciones; el informe lleva código de verificación. (nuevo)
/// </summary>
public class DayClose : AuditableEntity, IHechoInmutable
{
    public int CollectionPointDayId { get; set; }

    public int SessionCount { get; set; }

    public int ReceiptCount { get; set; }

    public int VoidingCount { get; set; }

    public decimal Expected { get; set; }

    public decimal Counted { get; set; }

    public decimal Difference { get; set; }

    public DateTime ClosedAt { get; set; }

    public int ClosedByUserId { get; set; }

    /// <summary>La suplencia con la que lo cerró un suplente (FR-184).</summary>
    public int? SupervisorDelegationId { get; set; }

    public string VerificationCode { get; set; } = string.Empty;
}

/// <summary><c>RCD_DayCloseLines</c> (H): los totales del día por medio. (nuevo)</summary>
public class DayCloseLine : AuditableEntity, IHechoInmutable
{
    public int DayCloseId { get; set; }

    public int PaymentMeansId { get; set; }

    public decimal Expected { get; set; }

    public decimal Counted { get; set; }

    public decimal Difference { get; set; }
}

/// <summary>
/// <c>RCD_DailySquares</c> (feature 015, T0089; FR-036, FR-185; data-model §2.8): el cuadre diario del efectivo de una oficina —cajas, bóveda y tránsito—
/// contra el saldo contable más lo emitido y pendiente de lote. Lo produce la tarea <c>recaudo.cuadre-diario</c>; sólo informa y alerta. Cambian la
/// explicación y el estado. (nuevo)
/// </summary>
public class DailySquare : AuditableEntity
{
    public int BranchId { get; set; }

    public DateOnly BusinessDate { get; set; }

    public decimal CashInBoxes { get; set; }

    public decimal CashInVault { get; set; }

    public decimal CashInTransit { get; set; }

    /// <summary>Nulo si Contabilidad no respondió (sin contabilidad iniciada).</summary>
    public decimal? LedgerBalance { get; set; }

    public decimal EmittedPendingBatch { get; set; }

    public decimal Difference { get; set; }

    public string? Explanation { get; set; }

    public int? ExplainedByUserId { get; set; }

    public DailySquareStatus Status { get; set; }

    /// <summary>La verificación de la bóveda: el saldo por denominación coincide con la suma de sus movimientos.</summary>
    public bool VaultBalanceMatches { get; set; } = true;
}

/// <summary>
/// <c>RCD_CashierReceivables</c> (feature 015, T0089; FR-183; data-model §4.4): el faltante a cargo del cajero. Descontarlo por Nómina exige la autorización
/// escrita del caso adjunta. Cambian el saldo, el estado y la autorización. (nuevo)
/// </summary>
public class CashierReceivable : AuditableEntity
{
    public int CashierPersonId { get; set; }

    public int CashCountLineId { get; set; }

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public Guid? PayrollDeductionAuthorizationAttachmentPublicId { get; set; }

    public CashierReceivableState Status { get; set; } = CashierReceivableState.Open;
}

/// <summary>
/// <c>RCD_CashSurpluses</c> (feature 015, T0089; FR-183; data-model §4.4): el sobrante de un arqueo, pasivo hasta <see cref="ToIncomeFrom"/> (conteo +
/// <c>Caja.Sobrantes.DiasAntesDeIngreso</c>); después pasa a ingreso con aprobación (<c>SobranteLlevadoAIngreso</c>) o se devuelve. (nuevo)
/// </summary>
public class CashSurplus : AuditableEntity
{
    public int CashCountLineId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Nulo mientras <c>Caja.Sobrantes.DiasAntesDeIngreso</c> no tenga valor: el sobrante no pasa a ingreso (P8).</summary>
    public DateOnly? ToIncomeFrom { get; set; }

    public CashSurplusState Status { get; set; } = CashSurplusState.Liability;

    public Guid? ApprovalRequestPublicId { get; set; }

    public DateTime? MovedToIncomeAt { get; set; }
}
