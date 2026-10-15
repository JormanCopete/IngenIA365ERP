namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, K3, US11 (T0891; contracts/api.md §11.1, §11.2, §11.4): los espejos de NoveltiesEndpoints, WaiversEndpoints y DebtorEventsEndpoints.
// Los enums SALEN de la API como número y así se leen aquí; las pantallas los muestran con TextosDeNovedades. Al ENTRAR se mandan por nombre
// («Extension», «Hardship»…), que la API acepta igual que el número. (nuevo)

// ------------------------------------------------------------------------------------------------ §11.1 novedades --

/// <summary>Las condiciones nuevas (<c>newTerms</c>): tasa EA en fracción, cuotas, periodicidad (días), día de pago, primer vencimiento, capitalizar,
/// créditos que recoge una refinanciación y la oficina nueva de un cambio de oficina.</summary>
public sealed record CondicionesDeNovedadDto(
    decimal? Rate = null,
    int? Installments = null,
    int? Periodicity = null,
    int? PaymentDay = null,
    DateOnly? FirstDueDate = null,
    bool CapitalizeInterest = false,
    IReadOnlyList<Guid>? PayoffLoans = null,
    Guid? BranchPublicId = null);

/// <summary>Pedir una novedad: <c>Mechanism</c> y <c>RequestMotive</c> por nombre.</summary>
public sealed record NovedadRequest(
    IReadOnlyList<Guid> LoanPublicIds,
    string Mechanism,
    string RequestMotive,
    CondicionesDeNovedadDto NewTerms,
    string Reason,
    Guid? InsolvencyProceedingPublicId = null);

public sealed record CreditoDeNovedadDto(Guid LoanPublicId, string LoanNumber, int Role);

public sealed record ConsentimientoDeNovedadDto(
    PersonaDeCarteraDto Party,
    int Role,
    int Status,
    Guid? CollateralPublicId,
    Guid? AttachmentPublicId,
    DateTime? At,
    int? RefusalResolution,
    string? ResolutionReason);

public sealed record CuotaSimuladaDeNovedadDto(int Number, DateOnly DueDate, int Days, decimal Principal, decimal Interest, decimal Insurance, decimal Balance);

public sealed record UsuraDeNovedadDto(string Verdict, decimal? EffectiveRate, decimal? UsuryRate, decimal? Margin);

/// <summary>La simulación (<c>POST /{id}/simulate</c>): causa, plan nuevo, calificación hoy y después, conteo, usura, concentración, validación previa.</summary>
public sealed record SimulacionDeNovedadDto(
    int Cause,
    string CauseExplanation,
    DateOnly Date,
    decimal PrincipalBalance,
    decimal CollectedPrincipal,
    decimal Capitalized,
    decimal NewPrincipal,
    int FirstInstallment,
    IReadOnlyList<CuotaSimuladaDeNovedadDto> NewSchedule,
    string RatingNow,
    string RatingAfter,
    int RestructuringCount,
    UsuraDeNovedadDto Usury,
    string? Concentration,
    string Prevalidation,
    IReadOnlyList<string> Warnings,
    string SimulationSha256);

/// <summary>El enrutamiento de una novedad o una condonación radicada.</summary>
public sealed record EnrutamientoDeAsuntoDto(
    string LevelCode,
    string LevelName,
    int LevelKind,
    string? GoverningBody,
    decimal Amount,
    bool RelatedParty,
    IReadOnlyList<RazonDeEnrutamientoDto> Reasons,
    string ContentSha256);

public sealed record DecisionDeAsuntoDto(Guid DecisionPublicId, int Outcome, string? Reason, DateTime DecidedAt, bool Collegiate, string? DecidedBy);

public sealed record NovedadResumenDto(
    Guid PublicId,
    string Number,
    IReadOnlyList<CreditoDeNovedadDto> Loans,
    int Mechanism,
    int Cause,
    int Status,
    string RequestedBy,
    DateTime RequestedAt,
    string? Level);

public sealed record NovedadDeCreditoDto(
    Guid PublicId,
    string Number,
    IReadOnlyList<CreditoDeNovedadDto> Loans,
    int Mechanism,
    int Cause,
    string CauseDetermination,
    int Status,
    string Reason,
    CondicionesDeNovedadDto NewTerms,
    SimulacionDeNovedadDto? Simulation,
    IReadOnlyList<ConsentimientoDeNovedadDto> Consents,
    EnrutamientoDeAsuntoDto? Routing,
    IReadOnlyList<DecisionDeAsuntoDto> Decisions,
    string RatingAtRequest,
    int? RestructuringCountAfter,
    Guid? InsolvencyProceedingPublicId,
    string RequestedBy,
    DateTime RequestedAt,
    Guid? AppliedOperationPublicId,
    Guid? NewLoanPublicId,
    string? NewLoanNumber,
    DateTime? AppliedAt,
    DateTime? CancelledAt,
    string? CancelReason);

/// <summary>Registrar el consentimiento de una parte: aceptar exige el adjunto firmado de la novedad.</summary>
public sealed record ConsentimientoRequest(Guid PartyPersonPublicId, bool Consented, Guid? AttachmentPublicId, Guid? AddendumAttachmentPublicId = null,
    Guid? CollateralPublicId = null);

