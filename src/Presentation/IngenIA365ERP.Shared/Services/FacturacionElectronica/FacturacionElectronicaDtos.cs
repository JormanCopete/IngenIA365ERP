using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.FacturacionElectronica;

// Espejos de facturación electrónica (feature 012, I4, T752; contracts/api.md §24). Los enums llegan como número
// (EnumPorNombreONumero) y se mandan por nombre (TextosDeFacturacionElectronica); sólo PublicId (Principio VI). (nuevos)

// ------------------------------------------------------------------------------------------ configuración (§24.1) --

public sealed record VigenciaDeEmisionDto(
    Guid SettingPublicId,
    int Mode,
    string ChannelCode,
    int Environment,
    string? SoftwareId,
    string? TestSetId,
    DateTime? TestSetAcceptedAt,
    int EmailDeliveryBy,
    bool IsEnabled,
    DateTime? CredentialVerifiedAt,
    string IssuerTaxId,
    string IssuerCheckDigit,
    string IssuerBusinessName,
    string IssuerAddress,
    string IssuerMunicipalityDaneCode,
    string IssuerEmail,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? CreatedByName);

public sealed record CapacidadesDelCanalDto(
    IReadOnlyList<string> DocumentKinds,
    IReadOnlyList<string> Events,
    bool AcceptsErpNumber,
    bool IsAsync,
    bool ReturnsPdf,
    bool CanSendEmail,
    bool QueriesNumberingRanges,
    bool IssuerContingency);

public sealed record CanalDisponibleDto(string ChannelCode, string Name, CapacidadesDelCanalDto Capabilities);

public sealed record CredencialDelCanalDto(string? Key, bool Configured, DateTime? VerifiedAt);

/// <summary><c>GET /settings</c>: la vigencia actual, su historial, los canales de la instalación y el estado de la credencial.</summary>
public sealed record ConfiguracionDeEmisionDto(
    VigenciaDeEmisionDto? Current,
    IReadOnlyList<VigenciaDeEmisionDto> History,
    IReadOnlyList<CanalDisponibleDto> AvailableChannels,
    CredencialDelCanalDto Credential);

/// <summary>
/// <c>POST /settings</c> (<c>ConfigureEmissionCommand</c>): una vigencia nueva que cierra la anterior la víspera. Los enums por nombre. Los
/// datos del emisor nulos se toman de la vigencia anterior o de la empresa. La credencial nunca viaja.
/// </summary>
public sealed record ConfiguracionDeEmisionRequest(
    string Mode,
    string ChannelCode,
    string Environment,
    string? SoftwareId,
    string? TestSetId,
    string EmailDeliveryBy,
    bool IsEnabled,
    DateOnly ValidFrom,
    string Reason,
    string? IssuerTaxId = null,
    string? IssuerCheckDigit = null,
    string? IssuerBusinessName = null,
    string? IssuerAddress = null,
    string? IssuerMunicipalityDaneCode = null,
    string? IssuerEmail = null);

public sealed record MensajeDelCanalDto(string Rule, string Kind, string Text, string? Translation);

public sealed record VerificacionDeCredencialDto(bool Verified, DateTime? VerifiedAt, string Outcome, IReadOnlyList<MensajeDelCanalDto> Messages);

// ---------------------------------------------------------------------------------------------- preparación (§24.3) --

public sealed record QuienLoArreglaDto(string? Page, string? Permission);

public sealed record FaltanteDeEmisionDto(string Code, string Message, QuienLoArreglaDto WhoFixes);

public sealed record CanalDeLaPreparacionDto(string ChannelCode, int Mode, int Environment);

public sealed record ResolucionDeLaPreparacionDto(int Kind, string Prefix, string Status, decimal ConsumedFraction, int DaysToExpire);

public sealed record ContingenciaAbiertaDto(Guid ContingencyPublicId, int Type);

/// <summary><c>GET /readiness</c>: el veredicto (<c>VeredictoFiscal</c>) y lo que falta con quién lo corrige.</summary>
public sealed record PreparacionDianDto(
    DateOnly AsOf,
    bool Obligated,
    int Verdict,
    CanalDeLaPreparacionDto? Channel,
    IReadOnlyList<ResolucionDeLaPreparacionDto> Resolutions,
    ContingenciaAbiertaDto? OpenContingency,
    IReadOnlyList<FaltanteDeEmisionDto> Missing);

// ---------------------------------------------------------------------------------------------- resoluciones (§24.2) --

public sealed record AsociacionDeResolucionDto(string ChannelCode, string? SoftwareId, DateOnly ValidFrom, DateOnly? ValidTo, string? TechnicalKeyMasked);

