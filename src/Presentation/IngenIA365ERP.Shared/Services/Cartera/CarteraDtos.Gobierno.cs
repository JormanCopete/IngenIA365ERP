namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, fase 4 (T0315; US17, T62–T69): los espejos de contracts/api.md §3.5 (atribuciones y montos), §3.6 (órganos), §5 (sesiones y
// actas), §3.7 (vinculados), §3.8 (alcance) y §14.4 (reclamos) tal como los publican Endpoints/Lending/*.cs. Los enums SALEN de la API como
// número y así se leen aquí; las pantallas los muestran con TextosDeCartera. Los montos operativos reusan MontoMaximoDto y
// FijarMontoMaximoRequest de Inventario (la misma plataforma, SEC_PermissionAmountLimits). (nuevo)

// ---------------------------------------------------------------------------------------------- referencias --

/// <summary>Una persona por su nombre (C84: sin documento ni contacto).</summary>
public sealed record PersonaDeCarteraDto(Guid PersonPublicId, string FullName);

/// <summary>Un usuario por su nombre.</summary>
public sealed record UsuarioDeCarteraDto(Guid UserPublicId, string Name);

/// <summary>Un órgano por su código y nombre.</summary>
public sealed record OrganoRefDto(Guid PublicId, string Code, string Name);

/// <summary>Un crédito por su número.</summary>
public sealed record CreditoRefDto(Guid LoanPublicId, string Number);

/// <summary>El cuerpo de las acciones que sólo piden motivo.</summary>
public sealed record MotivoDeCarteraRequest(string Reason);

/// <summary>El fin de una vigencia con su motivo.</summary>
public sealed record FinDeVigenciaRequest(DateOnly ValidTo, string Reason);

// ---------------------------------------------------------------------------------------- §3.5 atribuciones --

/// <summary>Un nivel de la matriz; <c>Order</c> es el rango (mayor = más alto), <c>Kind</c> 1 individual, 2 colegiado.</summary>
public sealed record NivelDeAtribucionDto(
    string Code,
    string Name,
    int Order,
    int Kind,
    string? PermissionCode,
    OrganoRefDto? GoverningBody,
    decimal? ConsolidatedAmountCeiling,
    bool IsExceptionLevel,
    bool IsBoardLevel);

/// <summary>Las condiciones de una regla; una condición nula no importa.</summary>
public sealed record CondicionesDeReglaDto(
    Guid? CreditLinePublicId = null,
    string? ModalityCode = null,
    decimal? MinConsolidatedAmount = null,
    decimal? MaxConsolidatedAmount = null,
    int? MinTermMonths = null,
    int? MaxTermMonths = null,
    int? CollateralKind = null,
    bool? ScoreBelowMinimum = null,
    bool? HasWarnings = null,
    bool? PolicyException = null,
    int? MinDaysPastDue = null,
    int? MaxDaysPastDue = null,
    int? Concept = null);

public sealed record ReglaDeAtribucionDto(int Order, string Subject, CondicionesDeReglaDto Conditions, string MinimumLevelCode);

/// <summary>Una versión de la matriz; <c>ValidTo</c> es la víspera de la siguiente.</summary>
public sealed record MatrizDeAtribucionesDto(
    Guid PublicId,
    int Version,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? Source,
    string ContentSha256,
    DateTime RegisteredAt,
    IReadOnlyList<NivelDeAtribucionDto> Levels,
    IReadOnlyList<ReglaDeAtribucionDto> Rules);

/// <summary>Un nivel que se publica; el colegiado nombra su órgano por <c>PublicId</c>.</summary>
public sealed record NivelDeAtribucionRequest(
    string Code,
    string Name,
    int Order,
    int Kind,
    string? PermissionCode,
    Guid? GoverningBodyPublicId,
    decimal? ConsolidatedAmountCeiling,
    bool IsExceptionLevel,
    bool IsBoardLevel);

public sealed record ReglaDeAtribucionRequest(int Order, string Subject, string MinimumLevelCode, CondicionesDeReglaDto? Conditions);

/// <summary>El cuerpo de <c>POST /attribution-matrices</c>: una versión COMPLETA.</summary>
public sealed record PublicarMatrizRequest(
    DateOnly ValidFrom,
    string Reason,
    string? Source,
    IReadOnlyList<NivelDeAtribucionRequest> Levels,
    IReadOnlyList<ReglaDeAtribucionRequest> Rules);

/// <summary>El cuerpo de la vista previa «¿a qué nivel iría?» (consulta por POST, sin clave); el monto es el consolidado.</summary>
public sealed record VistaPreviaDeAtribucionRequest(
    string Subject,
    decimal Amount,
    Guid? CreditLinePublicId = null,
    Guid? PersonPublicId = null,
    int? TermInstallments = null,
    IReadOnlyList<int>? CollateralKinds = null,
    decimal? Score = null,
    IReadOnlyList<string>? Alerts = null,
    int? DaysPastDue = null,
    int? Concept = null,
    bool? PolicyException = null,
    DateOnly? Date = null);

/// <summary>Una razón del enrutamiento: 1 tope de monto, 2 regla, 3 vinculado, 4 excepción.</summary>
public sealed record RazonDeEnrutamientoDto(int Kind, string LevelCode, string Detail);

public sealed record VistaPreviaDeAtribucionDto(
    NivelDeAtribucionDto EffectiveLevel,
    IReadOnlyList<RazonDeEnrutamientoDto> Reasons,
    decimal ConsolidatedAmount,
    IReadOnlyList<int> NonExceptionable,
    int MatrixVersion);

// -------------------------------------------------------------------------------------------- §3.6 órganos --

/// <summary>Una fracción exacta; <c>Comparison</c> 1 «al menos», 2 «más de».</summary>
public sealed record FraccionDto(int Numerator, int Denominator, int Comparison);

/// <summary>Una versión de las reglas; <c>MajorityBase</c> 1 presentes con voto, 2 composición.</summary>
public sealed record ReglasDeOrganoDto(
    int Version,
    FraccionDto Quorum,
    FraccionDto OrdinaryMajority,
    int MajorityBase,
    FraccionDto? RelatedPartyFraction,
    DateOnly ValidFrom,
    DateTime RegisteredAt,
    string Source,
    string Reason);

public sealed record OrganoEnListaDto(
    Guid PublicId,
    string Code,
    string Name,
    int Kind,
    bool DecidesCredit,
    string MinutesPrefix,
    string NextMinutesNumber,
    ReglasDeOrganoDto? CurrentRules,
    int Members,
    bool IsActive);

public sealed record MiembroDeOrganoDto(
    Guid MemberPublicId,
    PersonaDeCarteraDto Person,
    int Role,
    bool Votes,
    bool IsPrincipal,
    Guid? AlternateForMemberPublicId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? AppointmentNotes);

public sealed record OrganoDto(
    Guid PublicId,
    string Code,
    string Name,
    int Kind,
    bool DecidesCredit,
    string MinutesPrefix,
    string NextMinutesNumber,
    ReglasDeOrganoDto? CurrentRules,
    bool IsActive,
    IReadOnlyList<MiembroDeOrganoDto> MemberList,
    IReadOnlyList<ReglasDeOrganoDto> RulesHistory);

public sealed record FraccionRequest(int Numerator, int Denominator, int Comparison);

/// <summary>Las reglas que se registran; la fracción de vinculados sólo en el consejo.</summary>
public sealed record ReglasDeOrganoRequest(
    FraccionRequest Quorum,
    FraccionRequest OrdinaryMajority,
    FraccionRequest? RelatedPartyFraction,
    DateOnly ValidFrom,
    string Reason,
    string Source,
    int MajorityBase);

public sealed record CrearOrganoRequest(string Code, string Name, int Kind, bool DecidesCredit, string MinutesPrefix, ReglasDeOrganoRequest Rules);

/// <summary>El nombramiento; el suplente nombra a su principal.</summary>
public sealed record NombrarMiembroRequest(
    Guid PersonPublicId,
    int Position,
    bool Votes,
    DateOnly ValidFrom,
    Guid? AlternateForMemberPublicId,
    string? AppointmentNotes,
    Guid? AppointmentAttachmentPublicId);

// ------------------------------------------------------------------------------------------- §5 sesiones --

public sealed record QuorumDeSesionDto(int Required, int Present, bool Met, int Composition);

/// <summary>Una asistencia; <c>MemberPublicId</c> nulo = invitado. <c>Status</c> 1 presente, 2 ausente, 3 excusado.</summary>
public sealed record AsistenteDto(PersonaDeCarteraDto Person, Guid? MemberPublicId, int? Role, int Status, Guid? SubstitutesForMemberPublicId);

/// <summary>Una persona excluida de un punto: <c>Applicant</c>, <c>Filer</c>, <c>Interested</c>.</summary>
public sealed record ExcluidoDto(PersonaDeCarteraDto Person, string Reason);

/// <summary>Un voto, también el reemplazado (con <c>ReplacedAt</c>).</summary>
public sealed record VotoDto(
    Guid VotePublicId,
    PersonaDeCarteraDto Person,
    int Vote,
    int Method,
    UsuarioDeCarteraDto? RecordedBy,
    DateTime At,
    Guid? ReplacesVotePublicId,
    string? ReplacementReason,
    DateTime? ReplacedAt);

public sealed record ConteoDto(int For, int Against, int Abstain, int Composition, int Eligible, int Required);

public sealed record PuntoDeSesionDto(
    Guid ItemPublicId,
    int Order,
    string Subject,
    Guid SubjectPublicId,
    string Title,
    string? Summary,
    int Kind,
    int Rule,
    IReadOnlyList<ExcluidoDto> Excluded,
    IReadOnlyList<VotoDto> Votes,
    ConteoDto? Tally,
    int? Outcome,
    string? Conditions,
    string? Explanation,
    Guid? DecisionPublicId);

/// <summary>
/// Una sesión con su acta. <c>CurrentContentSha256</c> es la huella del acta tal como está: la que se manda al cerrarla; <c>ContentSha256</c>,
/// la sellada. <c>Status</c> 1 borrador, 2 abierta, 3 cerrada.
/// </summary>
public sealed record SesionDto(
    Guid PublicId,
    OrganoRefDto Body,
    string? Number,
    int Kind,
    int Mode,
    DateOnly HeldOn,
    string? Location,
    int Status,
    QuorumDeSesionDto? Quorum,
    IReadOnlyList<AsistenteDto> Attendees,
    IReadOnlyList<PuntoDeSesionDto> Items,
    string? Notes,
    string? ContentSha256,
    string? CurrentContentSha256,
    UsuarioDeCarteraDto? ClosedBy,
    DateTime? ClosedAt,
    Guid? MinutesAttachmentPublicId);

public sealed record SesionEnListaDto(
    Guid PublicId,
    OrganoRefDto Body,
    string? Number,
    int Kind,
    int Mode,
    DateOnly HeldOn,
    int Status,
    int Items,
    DateTime? ClosedAt);

/// <summary>La convocatoria: la sesión nace en borrador.</summary>
public sealed record ConvocarSesionRequest(Guid GoverningBodyPublicId, int Kind, int Mode, DateOnly HeldOn, string? Location, string? Notes);

public sealed record AsistenteRequest(Guid MemberPublicId, bool Present, Guid? SubstitutesForMemberPublicId = null);

/// <summary>La asistencia; los invitados van por su persona y no cuentan para el quórum.</summary>
public sealed record AsistenciaRequest(IReadOnlyList<AsistenteRequest> Attendees, IReadOnlyList<Guid>? Guests);

/// <summary>Un punto: el sujeto y su <c>PublicId</c>; la regla y los excluidos los calcula el sistema.</summary>
public sealed record PuntoDeSesionRequest(string Subject, Guid SubjectPublicId, int Kind, string? Notes, Guid? RecommendsToBodyPublicId);

/// <summary>La corrección de un punto abierto.</summary>
public sealed record CorreccionDePuntoRequest(int? ProposedOutcome, string? Conditions, string? Notes);

/// <summary>A quién se le pide la presencia.</summary>
public sealed record DesafioDeSesionRequest(Guid PersonPublicId);

/// <summary>La presencia que acompaña al voto: la aserción (base64url de <c>webauthn.js</c>) o el código TOTP.</summary>
public sealed record PresenciaDeVotoRequest(Guid ChallengePublicId, string? Assertion, string? TotpCode);

/// <summary>Un voto; <c>Method</c> 1 secretaría, 2 sesión propia, 3 passkey presente, 4 TOTP presente. El de reemplazo lleva su motivo.</summary>
public sealed record VotoRequest(Guid PersonPublicId, int Vote, int Method, PresenciaDeVotoRequest? Presence, string? ReplacementReason);

/// <summary>El desafío de presencia: <c>Methods</c> (<c>Passkey</c>, <c>Totp</c>) y las opciones WebAuthn en base64url.</summary>
public sealed record DesafioDeVotoDto(Guid ChallengePublicId, DateTime ExpiresAt, IReadOnlyList<string> Methods, string? PublicKeyOptions);

/// <summary>El cierre: la huella del acta que se revisó.</summary>
public sealed record CierreDeSesionRequest(string ExpectedContentSha256);

/// <summary>Un punto de un acta cerrada (selector de «punto de acta», api.md §1.9).</summary>
public sealed record ActaDelPuntoDto(Guid PublicId, string? Number, OrganoRefDto Body, DateTime? ClosedAt);

public sealed record PuntoDeActaCerradaDto(
    Guid SessionItemPublicId,
    ActaDelPuntoDto Session,
    string Subject,
    Guid SubjectPublicId,
    string Title,
    int Kind,
    int? Outcome,
    string? Conditions);

/// <summary>Los filtros de la lista de sesiones.</summary>
public sealed record FiltrosDeSesiones(Guid? Body = null, int? Status = null, DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 50);

// ------------------------------------------------------------------------------------------ §3.7 vinculados --

public sealed record PersonaConDocumentoDto(Guid PersonPublicId, string FullName, string DocumentType, string DocumentNumber);

/// <summary>Un vínculo (FR-040); <c>Source</c> 1 manual, 2 importación, 3 migración.</summary>
public sealed record VinculadoDto(
    Guid PublicId,
    PersonaConDocumentoDto Person,
    int Basis,
    PersonaDeCarteraDto? LinkedTo,
    int? KinshipKind,
    byte? KinshipDegree,
    decimal? ContributionPercentage,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    int Source,
    string? Notes,
    string? EndReason);

/// <summary>El alta: <c>Source</c> es el soporte descrito (acta, certificado, registro civil); el pariente lleva clase y grado.</summary>
public sealed record RegistrarVinculoRequest(
    Guid PersonPublicId,
    int Basis,
    DateOnly ValidFrom,
    Guid? LinkedToPersonPublicId,
    int? KinshipKind,
    byte? KinshipDegree,
    decimal? ContributionPercentage,
    DateOnly? ValidTo,
    string? Source,
    Guid? SupportAttachmentPublicId);

// ------------------------------------------------------------------------------------------- §3.8 alcance --

public sealed record UsuarioDelAlcanceDeCarteraDto(Guid UserPublicId, string Name, string? Email);

public sealed record SucursalContableDelAlcanceDto(Guid PublicId, string? Code, string Name);

public sealed record OficinaDelAlcanceDto(Guid BranchPublicId, string Code, string Name, bool IsDefault, SucursalContableDelAlcanceDto? AccountingBranch);

public sealed record LineaDelAlcanceDto(Guid CreditLinePublicId, string Code, string Name, bool IsDefault);

/// <summary>El alcance de Cartera: las oficinas (de sólo lectura, las asigna Seguridad) y las líneas que opera.</summary>
public sealed record AlcanceDeCarteraDto(
    UsuarioDelAlcanceDeCarteraDto User,
    bool HasAllOffices,
    bool HasAllLines,
    IReadOnlyList<OficinaDelAlcanceDto> Offices,
    IReadOnlyList<LineaDelAlcanceDto> Lines);

public sealed record LineaAsignadaRequest(Guid CreditLinePublicId, bool IsDefault);

/// <summary>El reemplazo en bloque de las líneas, a lo sumo una por defecto.</summary>
public sealed record FijarLineasRequest(IReadOnlyList<LineaAsignadaRequest> Lines);

// ------------------------------------------------------------------------------------------ §14.4 reclamos --

/// <summary>Un reclamo o suplantación sobre el reporte a centrales; <c>Kind</c> 1 reclamo, 2 suplantación.</summary>
public sealed record ReclamoDeCentralesDto(
    Guid PublicId,
    PersonaDeCarteraDto Person,
    CreditoRefDto? Loan,
    int Kind,
    int Status,
    string? BureauCode,
    DateOnly ReceivedAt,
    DateOnly DueDate,
    bool Extended);