/// <summary>Lo que decide quien aplica ante una parte que no consintió: <c>Decision</c> por nombre (<c>DoNotApply</c>,
/// <c>ApplyWithGuaranteeExtinguished</c>).</summary>
public sealed record SinConsentimientoRequest(Guid PartyPersonPublicId, string Decision, string Reason, Guid? CollateralPublicId = null,
    bool ApproveCoverageException = false);

public sealed record AplicarNovedadRequest(IReadOnlyList<SinConsentimientoRequest>? WithoutConsent = null, bool? AcknowledgeWarnings = null);

public sealed record NovedadAplicadaDto(
    Guid? OperationPublicId,
    string? OperationNumber,
    int? ScheduleVersion,
    Guid? NewLoanPublicId,
    string? NewLoanNumber,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Warnings);

// --------------------------------------------------------------------------------------------- §11.2 condonaciones --

/// <summary>Un concepto a condonar: <c>Concept</c> por nombre (<c>DefaultInterest</c>, <c>Principal</c>…).</summary>
public sealed record ConceptoACondonarRequest(string Concept, decimal Amount, int? InstallmentNumber = null);

public sealed record CondonacionRequest(Guid LoanPublicId, IReadOnlyList<ConceptoACondonarRequest> Concepts, string Reason, Guid? SupportAttachmentPublicId = null);

public sealed record LineaDeCondonacionDto(int Concept, int? InstallmentNumber, decimal Amount, decimal AvailableAtRequest);

public sealed record CondonacionDto(
    Guid PublicId,
    string Number,
    Guid LoanPublicId,
    string LoanNumber,
    int Status,
    DateOnly AsOfDate,
    string Reason,
    int DaysPastDueAtRequest,
    bool IsRelatedParty,
    decimal TotalAmount,
    IReadOnlyList<LineaDeCondonacionDto> Concepts,
    EnrutamientoDeAsuntoDto? Routing,
    IReadOnlyList<DecisionDeAsuntoDto> Decisions,
    string RequestedBy,
    DateTime RequestedAt,
    Guid? AppliedOperationPublicId,
    DateTime? AppliedAt);

public sealed record AplicarCondonacionRequest(bool? AcknowledgeWarnings = null);

public sealed record CondonacionAplicadaDto(Guid OperationPublicId, string OperationNumber, IReadOnlyList<string> Messages, decimal ImpairmentApplied,
    decimal ImpairmentShortfall, IReadOnlyList<string> Warnings);

// ----------------------------------------------------------------- §11.4 eventos del deudor, reclamación e insolvencia --

/// <summary>Un evento del deudor: <c>Kind</c> 7 fallecimiento, 6 retiro de la cooperativa, 9 embargo.</summary>
public sealed record EventoDelDeudorDto(
    Guid PublicId,
    PersonaDeCarteraDto Person,
    int Kind,
    DateOnly EventDate,
    string? Notes,
    UsuarioDeCarteraDto? RegisteredBy,
    DateTime RegisteredAt,
    Guid? Support,
    DateTime? LiftedAt,
    IReadOnlyList<string> Effects);

/// <summary>Registrar un evento: <c>Kind</c> por nombre (<c>Deceased</c>, <c>AssociateWithdrawal</c>, <c>Garnishment</c>).</summary>
public sealed record EventoDelDeudorRequest(Guid PersonPublicId, string Kind, DateOnly EventDate, Guid? SupportAttachmentPublicId,
    Guid? InsurerPersonPublicId = null, string? Notes = null);

public sealed record ReclamacionAlSeguroDto(
    Guid PublicId,
    Guid LoanPublicId,
    string LoanNumber,
    PersonaDeCarteraDto Insurer,
    PersonaDeCarteraDto Deceased,
    DateOnly DeathDate,
    int Status,
    string? ClaimNumber,
    DateOnly? FiledOn,
    decimal? PaidAmount,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string? CloseReason,
    Guid? PaymentOperationPublicId,
    bool Migrated);

/// <summary>El estado nuevo de una reclamación: <c>Denied</c> o <c>Withdrawn</c>.</summary>
public sealed record EstadoDeReclamacionRequest(string Status, string Reason, Guid? SupportAttachmentPublicId = null);

public sealed record ProcesoDeInsolvenciaDto(
    Guid PublicId,
    PersonaDeCarteraDto Person,
    int Status,
    int? Regime,
    string? Authority,
    string? CaseNumber,
    DateOnly StartDate,
    DateOnly? FailedOn,
    DateTime? ClosedAt,
    string? CloseReason,
    Guid? Support,
    IReadOnlyList<string> Loans,
    Guid? NoveltyPublicId,
    bool Migrated);

/// <summary>Abrir el proceso: <c>Regime</c> por nombre (<c>NaturalPersonDebtNegotiation</c>, <c>BusinessReorganization</c>).</summary>
public sealed record InsolvenciaRequest(Guid PersonPublicId, DateOnly StartDate, string Regime, string Authority, string? FileNumber, Guid? SupportAttachmentPublicId);

public sealed record FracasoDeInsolvenciaRequest(DateOnly Date, string Reason, Guid? SupportAttachmentPublicId, string? Regime = null, string? Authority = null);