public sealed record ResolucionDianDto(
    Guid ResolutionPublicId,
    int Kind,
    int? BacksUpKind,
    string ResolutionNumber,
    DateOnly ResolutionDate,
    string Prefix,
    long RangeFrom,
    long RangeTo,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    int Environment,
    long? LastIssuedNumber,
    decimal ConsumedFraction,
    int DaysToExpire,
    string Status,
    IReadOnlyList<AsociacionDeResolucionDto> Channels)
{
    /// <summary>Con números emitidos sólo se retira (se mueve <c>validTo</c> hacia atrás): lo demás responde <c>Resolution.InUse</c>.</summary>
    public bool TieneNumerosEmitidos => LastIssuedNumber is not null;
}

public sealed record DocumentosDeResolucionPorMesDto(int Year, int Month, int Count, long FirstConsecutive, long LastConsecutive);

public sealed record DetalleDeResolucionDto(ResolucionDianDto Resolution, IReadOnlyList<DocumentosDeResolucionPorMesDto> DocumentsByMonth);

/// <summary><c>POST /resolutions</c> y <c>PUT /resolutions/{id}</c>: los enums por nombre.</summary>
public sealed record ResolucionRequest(
    string Kind,
    string? BacksUpKind,
    string ResolutionNumber,
    DateOnly ResolutionDate,
    string? Prefix,
    long RangeFrom,
    long RangeTo,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Environment,
    string Reason,
    string? Notes = null);

/// <summary><c>POST /resolutions/{id}/channels</c>: la clave técnica sólo de entrada (vuelve enmascarada) o propuesta por el canal.</summary>
public sealed record AsociacionACanalRequest(
    string ChannelCode,
    string? SoftwareId,
    DateOnly ValidFrom,
    string? TechnicalKey,
    bool FetchTechnicalKeyFromChannel,
    string Reason);

// ---------------------------------------------------------------------------------------------- documentos (§24.4) --

public sealed record OrigenDelDocumentoDto(string Module, Guid DocumentPublicId, string? DocumentClass, string? Route);

/// <summary>Una fila de la bandeja (<c>ElectronicDocumentDto</c>).</summary>
public sealed record DocumentoElectronicoDto(
    Guid ElectronicDocumentPublicId,
    int Kind,
    string DianDocumentTypeCode,
    string Prefix,
    long Consecutive,
    string Number,
    int Environment,
    string ChannelCode,
    OrigenDelDocumentoDto Source,
    string? CounterpartyName,
    DateTime IssuedAt,
    decimal Total,
    int Status,
    int? ContingencyType,
    int? RejectedBy,
    string? UniqueCode,
    int? UniqueCodeKind,
    int AttemptCount,
    DateTime? NextAttemptAt,
    DateTime? TransmissionDeadline,
    string? LastMessage);

public sealed record ReferenciaDeResolucionDto(Guid PublicId, string Number, string Prefix);

public sealed record ArtefactoDeVersionDto(string Artifact, Guid AttachmentPublicId, string? FileName);

public sealed record VersionElectronicaDto(
    int VersionNumber,
    int Reason,
    Guid SourceDocumentPublicId,
    string CanonicalSha256,
    DateTime CreatedAt,
    IReadOnlyList<ArtefactoDeVersionDto> Artifacts,
    JsonElement? PartySnapshotChange);

public sealed record SolicitanteDto(int Kind, string Name);

public sealed record MensajeDeTransmisionDto(string Rule, string Kind, string Text, string? Translation);

public sealed record TransmisionElectronicaDto(
    int AttemptNumber,
    int Operation,
    string ChannelCode,
    DateTime RequestedAt,
    SolicitanteDto RequestedBy,
    int DurationMs,
    int Outcome,
    string? ProviderCode,
    string? DianStatusCode,
    IReadOnlyList<MensajeDeTransmisionDto> Messages,
    string? ExternalReference,
    Guid? ApplicationResponseAttachmentPublicId);

public sealed record DocumentoCorregidoDto(Guid ElectronicDocumentPublicId, string Number, string? UniqueCode);

public sealed record CorreccionDelDocumentoDto(Guid ElectronicDocumentPublicId, int Kind, string Number, int Status);

public sealed record ContingenciaDelDocumentoDto(Guid ContingencyPublicId, int Type, DateTime StartedAt, DateTime? EndedAt, DateTime? DeadlineAt);

public sealed record CancelacionDelDocumentoDto(string? Reason, string? ByName, DateTime? At);

