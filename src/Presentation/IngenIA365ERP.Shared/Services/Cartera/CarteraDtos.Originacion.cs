using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US2 (T0409): los espejos de contracts/api.md §4.1 (simulador), §4.2 (solicitudes), §4.3 (decisiones y «Mis pendientes»), §4.4
// (centrales) y §4.5 (listas restrictivas) tal como los publican Endpoints/Lending/*.cs. Los enums SALEN de la API como número y así se
// leen y se envían aquí (la API los admite por nombre o por número); las pantallas los muestran con TextosDeCartera. (nuevo)

// --------------------------------------------------------------------------------------------- §4.1 simulador --

/// <summary>Un abono extraordinario pactado: los meses del año y el monto.</summary>
public sealed record AbonoExtraordinarioRequest(IReadOnlyList<int> Months, decimal Amount);

/// <summary>La gracia: <c>Kind</c> 0 sin gracia, 1 a capital, 2 total.</summary>
public sealed record GraciaDeSimulacionRequest(int Kind = 0, int Periods = 0, int? InterestTreatment = null);

/// <summary>
/// <c>ScheduleSimulationInput</c>: la línea, el monto, las cuotas y, si vienen, periodicidad (el número es los días: 30 mensual), fechas,
/// sistema (1 cuota fija, 2 abono constante, 3 al vencimiento), extraordinarios, gracia, seguros opcionales y la persona simulada.
/// </summary>
public sealed record SimulacionRequest(
    Guid CreditLinePublicId,
    decimal Amount,
    int Installments,
    int? Periodicity = null,
    DateOnly? DisbursementDate = null,
    DateOnly? FirstDueDate = null,
    int? AmortizationSystem = null,
    IReadOnlyList<AbonoExtraordinarioRequest>? Extraordinary = null,
    GraciaDeSimulacionRequest? Grace = null,
    IReadOnlyList<string>? Insurances = null,
    Guid? PersonPublicId = null);

public sealed record UsuraDeSimulacionDto(string? ModalityKey, decimal? Rate, DateOnly Date, bool Passes, decimal? Headroom, Guid? NationalValuePublicId);

public sealed record TotalesDeSimulacionDto(decimal Principal, decimal Interest, decimal Insurance, decimal Other, decimal ToPay);

public sealed record CuotaSimuladaDto(int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Other, decimal Total,
    decimal BalanceAfter);

public sealed record TramoDeSimulacionDto(string Segment, int Days, decimal DailyRate, decimal Base);

/// <summary>Un aviso que no detiene nada (api.md §1.9), p. ej. la usura excedida en una simulación.</summary>
public sealed record AvisoDto(string Code, string Message);

/// <summary><c>ScheduleSimulationDto</c>: el plan cuota a cuota con sus tasas, la usura, los totales, la explicación y la huella.</summary>
public sealed record SimulacionDto(
    string RulesVersion,
    Guid CreditLinePublicId,
    string CreditLineCode,
    string CreditLineName,
    Guid CreditLineVersionPublicId,
    decimal Amount,
    int InstallmentCount,
    int Periodicity,
    int AmortizationSystem,
    DateOnly DisbursementDate,
    int TermMonths,
    decimal EffectiveAnnualRate,
    decimal NominalPeriodRate,
    decimal EffectiveRateWithCharges,
    UsuraDeSimulacionDto Usury,
    TotalesDeSimulacionDto Totals,
    IReadOnlyList<CuotaSimuladaDto> Installments,
    IReadOnlyList<TramoDeSimulacionDto> Explanation,
    IReadOnlyList<string> ExplanationLines,
    IReadOnlyList<AvisoDto> Warnings,
    string SimulationSha256);

/// <summary>El envío de la simulación por correo a la persona.</summary>
public sealed record EnviarSimulacionRequest(SimulacionRequest Simulation, Guid RecipientPersonPublicId);

public sealed record SimulacionEnviadaDto(Guid DebtorMessagePublicId, string Status);

// ------------------------------------------------------------------------------------------- §4.2 solicitudes --

