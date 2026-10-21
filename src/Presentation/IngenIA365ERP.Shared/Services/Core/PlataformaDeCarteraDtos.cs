using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Core;

// Espejos de la plataforma que pide Cartera (feature 013, fase 2, T0094; contracts/api.md §21): valores nacionales (registro central del
// maestro y copia de la cooperativa), perfil regulatorio, canales externos y registro de participantes. Los enums llegan como número (int) y
// se mandan por nombre (string) con TextosDePlataforma. Sólo PublicId (Principio VI). (nuevos)

/// <summary>Una página: sirve para <c>PagedList</c> (<c>pageNumber</c>) y para <c>PagedResult</c> (<c>page</c>).</summary>
public sealed record PaginaDePlataformaDto<T>(IReadOnlyList<T> Items, long TotalCount, int TotalPages);

// ------------------------------------------------------------------------------ registro central (maestro, §21.5) --

public sealed record ValorNacionalCentralDto(
    Guid PublicId,
    string Code,
    string Key,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateOnly? ReferenceDate,
    decimal? Value,
    string? Unit,
    string? Label,
    string Source,
    string? Reason,
    long Sequence,
    DateTime RegisteredAt,
    string RegisteredBy,
    Guid? SupersedesPublicId,
    Guid? SupersededByPublicId);

public sealed record DocumentoNormativoCentralDto(
    Guid PublicId,
    string Kind,
    string Code,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    short SchemaVersion,
    bool Verified,
    string Sha256,
    string Source,
    long Sequence,
    DateTime RegisteredAt,
    string RegisteredBy,
    Guid? SupersedesPublicId);

/// <summary>Un valor esperado que no tiene vigencia registrada para los próximos días (C69).</summary>
public sealed record ValorNacionalFaltanteDto(string Code, string Key, DateOnly Date, string Reason);

public sealed record RegistrarValorNacionalRequest(
    string Code, string? Key, DateOnly ValidFrom, DateOnly? ValidTo, DateOnly? ReferenceDate, decimal? Value, string? Unit, string Source, string? Label);

public sealed record CorregirValorNacionalRequest(
    decimal? Value, DateOnly? ValidFrom, DateOnly? ValidTo, DateOnly? ReferenceDate, string Source, string Reason, string? Label);

public sealed record RegistrarDocumentoNormativoRequest(
    string Kind, string Code, DateOnly ValidFrom, DateOnly? ValidTo, short SchemaVersion, string Content, bool Verified, string Source,
    Guid? SupersedesPublicId, string? Reason);

public sealed record ValorNacionalRegistradoDto(Guid NationalValuePublicId);

public sealed record DocumentoNormativoRegistradoDto(Guid RegulatoryDocumentPublicId);

// -------------------------------------------------------------------------- copia de la cooperativa (§21.3) --

/// <summary>La revisión vigente de una copia. <c>Decision</c>: 1 conforme, 2 objetada.</summary>
public sealed record RevisionDeValorNacionalDto(int Decision, string By, DateTime At, string? Note);

/// <summary>Una copia de valor nacional. <c>Status</c>: 0 pendiente de validar, 1 validada, 2 objetada.</summary>
public sealed record ValorNacionalDto(
    Guid PublicId,
    string Code,
    string Key,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateOnly? ReferenceDate,
    decimal? Value,
    string? Unit,
    string? Label,
    string Source,
    long Sequence,
    DateTime KnownAt,
    Guid? Supersedes,
    int Status,
    RevisionDeValorNacionalDto? Review);

public sealed record ReplicaDeValoresNacionalesDto(
    Guid PublicId,
    DateTime At,
    long FromSequence,
    long ToSequence,
    int Values,
    int Documents,
    int Origin,
    string Outcome,
    string ActorName,
    JsonElement? Effects);

public sealed record DocumentoNormativoDto(
    Guid PublicId,
    string Kind,
    string Code,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    short SchemaVersion,
    string Sha256,
    bool Verified,
    DateTime KnownAt,
    int Status,
    RevisionDeValorNacionalDto? Review);

public sealed record DocumentoNormativoDetalleDto(DocumentoNormativoDto Document, string Source, string Content);

/// <summary>La revisión de una copia de valor (<c>{ decision, notes }</c>); <c>Decision</c> por nombre: Confirmed u Objected.</summary>
public sealed record RevisionDeValorRequest(string Decision, string? Notes);

