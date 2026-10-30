using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Payments;

/// <summary>
/// <c>TES_PaymentResponses</c> (feature 015, US11, T0157; FR-118, FR-212; data-model §10.6): la respuesta del banco a un archivo de pago, leída con su formato de
/// vuelta (<c>TreasuryPaymentResponse</c>), tal como llegó (adjunto <c>TreasuryPaymentResponse</c> con su SHA-256). Cada línea se coteja contra la huella de la línea
/// del archivo generado; la que discrepa no se informa hasta que una persona la revise. (nuevo)
/// </summary>
public class PaymentResponse : AuditableEntity
{
    public int PaymentFileId { get; set; }

    public PaymentResponseSource Source { get; set; }

    /// <summary>El formato con que se leyó (nulo en la captura manual).</summary>
    public int? BankFileFormatId { get; set; }

    public Guid? ResponseAttachmentPublicId { get; set; }

    public string? FileName { get; set; }

    public string? Sha256 { get; set; }

    public int LoadedByUserId { get; set; }

    public DateTime LoadedAt { get; set; }

    public int LineCount { get; set; }

    /// <summary>Las líneas que discreparon del archivo generado (nuevo).</summary>
    public int DiscrepancyCount { get; set; }

    /// <summary>Las líneas que se informaron al cargar (nuevo).</summary>
    public int InformedCount { get; set; }
}

/// <summary>
/// <c>TES_PaymentResponseLines</c>: una línea de la respuesta, con la línea del archivo generado que casa (por número de línea, o por documento y valor), lo que el
/// banco dice (pagada o rechazada, causal, fecha, referencia) y si discrepa en documento, cuenta o valor (FR-212). Una línea con discrepancia queda sin informar
/// hasta <c>ReviewPaymentResponseLine</c>: la persona la acepta (se registra el resultado) o la descarta (la orden sigue enviada). (nuevo)
/// </summary>
public class PaymentResponseLine : AuditableEntity
{
    public int PaymentResponseId { get; set; }

    public int LineNumber { get; set; }

    public int? PaymentFileLineId { get; set; }

    public PaymentResponseOutcome Outcome { get; set; }

    public string? BankCause { get; set; }

    public DateOnly BankDate { get; set; }

    public decimal Amount { get; set; }

    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>El documento del beneficiario que trae la respuesta (nuevo).</summary>
    public string BeneficiaryTaxId { get; set; } = string.Empty;

    public string? BankReference { get; set; }

    public bool Discrepancy { get; set; }

    /// <summary>Qué discrepó (documento, cuenta, valor o sin línea en el archivo) (nuevo).</summary>
    public string? DiscrepancyDetail { get; set; }

    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>La decisión de la revisión: <c>true</c> la informa, <c>false</c> la descarta (nuevo).</summary>
    public bool? ReviewAccepted { get; set; }

    public string? ReviewNote { get; set; }

    public int? DisbursementResultId { get; set; }
}

/// <summary>
/// <c>TES_DisbursementResults</c> (H; feature 015, US11, T0157; R-18; FR-118, FR-215; data-model §10.7): el resultado del giro de una orden —girada, rechazada,
/// cheque no cobrado o anulada—, registrado siempre por una persona (o, el «anulada», a nombre de la persona del origen que anuló) y emitido como
/// <c>ResultadoDelGiro</c> v1 a su origen (<see cref="EmittedMessagePublicId"/>; nulo en las órdenes de Recaudo, que se informan por su puerto). UK
/// <c>(PaymentOrderId, Outcome)</c>: el mismo resultado dos veces no cambia nada. No se edita ni se borra. (nuevo)
/// </summary>
public class DisbursementResult : AuditableEntity, IHechoInmutable
{
    public int PaymentOrderId { get; set; }

    public DisbursementOutcome Outcome { get; set; }

    public DateOnly OccurredOn { get; set; }

    public decimal Amount { get; set; }

    public string TreasuryReference { get; set; } = string.Empty;

    public string? CheckNumber { get; set; }

    public string? Reason { get; set; }

    /// <summary>De dónde salió (respuesta del banco, extracto confirmado, registro manual) (nuevo).</summary>
    public PaymentResponseSource Source { get; set; }

    /// <summary>La persona de Tesorería que lo registró; nulo en el «anulada», que nombra a la persona del origen.</summary>
    public int? RecordedByUserId { get; set; }

    public Guid? RecordedByCentralUserId { get; set; }

    public string RecordedByName { get; set; } = string.Empty;

    public DateTime RecordedAt { get; set; }

    public Guid? SupportAttachmentPublicId { get; set; }

    public int? PaymentResponseLineId { get; set; }

    public Guid? StatementLinePublicId { get; set; }

    public Guid? EmittedMessagePublicId { get; set; }
}
