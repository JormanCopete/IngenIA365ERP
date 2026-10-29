using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Banks;

// Feature 015, US6 (T0192; FR-066 a FR-073, FR-171, FR-198, FR-199; data-model §5): lo que Recaudo guarda de los abonos del extracto de Tesorería. El extracto es de
// Tesorería (R-06): Recaudo guarda SU identificación por el PublicId de la línea, sin FK hacia TES_ (R-05). (nuevo)

/// <summary>
/// <c>RCD_BankItemIdentifications</c> (H; data-model §5.1): una identificación (total o parcial, por <see cref="Sequence"/>) de un abono del extracto: el recibo de canal
/// banco que generó, la partida por identificar, la clasificación a un concepto, el giro devuelto o el giro de una pagaduría. La suma de las de una línea nunca supera su
/// valor (<c>Receipts.BankItem.OverIdentified</c>). (nuevo)
/// </summary>
public class BankItemIdentification : AuditableEntity, IHechoInmutable
{
    public Guid BankStatementLinePublicId { get; set; }

    /// <summary>1, 2… las identificaciones de una misma línea.</summary>
    public int Sequence { get; set; }

    public Guid OwnBankAccountPublicId { get; set; }

    public DateOnly MovementDate { get; set; }

    public decimal Amount { get; set; }

    public BankItemOutcome Outcome { get; set; }

    public long? ReceiptId { get; set; }

    public int? UnidentifiedItemId { get; set; }

    public int? MiscConceptId { get; set; }

    /// <summary>La orden de pago del giro devuelto (FR-199).</summary>
    public Guid? PaymentOrderPublicId { get; set; }

    public int? PaymasterRemittanceId { get; set; }

    public int IdentifiedByUserId { get; set; }

    public DateTime IdentifiedAt { get; set; }

    /// <summary>La regla de identificación (R1).</summary>
    public int? RuleId { get; set; }

    /// <summary>La aprobación de la clasificación (nuevo).</summary>
    public Guid? ApprovalRequestPublicId { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// <c>RCD_UnidentifiedItems</c> (data-model §5.2; FR-070 a FR-073): un abono que nadie identificó, un pasivo con banco, cuenta, fecha, valor y referencia, sin persona.
/// <see cref="Balance"/> baja con cada resolución y <see cref="Status"/> sigue §19.6; el escalón de antigüedad sólo alerta: nunca cambia sola. (nuevo)
/// </summary>
public class UnidentifiedItem : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public Guid? BankStatementLinePublicId { get; set; }

    public Guid OwnBankAccountPublicId { get; set; }

    public int? BankId { get; set; }

    /// <summary>La oficina de la cuenta (alcance y alertas) (nuevo).</summary>
    public int BranchId { get; set; }

    public DateOnly MovementDate { get; set; }

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? DepositorName { get; set; }

    public string? DepositorTaxId { get; set; }

    public decimal Balance { get; set; }

    public UnidentifiedItemState Status { get; set; } = UnidentifiedItemState.Open;

    /// <summary>El último escalón de antigüedad alertado (30, 60, 90 por defecto; 0 sin alertar).</summary>
    public short AgingStep { get; set; }

    public UnidentifiedItemOrigin OriginKind { get; set; } = UnidentifiedItemOrigin.Statement;

    public DateOnly OpenedOn { get; set; }
}

/// <summary>
/// <c>RCD_UnidentifiedItemResolutions</c> (H; data-model §5.2): una resolución (total o parcial) de una partida: identificada con un recibo, clasificada a un concepto
/// (con aprobación), devuelta al consignante por orden de pago o llevada a ingreso por una decisión registrada con aprobación (FR-073). (nuevo)
/// </summary>
public class UnidentifiedItemResolution : AuditableEntity, IHechoInmutable
{
    public int UnidentifiedItemId { get; set; }

    public UnidentifiedItemResolutionKind Kind { get; set; }

    public decimal Amount { get; set; }

    /// <summary>El recibo, la orden de pago o la resolución misma.</summary>
    public Guid DocumentPublicId { get; set; }

    public string? DocumentNumber { get; set; }

    public int? MiscConceptId { get; set; }

    public Guid? ApprovalRequestPublicId { get; set; }

    public int ByUserId { get; set; }

    public DateTime At { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// <c>RCD_ProcessingRuns</c> (data-model §5.3; FR-171, R-17): una corrida no interactiva —la identificación de un abono, la aplicación de un giro de pagaduría, un archivo
/// de convenio (R2)—, a nombre de quien la confirma. Los conteos cambian con cada tanda. (nuevo)
/// </summary>
public class ProcessingRun : AuditableEntity
{
    public ProcessingRunKind Kind { get; set; }

    public Guid SourcePublicId { get; set; }

    public string SourceNumber { get; set; } = string.Empty;

    public int StartedByUserId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public int Lines { get; set; }

    public int Applied { get; set; }

    public int Rejected { get; set; }

    public decimal Total { get; set; }

    public string InputsHash { get; set; } = string.Empty;

    /// <summary>Cuántas tandas se procesaron (nuevo).</summary>
    public int Batches { get; set; }
}

/// <summary>
/// <c>RCD_ProcessingRunLines</c> (data-model §5.3): una línea de la corrida con su clave. Reprocesar repite la clave (el contrato no duplica). Lo pedido queda en
/// <see cref="RequestJson"/> para reprocesar igual (nuevo). (nuevo)
/// </summary>
public class ProcessingRunLine : AuditableEntity
{
    public int ProcessingRunId { get; set; }

    public string LineRef { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public ProcessingLineOutcome Outcome { get; set; } = ProcessingLineOutcome.Pending;

    public decimal Amount { get; set; }

    public string? Code { get; set; }

    public string? Message { get; set; }

    public long? ReceiptId { get; set; }

    public Guid? ResolutionPublicId { get; set; }

    public string RequestJson { get; set; } = "{}";

    public int Attempts { get; set; }

    public DateTime? LastAttemptAt { get; set; }
}
