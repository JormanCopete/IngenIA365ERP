using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Paymasters;

// Feature 015, US7 (T0205; FR-075 a FR-081, FR-171, FR-201; data-model §6.2 a §6.5): las pagadurías y la libranza en Recaudo. El archivo de descuentos es de Cartera: Recaudo lo
// recibe por DescuentosPorPagaduria (v1 o v2), confirma su envío, carga la respuesta de la pagaduría, devuelve el resultado y aplica el giro línea por línea. Lo de Cartera se
// guarda por su PublicId, sin FK hacia LND_ (R-05). (nuevo)

/// <summary>
/// <c>RCD_PayrollDeductionFiles</c> (data-model §6.2): una versión del archivo de descuentos de una pagaduría que llegó de Cartera, con su mensaje (UK), sus totales y su estado;
/// una versión que reemplaza a otra no enviada la deja <c>Superseded</c>. (nuevo)
/// </summary>
public class PayrollDeductionFile : AuditableEntity
{
    public Guid FileMessagePublicId { get; set; }

    public short MessageVersion { get; set; }

    public int PaymasterPersonId { get; set; }

    public string AgreementCode { get; set; } = string.Empty;

    public int AgreementVersion { get; set; }

    public string? FileGroupCode { get; set; }

    public int PeriodYear { get; set; }

    public int PeriodMonth { get; set; }

    public int? SubPeriod { get; set; }

    public int FileVersion { get; set; }

    public Guid FilePublicId { get; set; }

    /// <summary>El número del archivo en Cartera (el del origen del mensaje) (nuevo).</summary>
    public string FileNumber { get; set; } = string.Empty;

    public Guid FileAttachmentPublicId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileSha256 { get; set; } = string.Empty;

    public string FormatCode { get; set; } = string.Empty;

    public DateOnly SendBy { get; set; }

    public int LineCount { get; set; }

    public decimal Amount { get; set; }

    public decimal PastDueAmount { get; set; }

    public Guid? SupersedesFileMessagePublicId { get; set; }

    public PayrollDeductionFileState Status { get; set; } = PayrollDeductionFileState.Received;

    /// <summary>La oficina de la pagaduría para el alcance y las alertas: la sucursal del mensaje (nuevo).</summary>
    public int BranchId { get; set; }
}

/// <summary>
/// <c>RCD_PayrollDeductionFileLines</c> (H; data-model §6.2): una línea estructurada del archivo —un crédito o la cuota de una cuenta—. El resultado de la pagaduría se escribe una
/// vez cuando Cartera acepta la respuesta; lo girado (<see cref="RemittedAmount"/>) crece con cada giro aplicado. (nuevo)
/// </summary>
public class PayrollDeductionFileLine : AuditableEntity, IHechoInmutable
{
    public int PayrollDeductionFileId { get; set; }

    public Guid DeductionFileLinePublicId { get; set; }

    public PayrollDeductionLineKind Kind { get; set; }

    public Guid ProductPublicId { get; set; }

    public string ProductNumber { get; set; } = string.Empty;

    public Guid? InstallmentPublicId { get; set; }

    public int? InstallmentNumber { get; set; }

    public int PersonId { get; set; }

    public string PayerEmployeeId { get; set; } = string.Empty;

    public string? PaymasterConceptCode { get; set; }

    public string NoveltyKind { get; set; } = string.Empty;

    public DateOnly DueDate { get; set; }

    public decimal Amount { get; set; }

    public decimal PastDueAmount { get; set; }

    [EscrituraUnica] public PaymasterDeductionOutcome? DeductionOutcome { get; set; }

    [EscrituraUnica] public decimal? DeductedAmount { get; set; }

    [EscrituraUnica] public DateOnly? DeductionDate { get; set; }

    [ColumnaMutable] public decimal RemittedAmount { get; set; }
}

/// <summary><c>RCD_PayrollFileSendings</c> (H; data-model §6.3): la confirmación del envío a la pagaduría, una por versión del archivo. Repetirla no cambia nada. (nuevo)</summary>
public class PayrollFileSending : AuditableEntity, IHechoInmutable
{
    public int PayrollDeductionFileId { get; set; }

    public PayrollFileChannel Channel { get; set; }

    public DateTime SentAt { get; set; }

    public string? Reference { get; set; }

    public int SentByUserId { get; set; }

    public Guid EmittedMessagePublicId { get; set; }
}

