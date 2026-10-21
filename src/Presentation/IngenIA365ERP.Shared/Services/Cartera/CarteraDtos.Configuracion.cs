using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, fase 4 (T0315; US17): los espejos de contracts/api.md §3.4 (parámetros del módulo LND), §3.9 (registro de plantillas),
// §3.10 (alertas, que reusan AlertaDto y TipoDeAlertaDto de la plataforma) y §22.2 (autorizaciones del titular por finalidad). (nuevo)

// ------------------------------------------------------------------------------------------- §3.4 parámetros --

/// <summary>
/// Una clave de Cartera con su valor vigente (la forma de <c>ParameterDto</c> de la plataforma). <c>LegalLimits</c> son los códigos de los
/// límites legales que la acotan; <c>PendingValidation</c>, por qué su defecto sigue «pendiente de validar» (nulo si no lo está).
/// <c>AvailableFrom</c> es la entrega (K1…): una clave de una entrega que no llegó no se edita.
/// </summary>
public sealed record ParametroDeCarteraDto(
    string Module,
    string Key,
    string Description,
    string Type,
    IReadOnlyList<string>? AllowedValues,
    string DefaultValue,
    IReadOnlyList<int> AllowedScopes,
    bool SealedOnConfirm,
    string? RequiredPermission,
    bool RequiresLegalSource,
    string AvailableFrom,
    ValorVigenteDeParametroDto Current,
    IReadOnlyList<ExcepcionDeParametroDto> Overrides,
    IReadOnlyList<ParametroProgramadoDto> Scheduled,
    IReadOnlyList<string>? LegalLimits = null,
    string? PendingValidation = null);

/// <summary>El cuerpo de <c>POST /parameters/{key}/versions</c>; <c>ScopeKind</c> 0 general, 6 línea de crédito.</summary>
public sealed record NuevaVigenciaDeCarteraRequest(int ScopeKind, Guid? ScopePublicId, string Value, DateOnly ValidFrom, string Reason, string? LegalSource);

// ------------------------------------------------------------------------------------------- §3.9 plantillas --

/// <summary>Una plantilla del registro: su ruta de importación, su entrega (1 = K1…) y lo que puede hacer quien pregunta.</summary>
public sealed record PlantillaDeCarteraDto(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Sheets,
    string Route,
    string ImportPermission,
    int AvailableFrom,
    bool PersonalData,
    bool CanDownload,
    bool CanImport);

// --------------------------------------------------------------------------------------- §22.2 autorizaciones --

public sealed record VersionDePoliticaRefDto(Guid PublicId, int Version);

/// <summary>
/// Una autorización del titular por finalidad. <c>Status</c>: <c>Active</c>, <c>Declined</c>, <c>Revoked</c>, <c>Replaced</c>. Sin
/// <c>Evidence</c> no habilita centrales ni cobranza (FR-006).
/// </summary>
public sealed record AutorizacionDeDatosDto(
    Guid PublicId,
    int Purpose,
    int? ContactChannel,
    VersionDePoliticaRefDto PolicyVersion,
    int Origin,
    DateTime GrantedAt,
    Guid? Evidence,
    DateTime? RevokedAt,
    string Status);

/// <summary>El cuerpo de <c>POST /api/compliance/habeas-data/authorizations</c>; <c>Decision</c> 1 acepta, 2 rechaza.</summary>
public sealed record RegistrarAutorizacionRequest(
    Guid PersonPublicId,
    int Purpose,
    int? ContactChannel,
    Guid? PolicyVersionPublicId,
    Guid? EvidenceAttachmentPublicId,
    int? Origin,
    DateTime GrantedAt,
    int? Decision,
    string? Notes);

public sealed record AutorizacionRegistradaDto(Guid PublicId);