/// <summary>El detalle (<c>ElectronicDocumentDetailDto</c>).</summary>
public sealed record DetalleDeDocumentoElectronicoDto(
    DocumentoElectronicoDto Document,
    ReferenciaDeResolucionDto? Resolution,
    string? QrContent,
    DateTime? ValidatedAt,
    DateTime? DeliveredAt,
    DateTime? EmailSentAt,
    int EmailDeliveryBy,
    IReadOnlyList<VersionElectronicaDto> Versions,
    IReadOnlyList<TransmisionElectronicaDto> Transmissions,
    DocumentoCorregidoDto? CorrectsDocument,
    IReadOnlyList<CorreccionDelDocumentoDto> CorrectedBy,
    ContingenciaDelDocumentoDto? Contingency,
    CancelacionDelDocumentoDto? Cancellation)
{
    /// <summary>¿El rechazo está confirmado por una consulta posterior al último envío? (lo decide el servidor; esto sólo habilita botones).</summary>
    public bool RechazoConfirmado => AccionesDelDocumentoElectronico.RechazoConfirmado(Document.Status,
        Transmissions.OrderBy(t => t.AttemptNumber).Select(t => (t.Operation, t.Outcome)));
}

/// <summary>Los filtros de la bandeja (<c>GET /documents</c>); estado y tipo por nombre.</summary>
public sealed class FiltroDeDocumentosElectronicos
{
    public string? Status { get; set; }
    public string? Kind { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Prefix { get; set; }
    public string? Number { get; set; }
    public bool? Contingency { get; set; }
    public bool? Overdue { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>La respuesta de <c>/retry</c> y <c>/query-status</c>: el estado, el resultado del canal y sus mensajes.</summary>
public sealed record ResultadoDeTransmisionDto(
    Guid ElectronicDocumentPublicId,
    int Status,
    int? Outcome,
    IReadOnlyList<MensajeDelCanalDto>? Messages,
    bool Attempted,
    string? Detail);

// ------------------------------------------------------------------------------------------- casos a, b y c (§24.5) --

/// <summary>Lo que se corrige de la contraparte en el caso a (nulo = no cambia). La identificación se corrige en el maestro.</summary>
public sealed record CambiosDeContraparteRequest(
    string? Name = null,
    string? Address = null,
    string? CityDaneCode = null,
    string? Email = null,
    string? Phone = null,
    IReadOnlyList<string>? Responsibilities = null);

public sealed record CorreccionCasoARequest(string Reason, CambiosDeContraparteRequest? PartySnapshotChanges);

public sealed record ReemplazoRequest(Guid ReplacementDocumentPublicId, string Reason);

public sealed record MotivoDeFacturacionRequest(string Reason);

/// <summary>El borrador de reemplazo del caso b; <see cref="EditRoute"/> es la ruta de API (la pantalla la traduce).</summary>
public sealed record BorradorDeReemplazoDto(Guid ReplacementDraftPublicId, string SourceModule, string EditRoute);

/// <summary>La respuesta de los casos a, b y c y del cambio de canal: sólo lo que la pantalla usa (el estado resultante).</summary>
public sealed record ResultadoDeCorreccionDto(Guid ElectronicDocumentPublicId, int Status, int? VersionNumber, Guid? VoidingDocumentPublicId,
    Guid? ReplacementDocumentPublicId, string? ChannelCode);

// --------------------------------------------------------------------------------------------- contingencias (§24.6) --

public sealed record QuienDetectoDto(int Kind, string Name);

public sealed record DocumentosDeLaContingenciaDto(int Total, int Pending, int Transmitted, int Rejected);

public sealed record ContingenciaDianDto(
    Guid ContingencyPublicId,
    int Type,
    string ChannelCode,
    DateTime StartedAt,
    DateTime? EndedAt,
    bool IsOpen,
    QuienDetectoDto DetectedBy,
    string Reason,
    DateTime? DeadlineAt,
    DocumentosDeLaContingenciaDto Documents);

public sealed record EvidenciaDeContingenciaDto(DateTime At, string ByName, string Note);

public sealed record DetalleDeContingenciaDto(
    ContingenciaDianDto Contingency,
    IReadOnlyList<DocumentoElectronicoDto> Documents,
    IReadOnlyList<EvidenciaDeContingenciaDto> Evidence,
    short DeadlineHoursApplied,
    string LegalSource,
    string? ClosedByName,
    string? CloseReason);

/// <summary><c>POST /contingencies</c>: sólo la 03 se declara a mano (<c>Issuer03</c>).</summary>
public sealed record DeclaracionDeContingenciaRequest(string Type, string Reason, DateTime? StartedAt, string? EvidenceNote);

public sealed record CierreDeContingenciaRequest(string Reason, DateTime? EndedAt, string? EvidenceNote);