/// <summary>La capacidad declarada (mensual).</summary>
public sealed record CapacidadRequest(decimal? MonthlyIncome = null, decimal? MonthlyExpenses = null, decimal? ExternalInstallments = null);

/// <summary>Un codeudor (rol 2) o avalista (3) con lo que declara.</summary>
public sealed record ParteDeSolicitudRequest(Guid PersonPublicId, decimal? MonthlyIncome = null, decimal? MonthlyExpenses = null,
    decimal? ExternalInstallments = null, int Role = 2);

/// <summary>La nómina propia (en K1 no hay pagaduría externa).</summary>
public sealed record NominaDeSolicitudRequest(Guid? PayrollAgreementPublicId = null, string? EmployeeId = null, Guid? CertificationAttachmentPublicId = null,
    DateOnly? CertificationDate = null);

/// <summary>Una autorización de datos con su finalidad (1 tratamiento general, 2 trámite de crédito, 3 consulta a centrales, 4 reporte, 5 canal).</summary>
public sealed record AutorizacionDeDatosRequest(Guid PersonPublicId, int Purpose, int? ContactChannel = null, Guid? PolicyVersionPublicId = null,
    Guid? EvidenceAttachmentPublicId = null, DateTime? GrantedAt = null);

/// <summary><c>CreditApplicationInput</c> (api.md §4.2). Se edita en la pantalla: es una clase con propiedades.</summary>
public sealed class SolicitudRequest
{
    public Guid ApplicantPersonPublicId { get; set; }
    public Guid OfficePublicId { get; set; }
    public Guid CreditLinePublicId { get; set; }
    public int Kind { get; set; } = 1;
    public string? Purpose { get; set; }
    public decimal Amount { get; set; }
    public int Installments { get; set; }
    public int? Periodicity { get; set; }
    public DateOnly? ExpectedDisbursementDate { get; set; }
    public DateOnly? FirstDueDate { get; set; }
    public int? AmortizationSystem { get; set; }
    public IReadOnlyList<string>? Insurances { get; set; }
    public int PaymentForm { get; set; } = 1;
    public NominaDeSolicitudRequest? Payroll { get; set; }
    public int? PrepaymentPreference { get; set; }
    public CapacidadRequest? ApplicantCapacity { get; set; }
    public List<ParteDeSolicitudRequest> CoDebtors { get; set; } = [];
    public List<AutorizacionDeDatosRequest> Authorizations { get; set; } = [];
    public bool PolicyExceptionRequested { get; set; }
    public string? PolicyExceptionReason { get; set; }
    public string? SimulationSha256 { get; set; }
}

public sealed record LineaDeSolicitudDto(Guid PublicId, string Code, string Name, Guid? VersionPublicId);

public sealed record OficinaDeSolicitudDto(Guid PublicId, string Name);

public sealed record ParteDeSolicitudDto(PersonaDeCarteraDto Person, int Role, int Order, decimal? MonthlyIncome, decimal? MonthlyExpenses,
    decimal? ExternalInstallments);

public sealed record RequisitoDeSolicitudDto(string Code, string Name, bool Mandatory, int? AppliesTo, int? MaxAgeDays);

public sealed record DocumentoDeSolicitudDto(Guid PublicId, string? RequirementCode, string Name, bool Mandatory, Guid? PersonPublicId,
    Guid AttachmentPublicId, DateOnly? DocumentDate, string? RejectionReason);

/// <summary>Una validación (FR-036): clase, etapa, persona, resultado (1 pasó, 2 advertencia, 3 bloquea, 4 no aplica, 5 no disponible).</summary>
public sealed record ValidacionDeSolicitudDto(int Kind, int Stage, PersonaDeCarteraDto? Person, int Outcome, bool IsExceptionable, string? ErrorCode,
    JsonElement? Detail, string EvidenceSha256, DateTime At);

public sealed record VariableDePuntajeCalculadaDto(string Code, decimal Points, decimal Contribution, string Explanation);

