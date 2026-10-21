namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, fase 4 (T0310; US17, C108, T74–T76): los espejos de contracts/api.md §14.1 (plantillas de mensaje versionadas) y §14.2 (cola de
// mensajes al deudor, adaptadores-y-archivos.md §4.6). Canal: 1 correo, 2 SMS, 3 WhatsApp, 4 carta. Propósito: 1 cobranza, 2 aviso previo,
// 3 informativo, 4 entrega de documentos, 5 aviso de gastos de cobranza. Estado (DebtorMessageEventKind): 1 en cola, 2 diferido,
// 3 excluido, 4 enviado, 5 entregado, 6 fallido, 7 simulado, 8 cancelado. (nuevo)

public sealed record PlantillaDelMensajeDto(string Code, int Version);

public sealed record UltimoEventoDelMensajeDto(int Kind, DateTime At, string? Detail, DateTime? NextAttemptAt, IReadOnlyList<string> Reasons);

/// <summary>Un mensaje del registro; <c>RecipientRole</c> 1 deudor, 2 codeudor, 3 pagador.</summary>
public sealed record MensajeAlDeudorDto(
    Guid PublicId,
    CreditoRefDto? Loan,
    PersonaDeCarteraDto Recipient,
    int RecipientRole,
    int Channel,
    int Purpose,
    PlantillaDelMensajeDto? Template,
    DateTime RequestedAt,
    DateTime? ScheduledFor,
    int Status,
    UltimoEventoDelMensajeDto LastEvent,
    bool Simulated);

/// <summary>Un evento de la historia del mensaje (sólo inserción).</summary>
public sealed record EventoDelMensajeDto(
    int Sequence,
    int Kind,
    DateTime OccurredAt,
    DateTime? NextAttemptAt,
    IReadOnlyList<string> Reasons,
    string? ReasonCode,
    string? ReasonText,
    string? DestinationAddress,
    int? Attempt,
    string? ProviderReference,
    int? Outcome,
    int ActorKind);

public sealed record DetalleDelMensajeDto(
    MensajeAlDeudorDto Message,
    string? Subject,
    string Body,
    string ChannelCode,
    string? DestinationAddress,
    Guid? AuthorizationPublicId,
    IReadOnlyList<EventoDelMensajeDto> Events);

/// <summary>La ventana de contacto: <c>Decision</c> <c>Permitido</c>, <c>Diferido</c> o <c>Excluido</c>, con las razones.</summary>
public sealed record VentanaDeContactoDto(string Decision, DateTimeOffset? AllowedAt, DateTimeOffset? NextAllowedAt, IReadOnlyList<string> Reasons, string? ChannelUsed);

/// <summary>El envío manual; sin <c>ScheduledFor</c> es inmediato.</summary>
public sealed record EnviarMensajeRequest(
    Guid? LoanPublicId,
    Guid RecipientPersonPublicId,
    int Channel,
    int Purpose,
    string TemplateCode,
    DateTimeOffset? ScheduledFor,
    IReadOnlyDictionary<string, string?>? Variables);

/// <summary>Los filtros del registro de envíos.</summary>
public sealed record FiltrosDeMensajes(
    Guid? Loan = null,
    Guid? Person = null,
    int? Purpose = null,
    int? Channel = null,
    int? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 50);

// --------------------------------------------------------------------------------------------- §14.1 plantillas --

/// <summary>Un aviso de la revisión de una plantilla (variable no permitida, SMS largo, plantilla del proveedor faltante…).</summary>
public sealed record AvisoDePlantillaDto(string Codigo, string Mensaje);

public sealed record VersionDePlantillaDeMensajeDto(
    Guid PublicId,
    string Code,
    int Purpose,
    int Channel,
    int Version,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Subject,
    string Body,
    IReadOnlyList<string> Variables,
    string? ProviderTemplateId,
    bool IsSeeded,
    bool ReviewPending,
    IReadOnlyList<AvisoDePlantillaDto> Warnings);

/// <summary>El cuerpo de una versión nueva; el código va en la ruta.</summary>
public sealed record VersionDePlantillaDeMensajeRequest(
    int Purpose,
    int Channel,
    string? Subject,
    string Body,
    IReadOnlyList<string>? Variables,
    string? ProviderTemplateId,
    DateOnly ValidFrom,
    string Reason);

/// <summary>La vista previa (consulta por POST, sin clave): una plantilla guardada por código o un cuerpo en edición.</summary>
public sealed record VistaPreviaDeMensajeRequest(
    int Channel,
    string? Code,
    string? Body,
    int? Purpose,
    string? Subject,
    Guid? SampleLoanPublicId,
    string? ProviderTemplateId);

/// <summary>El texto renderizado con datos de ejemplo, su largo y, en SMS, los segmentos.</summary>
public sealed record VistaPreviaDeMensajeDto(string? Subject, string Body, int Length, int? Segments, IReadOnlyList<AvisoDePlantillaDto> Warnings);
