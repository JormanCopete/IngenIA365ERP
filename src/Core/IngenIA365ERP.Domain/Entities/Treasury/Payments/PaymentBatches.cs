using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Payments;

/// <summary>
/// <c>TES_PaymentBatches</c> (feature 015, US10, T0135; FR-110, FR-111, FR-207, FR-213; data-model §10.3, §19.10): un lote de pago por cuenta de origen y
/// forma, con su formato sellado al preparar, sus totales y su GMF estimado; lo aprueba otra persona por monto (aprobación diferida) y sale por archivo, por el
/// portal del banco o en cheques. No es un hecho: <see cref="Status"/> recorre su ciclo y la huella <see cref="InputsHash"/> es la que el aprobador confirma.
/// Nunca se borra. (nuevo)
/// </summary>
public class PaymentBatch : AuditableEntity, IngenIA365ERP.Domain.Common.IDocumentoNumerado
{
    public string Number { get; set; } = string.Empty;

    /// <summary>La cuenta de origen (<c>TES_BankAccounts.Id</c>); nunca del fondo de liquidez sin un uso aprobado.</summary>
    public int BankAccountId { get; set; }

    public PaymentBatchForm Form { get; set; }

    /// <summary>El formato vigente al preparar (sellado); nulo en el portal del banco y en cheques.</summary>
    public int? BankFileFormatId { get; set; }

    public int PreparedByUserId { get; set; }

    public DateTime PreparedAt { get; set; }

    /// <summary>La fecha de pago (nuevo): la del archivo y la del débito esperado.</summary>
    public DateOnly PayDate { get; set; }

    public int OrderCount { get; set; }

    public decimal Total { get; set; }

    public decimal GmfEstimate { get; set; }

    public Guid? ApprovalRequestPublicId { get; set; }

    /// <summary>El uso del fondo de liquidez que lo autoriza, si la cuenta es del fondo (FR-207).</summary>
    public int? LiquidityFundUseId { get; set; }

    public PaymentBatchState Status { get; set; } = PaymentBatchState.Draft;

    public string InputsHash { get; set; } = string.Empty;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    /// <summary>Marcado enviado (nuevo): el archivo o el portal; quién lo cargó y quién lo autorizó en el banco (FR-112).</summary>
    public DateTime? SentAt { get; set; }

    public int? SentByUserId { get; set; }

    public string? UploadedAtBankBy { get; set; }

    public string? AuthorizedAtBankBy { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// <c>TES_PaymentBatchItems</c> (data-model §10.4; FR-116, FR-210): una orden en un lote con su valor (el neto), sus marcas antifraude y la revisión expresa
/// de quien no preparó. Una orden está en a lo sumo un ítem vivo de un lote no anulado ni rechazado por el banco (índice único filtrado por
/// <see cref="IsLive"/>, nuevo). (nuevo)
/// </summary>
public class PaymentBatchItem : AuditableEntity
{
    public int PaymentBatchId { get; set; }

    public int PaymentOrderId { get; set; }

    public decimal Amount { get; set; }

    public decimal GmfEstimate { get; set; }

    public PaymentBatchItemFlags Flags { get; set; }

    /// <summary>Obligatorio con <see cref="PaymentBatchItemFlags.RecentAccountChange"/> (US26-1): otra persona que quien preparó.</summary>
    public int? ExplicitlyReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public PaymentBatchItemState Status { get; set; } = PaymentBatchItemState.Included;

    /// <summary>
    /// Vivo (nuevo): incluido o pagado en un lote que no está anulado ni rechazado por el banco. Lo baja la salida del ítem o el fin del lote; el índice único
    /// <c>(PaymentOrderId)</c> filtrado por él cumple FR-116 en los dos motores sin mirar otra tabla.
    /// </summary>
    public bool IsLive { get; set; } = true;
}

/// <summary>
/// <c>TES_PaymentFiles</c> (H) (feature 015, US10, T0135; FR-112, FR-212; data-model §10.5): el archivo de pago de un lote tal como se generó, inmutable, con su
/// huella SHA-256 y el adjunto no borrable (<c>TreasuryPaymentFile</c>) subido <b>antes</b> de guardar la fila. Lo que pasa después (la primera descarga, el
/// envío, la anulación, el rechazo técnico del banco) son columnas de escritura única. (nuevo)
/// </summary>
public class PaymentFile : AuditableEntity, IHechoInmutable
{
    public int PaymentBatchId { get; set; }

    public int Version { get; set; }

    public int BankFileFormatId { get; set; }

    public Guid FileAttachmentPublicId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int LineCount { get; set; }

    public decimal Total { get; set; }

    public int GeneratedByUserId { get; set; }

    public DateTime GeneratedAt { get; set; }

    [EscrituraUnica] public int? DownloadedByUserId { get; set; }

    [EscrituraUnica] public DateTime? DownloadedAt { get; set; }

    [EscrituraUnica] public DateTime? SentAt { get; set; }

    [EscrituraUnica] public int? SentByUserId { get; set; }

    [EscrituraUnica] public string? UploadedAtBankBy { get; set; }

    [EscrituraUnica] public string? AuthorizedAtBankBy { get; set; }

    [EscrituraUnica] public DateTime? VoidedAt { get; set; }

    [EscrituraUnica] public string? VoidReason { get; set; }

    [EscrituraUnica] public DateTime? RejectedByBankAt { get; set; }

    [EscrituraUnica] public string? RejectedByBankReason { get; set; }

    public bool Vigente => VoidedAt is null && RejectedByBankAt is null;
}

/// <summary>
/// <c>TES_PaymentFileLines</c> (H) (data-model §10.5): una línea del archivo con lo que se coteja contra la respuesta del banco (documento, banco, cuenta, valor)
/// y su huella <see cref="LineSha256"/> (FR-212). (nuevo)
/// </summary>
public class PaymentFileLine : AuditableEntity, IHechoInmutable
{
    public int PaymentFileId { get; set; }

    public int PaymentBatchItemId { get; set; }

    public int LineNumber { get; set; }

    public string BeneficiaryTaxId { get; set; } = string.Empty;

    public string BankCode { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string LineSha256 { get; set; } = string.Empty;

    /// <summary>El texto de la línea tal como salió (nuevo).</summary>
    public string RecordText { get; set; } = string.Empty;
}