/// <summary>La revisión de un documento (<c>{ status, notes }</c>).</summary>
public sealed record RevisionDeDocumentoRequest(string Status, string? Notes);

public sealed record RevisionRegistradaDto(Guid ReviewPublicId);

// ------------------------------------------------------------------------------------ perfil regulatorio (§21.4) --

public sealed record VersionDePerfilRegulatorioDto(
    Guid VersionPublicId,
    DateOnly ValidFrom,
    string OrganizationTypeCode,
    bool FinancialActivityAuthorized,
    byte SupervisionLevel,
    string? EmployeeFundCategory,
    byte? NiifGroup,
    bool LibranzaOperatorRegistered,
    string? LibranzaOperatorRegistrationNumber,
    DateOnly? LibranzaOperatorRegistrationDate,
    string Source,
    Guid? SupportAttachmentPublicId,
    string Reason,
    Guid? SupersedesPublicId,
    string RegisteredByName,
    DateTime RegisteredAt);

/// <summary>La evaluación de la obligación de aplicar pérdida esperada; <c>Obligated</c> nulo = sin evaluar.</summary>
public sealed record EvaluacionDePerdidaEsperadaDto(bool? Obligated, IReadOnlyList<string> Segments, DateOnly? From, string Basis);

public sealed record PerfilRegulatorioDto(
    VersionDePerfilRegulatorioDto? Current,
    IReadOnlyList<VersionDePerfilRegulatorioDto> Versions,
    EvaluacionDePerdidaEsperadaDto ExpectedLoss,
    bool? NiifGroupMismatch,
    byte? AccountingNiifGroup);

public sealed record VersionDePerfilCreadaDto(Guid VersionPublicId, bool NiifGroupMismatch, byte? AccountingNiifGroup, EvaluacionDePerdidaEsperadaDto ExpectedLoss);

public sealed record InscripcionDeOperadorRequest(string? Number, DateOnly? Date);

public sealed record VersionDePerfilRequest(
    string OrganizationType,
    bool FinancialActivity,
    byte SupervisionLevel,
    string? Category,
    byte? NiifGroup,
    InscripcionDeOperadorRequest? PayrollOperatorRegistration,
    DateOnly ValidFrom,
    string Source,
    Guid? SupportAttachmentPublicId,
    string Reason,
    Guid? SupersedesPublicId);

// -------------------------------------------------------------------------------------- canales externos (§21.2) --

/// <summary>
/// Una vigencia de canal externo. <c>Kind</c>: 1 SMS, 2 WhatsApp, 3 depósito de valores, 4 central de riesgo, 5 listas restrictivas;
/// <c>LastOutcome</c> el de la última verificación; <c>Circuit</c>: 1 cerrado, 2 abierto. La credencial nunca viaja: sólo su clave.
/// </summary>
public sealed record CanalExternoDto(
    Guid PublicId,
    int Kind,
    string? BureauCode,
    string ChannelCode,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? CredentialKey,
    DateTime? VerifiedAt,
    string? VerifiedBy,
    int? LastOutcome,
    bool Simulated,
    int Circuit);

public sealed record VerificacionDeCanalDto(int Outcome, string? Detail, DateTime? VerifiedAt);

public sealed record ConfiguracionDeCanalRequest(string Kind, string? BureauCode, string ChannelCode, DateOnly ValidFrom, string? CredentialKey, string Reason);

// ---------------------------------------------------------------------------------- registro de participantes (§21.1) --

public sealed record AdopcionDto(string Contract, int Version, DateOnly? ValidFrom);

/// <summary><c>Class</c> y <c>Role</c> son los números de <c>IntegrationParticipantClass</c> e <c>IntegrationParticipantRole</c>.</summary>
public sealed record ParticipanteDeIntegracionDto(
    Guid PublicId,
    string Code,
    int Class,
    int Role,
    string? Destination,
    bool IsEnabled,
    bool IsTestDouble,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<AdopcionDto> Adoptions,
    bool ImplementationAvailable,
    bool Counts);

public sealed record AdopcionRequest(string Contract, int Version);

public sealed record RegistroDeParticipanteRequest(
    string Code, string Class, string Role, string? Destination, bool IsEnabled, bool IsTestDouble, IReadOnlyList<AdopcionRequest> Adoptions,
    DateOnly ValidFrom, string Reason);

public sealed record RetiroDeParticipanteRequest(DateOnly ValidTo, string Reason);