/// <summary>
/// <c>RCD_PaymasterResponses</c> (data-model §6.4): una versión de la respuesta de la pagaduría —por su formato, la plantilla genérica o a mano— con su estado: se previsualiza, se
/// emite a Cartera (<c>ResultadoDeDescuentos</c> v1 o v2 según el archivo) y Cartera la acepta o la rechaza; una rechazada se corrige con otra versión. (nuevo)
/// </summary>
public class PaymasterResponse : AuditableEntity
{
    public int PayrollDeductionFileId { get; set; }

    public int Version { get; set; }

    public PaymasterResponseSource Source { get; set; }

    public int? BankFileFormatId { get; set; }

    public Guid? ResponseAttachmentPublicId { get; set; }

    public string? ResponseSha256 { get; set; }

    public string? FileName { get; set; }

    public int LoadedByUserId { get; set; }

    public DateTime LoadedAt { get; set; }

    public PaymasterResponseState Status { get; set; } = PaymasterResponseState.Previewed;

    public Guid? EmittedMessagePublicId { get; set; }

    public short? EmittedMessageVersion { get; set; }

    public string? RejectionCode { get; set; }

    public string? RejectionDataJson { get; set; }

    public int LineCount { get; set; }

    public decimal Expected { get; set; }

    public decimal Deducted { get; set; }

    public int Unmatched { get; set; }
}

/// <summary>
/// <c>RCD_PaymasterResponseLines</c> (data-model §6.4): una fila de la respuesta —por línea de Cartera (v1) o por empleado y código de la pagaduría (v2)—; <see cref="Unmatched"/>
/// es la fila sin equivalencia, que queda como «no aplicado». (nuevo)
/// </summary>
public class PaymasterResponseLine : AuditableEntity
{
    public int PaymasterResponseId { get; set; }

    public int LineNumber { get; set; }

    public Guid? DeductionFileLinePublicId { get; set; }

    public string? PayerEmployeeId { get; set; }

    public string? PaymasterConceptCode { get; set; }

    public string? ProductNumber { get; set; }

    public decimal Expected { get; set; }

    public decimal Deducted { get; set; }

    public DateOnly? DeductionDate { get; set; }

    public PaymasterDeductionOutcome Outcome { get; set; }

    public string? PaymasterResultCode { get; set; }

    public string? Reason { get; set; }

    public bool Unmatched { get; set; }
}

/// <summary>
/// <c>RCD_PaymasterRemittances</c> (data-model §6.5): el dinero que giró la pagaduría —identificado en el extracto o recibido en caja— y las líneas descontadas que cubre en su
/// orden; el excedente queda como saldo a favor de la pagaduría. (nuevo)
/// </summary>
public class PaymasterRemittance : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public int PaymasterPersonId { get; set; }

    public PaymasterRemittanceSource Source { get; set; }

    public Guid? BankStatementLinePublicId { get; set; }

    public long? SourceReceiptId { get; set; }

    public Guid? OwnBankAccountPublicId { get; set; }

    public int BranchId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly RemittedOn { get; set; }

    /// <summary>Los archivos que cubre, en orden (sus <c>FilePublicId</c>).</summary>
    public string PeriodsJson { get; set; } = "[]";

    public PaymasterRemittanceState Status { get; set; } = PaymasterRemittanceState.Registered;

    public PaymasterAllocationOrder AllocationOrder { get; set; } = PaymasterAllocationOrder.AgreementPriority;

    public int? SurplusCreditBalanceId { get; set; }

    public int RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }
}

/// <summary>
/// <c>RCD_PaymasterRemittanceLines</c> (data-model §6.5): una línea descontada que cubre el giro, con su clave <c>REC:{giro:N}:{línea:N}</c> y su resultado; la rechazada por el
/// contrato queda como saldo de la pagaduría (<see cref="CreditBalanceId"/>, US7-6). (nuevo)
/// </summary>
public class PaymasterRemittanceLine : AuditableEntity
{
    public int PaymasterRemittanceId { get; set; }

    public int Order { get; set; }

    public int PayrollDeductionFileLineId { get; set; }

    public Guid DeductionFileLinePublicId { get; set; }

    public decimal Amount { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public PaymasterRemittanceLineOutcome Outcome { get; set; } = PaymasterRemittanceLineOutcome.Pending;

    public long? ReceiptId { get; set; }

    public string? RejectionCode { get; set; }

    public string? RejectionMessage { get; set; }

    public int? CreditBalanceId { get; set; }
}
