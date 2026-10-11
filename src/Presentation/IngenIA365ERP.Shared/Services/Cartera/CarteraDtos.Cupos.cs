namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US2 (T0409): los espejos de contracts/api.md §7.1 (cupos y disponible), §7.2 (bloqueos y embargos), §7.4 (saldos de aportes)
// y de los grupos conectados (/api/lending/connected-groups, T0393). Los enums salen como número. (nuevo)

// ------------------------------------------------------------------------------------------------ §7.1 cupos --

public sealed record LineaDeCupoDto(Guid PublicId, string Code, string Name);

/// <summary>El enrutamiento de un cupo que espera decisión (sellado en la versión).</summary>
public sealed record EnrutamientoDeCupoDto(
    int MatrixVersion,
    string LevelCode,
    string LevelName,
    int LevelKind,
    string? GoverningBody,
    IReadOnlyList<RazonDeEnrutamientoDto> Reasons,
    decimal ConsolidatedAmount,
    bool RelatedParty,
    string ContentSha256);

/// <summary>Un aviso de Cartera en una respuesta exitosa (p. ej. <c>Lending.Screening.Blocked</c> con el resultado que hay que liberar).</summary>
public sealed record AvisoDeCarteraDto(string Code, string Message, Guid? ScreeningResultPublicId = null);

/// <summary>
/// Un cupo (<c>CreditLimitDto</c>): clase 1 consolidado, 2 por línea, 3 rotativo; origen 1 calculado, 2 asignado, 3 migrado; <c>Status</c>
/// calculado (PendingDecision, Active, Blocked, Ended).
/// </summary>
public sealed record CupoDto(
    Guid PublicId,
    PersonaDeCarteraDto Person,
    int Kind,
    LineaDeCupoDto? Line,
    decimal Amount,
    int Origin,
    string Status,
    int? BlockReason,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    int VersionNumber,
    string Reason,
    int? PrepaymentPreference,
    Guid? DecisionPublicId,
    EnrutamientoDeCupoDto? Routing,
    IReadOnlyList<AvisoDeCarteraDto> Warnings);

public sealed record FiltrosDeCupos(Guid? PersonPublicId = null, int? Kind = null, string? Status = null, Guid? Line = null, int Page = 1, int PageSize = 20);

/// <summary>El otorgamiento: el asignado lleva monto; el calculado, la línea con su relación cupo/aportes.</summary>
public sealed record OtorgarCupoRequest(
    Guid PersonPublicId,
    int Kind,
    Guid? CreditLinePublicId,
    int Origin,
    decimal? Amount,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    int? PrepaymentPreference,
    string Reason,
    Guid? ScreeningResultPublicId = null,
    Guid? BureauQueryPublicId = null,
    IReadOnlyList<AutorizacionDeDatosRequest>? Authorizations = null);

/// <summary>La preferencia del rotativo: 1 reducir plazo, 2 reducir cuota.</summary>
public sealed record PreferenciaDeAbonoRequest(int Choice, string? Reason = null, Guid? EvidenceAttachmentPublicId = null);

public sealed record DisponibleDeCupoDto(Guid CreditLimitPublicId, LineaDeCupoDto? Line, decimal Limit, decimal Used, decimal Reserved,
    decimal ApprovedNotDisbursed, decimal Available);

public sealed record DisponibleAplicableDto(decimal? Available, Guid? CreditLimitPublicId);

/// <summary>El disponible de una persona a una fecha: cupo − saldos vivos − reservas − aprobadas sin desembolsar, calculado al leer.</summary>
public sealed record DisponibleDto(
    PersonaDeCarteraDto Person,
    DateOnly Date,
    DisponibleDeCupoDto? Consolidated,
    IReadOnlyList<DisponibleDeCupoDto> ByLine,
    IReadOnlyList<DisponibleDeCupoDto> Revolving,
    string Status,
    int? DaysPastDue,
    IReadOnlyList<BloqueoDto> Blocks,
    DisponibleAplicableDto Applicable);

// --------------------------------------------------------------------------------------------- §7.2 bloqueos --

/// <summary>
/// Un bloqueo del deudor: clase 1 manual, 2 mora, 3 validación fallida, 4 castigo, 5 listas, 6 retiro, 7 fallecimiento, 8 insolvencia,
/// 9 embargo, 10 cupo por revalidar; <c>Origin</c> Manual o Automatic; <c>HowItLifts</c> dice cómo se levanta.
/// </summary>
public sealed record BloqueoDto(
    Guid PublicId,
    PersonaDeCarteraDto Person,
    int Kind,
    string Origin,
    string? Reason,
    DateOnly Since,
    DateTime? LiftedAt,
    UsuarioDeCarteraDto? LiftedBy,
    string? LiftReason,
    Guid? Support,
    string HowItLifts);

public sealed record FiltrosDeBloqueos(Guid? PersonPublicId = null, int? Kind = null, bool? Active = null, int Page = 1, int PageSize = 20);

/// <summary>Un bloqueo a mano: sólo manual (1) o embargo (9), con motivo.</summary>
public sealed record BloqueoRequest(Guid PersonPublicId, int Kind, string Reason, Guid? SupportAttachmentPublicId = null);

// ------------------------------------------------------------------------------------------ §7.4 saldos de aportes --

public sealed record SaldoDeAportesDto(PersonaDeCarteraDto Person, decimal Contributions, decimal PermanentSavings, DateOnly CutoffDate, Guid LoadPublicId,
    DateTime LoadedAt, string? Source);

// ---------------------------------------------------------------------------------------------- grupos conectados --

public sealed record MiembroDeGrupoDto(PersonaDeCarteraDto Person, DateOnly ValidFrom, DateTime? EndedAt);

/// <summary>Un grupo conectado: fundamento 1 vínculo económico, 2 jurídico, 3 codeudor común, 4 vinculado.</summary>
public sealed record GrupoConectadoDto(
    Guid PublicId,
    string Name,
    int Basis,
    string Grounds,
    Guid? SupportAttachmentPublicId,
    DateOnly ValidFrom,
    DateTime? EndedAt,
    string? EndReason,
    IReadOnlyList<MiembroDeGrupoDto> Members);

public sealed record RegistrarGrupoRequest(string Name, int Basis, string Grounds, IReadOnlyList<Guid> MemberPublicIds, Guid? SupportAttachmentPublicId = null,
    DateOnly? ValidFrom = null);

public sealed record MiembroDeGrupoRequest(Guid PersonPublicId, string Reason);
