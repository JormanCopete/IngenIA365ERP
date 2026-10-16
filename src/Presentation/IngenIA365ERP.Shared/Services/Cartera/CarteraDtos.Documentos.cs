using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US13 (T0791): los espejos de contracts/api.md §15 (documentos al deudor y verificación). Clases, estados y eventos viajan como texto
// (su nombre en el dominio); la pantalla los traduce con TextosDeDocumentos. Sólo PublicId hacia afuera. (nuevo)

/// <summary>El titular con la copia de su identificación tal como se emitió.</summary>
public sealed record TitularDeDocumentoDto(Guid PersonPublicId, string FullName, string DocumentType, string DocumentNumber);

/// <summary>Un crédito que cubre el documento y el saldo que mostró.</summary>
public sealed record CreditoDeDocumentoDto(Guid LoanPublicId, string Number, decimal? Balance);

/// <summary><c>IssuedDocumentDto</c>: el documento con su estado calculado (<c>Valid</c>, <c>SubjectToFinality</c>, <c>Voided</c>).</summary>
public sealed record DocumentoEmitidoDto(
    Guid PublicId,
    string Kind,
    string Number,
    TitularDeDocumentoDto Holder,
    IReadOnlyList<CreditoDeDocumentoDto> Loans,
    DateOnly? CutoffDate,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    short? Year,
    DateTime IssuedAt,
    string IssuedBy,
    string Status,
    DateOnly? FinalityUntil,
    bool? CoversCoDebts,
    string VerificationCode,
    string DeliveryMode,
    bool IsTestEnvironment);

/// <summary>Un evento del documento (<c>Issued</c>, <c>Delivered</c>, <c>Voided</c>) con quién, el motivo y la operación causa.</summary>
public sealed record EventoDeDocumentoDto(string Kind, DateTime At, string? By, string? Reason, string? CauseOperation, Guid? DebtorMessagePublicId);

/// <summary>El detalle: el documento, lo que muestra la verificación, sus eventos y la nota de ventas fuera de Cartera.</summary>
public sealed record DetalleDeDocumentoDto(DocumentoEmitidoDto Document, JsonElement EssentialData, IReadOnlyList<EventoDeDocumentoDto> Events,
    string? OutsideLendingNote);

/// <summary>El período de un extracto (K5).</summary>
public sealed record PeriodoDeDocumentoRequest(DateOnly From, DateOnly To);

/// <summary>
/// El pedido de emisión: clase, titular, crédito, fecha de corte del certificado de saldo, período del extracto o año del certificado tributario (K5)
/// y la entrega (<c>Download</c> o <c>Email</c>).
/// </summary>
public sealed record EmitirDocumentoRequest(
    string Kind,
    Guid PersonPublicId,
    Guid? LoanPublicId,
    DateOnly? CutoffDate,
    PeriodoDeDocumentoRequest? Period,
    short? Year,
    string Delivery);

/// <summary>La respuesta de la emisión (201): el documento, su número, el código, el estado, el enlace del PDF o el mensaje al deudor.</summary>
public sealed record DocumentoEmitidoRespuestaDto(
    Guid DocumentPublicId,
    string Number,
    string VerificationCode,
    string Status,
    DateOnly? FinalityUntil,
    EnlaceDeCarteraDto? Download,
    Guid? DebtorMessagePublicId);

/// <summary>El destinatario del envío posterior (nulo = el titular).</summary>
public sealed record EnviarDocumentoRequest(Guid? RecipientPersonPublicId);

/// <summary>Lo que respondió el envío: el mensaje al deudor que quedó en la cola.</summary>
public sealed record DocumentoEnviadoDto(Guid DebtorMessagePublicId);

/// <summary>El titular como lo muestra la verificación: el nombre y el documento enmascarado.</summary>
public sealed record TitularVerificadoDto(string FullName, string DocumentNumberMasked);

/// <summary>La anulación como la muestra la verificación.</summary>
public sealed record AnulacionVerificadaDto(DateTime At, string Reason);

/// <summary>
/// La respuesta de la verificación de un código. <see cref="PdfMatches"/> (K5) sólo viene si se comparó la huella de un PDF: <c>true</c> si es la del
/// PDF guardado.
/// </summary>
public sealed record DocumentoVerificadoDto(bool Authentic, string Status, string Kind, string Number, DateTime IssuedAt, TitularVerificadoDto Holder,
    JsonElement EssentialData, AnulacionVerificadaDto? Voided, bool? PdfMatches = null);

/// <summary>Lo que devuelve <c>huellaDeArchivo.calcular</c> (K5): el nombre, el tamaño y la huella SHA-256 en hexadecimal; nunca los bytes.</summary>
public sealed record HuellaDeArchivoDto(string Name, long Size, string Sha256);

/// <summary>Una obligación con saldo que niega el paz y salvo (<c>Lending.Document.ClearanceDenied</c>, <c>data.obligations[]</c>).</summary>
public sealed record ObligacionQueNiegaDto(Guid LoanPublicId, string Number, decimal Balance);

/// <summary>Una venta en reproceso que niega el paz y salvo general (<c>data.pendingSales[]</c>).</summary>
public sealed record VentaQueNiegaDto(string Invoice, Guid MessagePublicId);

/// <summary>Una imagen vigente de los documentos (logotipo o firma).</summary>
public sealed record ImagenDeDocumentosDto(Guid AttachmentPublicId, string FileName, string ContentType, DateTime UploadedAt);

/// <summary>El dueño de las imágenes de los documentos (la configuración de Cartera) y las vigentes.</summary>
public sealed record ImagenesDeDocumentosDto(Guid SetupPublicId, string LogoOwnerType, string SignatureOwnerType, ImagenDeDocumentosDto? Logo,
    ImagenDeDocumentosDto? Signature);