public sealed record PuntajeDeSolicitudDto(decimal Value, decimal? Minimum, bool BelowMinimum, IReadOnlyList<VariableDePuntajeCalculadaDto> Variables);

public sealed record DecisionDeSolicitudDto(Guid PublicId, int Outcome, string? Conditions, string? Reason, Guid? DecidedByUserPublicId, DateTime At);

public sealed record AprobacionDeSolicitudDto(decimal? Amount, string? Conditions, DateOnly? ExpiresAt, bool Expired);

/// <summary><c>CreditApplicationDto</c> (api.md §4.2): «vencida» y «bloqueada por listas» se calculan al leer.</summary>
public sealed record SolicitudDto(
    Guid PublicId,
    string? Number,
    int Kind,
    int Status,
    byte[] RowVersion,
    PersonaDeCarteraDto Applicant,
    int DebtorKind,
    OficinaDeSolicitudDto? Office,
    LineaDeSolicitudDto Line,
    string? Purpose,
    decimal Amount,
    int? Installments,
    int? TermMonths,
    int? Periodicity,
    int? AmortizationSystem,
    DateOnly? ExpectedDisbursementDate,
    DateOnly? FirstDueDate,
    int PaymentForm,
    int? PrepaymentPreference,
    bool PolicyExceptionRequested,
    string? SimulationSha256,
    IReadOnlyList<ParteDeSolicitudDto> Parties,
    IReadOnlyList<RequisitoDeSolicitudDto> Requirements,
    IReadOnlyList<DocumentoDeSolicitudDto> Documents,
    IReadOnlyList<ValidacionDeSolicitudDto> Checks,
    PuntajeDeSolicitudDto? Score,
    string? RoutingSha256,
    decimal? ConsolidatedExposure,
    bool IsRelatedParty,
    IReadOnlyList<DecisionDeSolicitudDto> Decisions,
    AprobacionDeSolicitudDto? Approval,
    bool BlockedByRestrictiveLists,
    DateTime? SubmittedAt,
    string CreatedBy,
    DateTime CreatedAt);

/// <summary>Una fila de la lista de solicitudes.</summary>
public sealed record SolicitudEnListaDto(
    Guid PublicId,
    string? Number,
    PersonaDeCarteraDto Applicant,
    LineaDeSolicitudDto Line,
    OficinaDeSolicitudDto? Office,
    decimal Amount,
    int? Installments,
    int Status,
    string? LevelCode,
    DateTime? SubmittedAt,
    DateOnly? ApprovalExpiresAt,
    bool ApprovalExpired);

/// <summary>Los filtros de <c>GET /applications</c>.</summary>
public sealed record FiltrosDeSolicitudes(
    int? Status = null,
    Guid? Line = null,
    Guid? Office = null,
    Guid? Applicant = null,
    string? Level = null,
    bool? Mine = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20);

/// <summary><c>GET /applications/{id}/routing</c>: lo que ve quien decide y la huella que compara.</summary>
public sealed record EnrutamientoDeSolicitudDto(
    int MatrixVersion,
    string EffectiveLevelCode,
    string EffectiveLevelName,
    int EffectiveLevelKind,
    IReadOnlyList<RazonDeEnrutamientoDto> Reasons,
    decimal ConsolidatedAmount,
    bool RelatedParty,
    IReadOnlyList<int> NonExceptionable,
    string ContentSha256);

public sealed record DocumentoDeSolicitudRequest(string? RequirementCode, Guid AttachmentPublicId, DateOnly? DocumentDate = null, Guid? PersonPublicId = null);

public sealed record VersionDeSolicitudRequest(byte[]? RowVersion);

public sealed record AnalisisDeSolicitudRequest(decimal? Income, decimal? Expenses, decimal? ExternalInstallments, string? Notes = null,
    string? Recommendation = null, Guid? PersonPublicId = null);

public sealed record RepetirValidacionesRequest(IReadOnlyList<string>? Checks = null);

