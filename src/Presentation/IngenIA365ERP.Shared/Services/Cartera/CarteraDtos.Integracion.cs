namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US6 (T0601; contracts/api.md §7.3, §16.2): lo que llega a Cartera —las ventas a crédito y sus ajustes de Comercial, los resultados del
// giro de Tesorería— y las reservas de número de las ventas. Espejo de los DTOs de la API; los enums llegan como texto.

/// <summary>Una evaluación de un mensaje recibido.</summary>
public sealed record EvaluacionDeMensajeDto(int Number, string Outcome, string? Code, string? Reason, DateTime At, bool Retryable, string? CreditLineCode,
    string ChecksJson, string? PrevalidationOutcome, Guid? ResponseMessagePublicId);

/// <summary>La respuesta que Cartera envió a Comercial (<c>ObligacionCreada</c> u <c>ObligacionRechazada</c>).</summary>
public sealed record RespuestaAComercialDto(string Type, Guid MessagePublicId);

/// <summary>Un recibo de lo que llegó a Cartera, con sus evaluaciones y la respuesta enviada.</summary>
public sealed record MensajeRecibidoDto(
    Guid MessagePublicId,
    string Type,
    int Version,
    string OriginModule,
    string? OriginNumber,
    DateOnly OperationDate,
    DateTime ReceivedAt,
    DateTime LastProcessedAt,
    string Status,
    ReferenciaDeCreditoDto? Loan,
    string ValidationStatus,
    string? ValidationReason,
    DateTime? NotRetryableAt,
    string? NotRetryableReason,
    IReadOnlyList<EvaluacionDeMensajeDto> Evaluations,
    RespuestaAComercialDto? Response);

/// <summary>El recibo con el contenido del mensaje.</summary>
public sealed record MensajeRecibidoDetalleDto(MensajeRecibidoDto Receipt, string? PayloadJson);

/// <summary>Lo que respondió marcar un recibo no reprocesable.</summary>
public sealed record MensajeMarcadoDto(string Status, DateTime NotRetryableAt, RespuestaAComercialDto Response);

/// <summary>El pago de Comercial que pidió la reserva.</summary>
public sealed record PagoDeLaReservaDto(string Module, Guid PaymentPublicId, Guid SalesDocumentPublicId);

/// <summary>Una reserva de número (§7.3): número, estado, valor, persona, pago, vencimiento y el crédito que lo usó.</summary>
public sealed record ReservaDeNumeroDto(
    Guid PublicId,
    string LoanNumber,
    string Status,
    decimal Amount,
    ReferenciaDeCreditoDto Person,
    PagoDeLaReservaDto PaymentReference,
    DateTime ReservedAt,
    DateOnly ExpiresAt,
    Guid? UsedByLoanPublicId,
    DateTime? ReleasedAt,
    string? ReleaseReason,
    string? ConsultationEvidence);