/// <summary>Anular (<c>Annulment</c>) o desistir (<c>Withdrawal</c>) con motivo.</summary>
public sealed record CancelarSolicitudRequest(string Kind, string Reason);

// ------------------------------------------------------------------------------------------- §4.3 decisiones --

public sealed record GarantiaAdicionalRequest(int Kind, Guid? PersonPublicId = null);

/// <summary>Las condiciones menores: monto y cuotas que no pasan de lo pedido y garantías adicionales.</summary>
public sealed record CondicionesDeDecisionRequest(decimal? Amount = null, int? Installments = null, IReadOnlyList<GarantiaAdicionalRequest>? AdditionalCollaterals = null);

/// <summary><c>DecisionInput</c>: resultado 1 aprobar, 2 con condiciones, 3 negar, 4 devolver.</summary>
public sealed record DecisionRequest(int Outcome, string? Reason, CondicionesDeDecisionRequest? Conditions, string? ExpectedContentSha256);

public sealed record DecisionRegistradaDto(Guid DecisionPublicId, int Outcome, string SubjectStatus, DateOnly? ApprovalExpiresAt, decimal? ApprovedAmount,
    Guid? ResultingSubjectPublicId);

/// <summary>Un pendiente de «Mis pendientes»: <c>Route</c> dice si se decide aquí (individual) o en sesión.</summary>
public sealed record DecisionPendienteDto(
    string Subject,
    Guid SubjectPublicId,
    string? Number,
    string Summary,
    decimal Amount,
    string LevelCode,
    string LevelName,
    DateTime? Since,
    string Route,
    string? GoverningBody,
    bool CanDecide,
    string? ExcludedReason);

// ------------------------------------------------------------------------------------------- §4.4 centrales --

public sealed record ConsultaACentralRequest(Guid PersonPublicId, string BureauCode, string Purpose, Guid? ApplicationPublicId = null,
    Guid? CreditLimitPublicId = null);

public sealed record ConsultaACentralRegistradaDto(Guid BureauQueryPublicId, string Status, bool Simulated, DateTime QueriedAt, DateOnly ValidUntil, bool Reused);

public sealed record ResumenDeCentralDto(decimal? Score, int? NegativeMarks);

public sealed record InformeDeCentralDto(Guid AttachmentPublicId, string FileName);

public sealed record ConsultaACentralDto(
    Guid PublicId,
    PersonaDeCarteraDto Person,
    string BureauCode,
    int Purpose,
    string Status,
    bool Simulated,
    DateTime QueriedAt,
    DateOnly ValidUntil,
    ResumenDeCentralDto? Summary,
    Guid? ApplicationPublicId,
    InformeDeCentralDto? Artifact);

/// <summary>Un enlace firmado de 60 s (o, para lo cifrado por la aplicación, «por la API»).</summary>
public sealed record EnlaceDeCarteraDto(bool Direct, string? Url, DateTimeOffset? ExpiresAt);

// --------------------------------------------------------------------------------------- §4.5 listas restrictivas --

/// <summary>La persona de un resultado de listas (con su documento: sólo la ve el oficial).</summary>
public sealed record PersonaConDocumentoDeListasDto(Guid PersonPublicId, string FullName, string? DocumentNumber);

public sealed record CargaDeListaDto(string ListCode, int Version);

public sealed record CoincidenciaEnListaDto(string ListCode, string EntryName, decimal Score, string? Program, bool ByDocument);

/// <summary>El resultado de una consulta a listas: <c>Status</c> Blocked, Released o NoMatch (calculado).</summary>
public sealed record ResultadoDeListasDto(
    Guid PublicId,
    PersonaConDocumentoDeListasDto Person,
    int Context,
    Guid? ContextPublicId,
    IReadOnlyList<CargaDeListaDto> ListLoads,
    int Outcome,
    IReadOnlyList<CoincidenciaEnListaDto> Matches,
    string Status,
    string ChannelCode,
    bool Simulated,
    DateTime QueriedAt,
    UsuarioDeCarteraDto? ReleasedBy,
    DateTime? ReleasedAt,
    string? Reason);
