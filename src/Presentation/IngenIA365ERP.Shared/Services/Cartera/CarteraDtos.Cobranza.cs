namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, K4, US12 (T0957; contracts/api.md §14.3–§14.9): los espejos de CollectionStagesEndpoints, CollectorAssignmentsEndpoints,
// CollectionActionsEndpoints, PaymentAgreementsEndpoints, LegalProcessesEndpoints, CampaignsEndpoints, CollectionFeesEndpoints, PriorNoticesEndpoints y
// CreditReportClaimsEndpoints. Los enums SALEN de la API como número y así se leen aquí; las pantallas los muestran con TextosDeCobranza. Al ENTRAR se
// mandan por nombre («Phone», «ByCount»…), que la API acepta igual que el número. Ninguna pantalla deja elegir a alguien que no sea deudor o codeudor
// (LaCobranzaNuncaContactaReferencias). (nuevo)

// ------------------------------------------------------------------------------------------------ §14.5 etapas --

/// <summary>La regla de gasto de una etapa: base, tarifa (fracción), tope y momento.</summary>
public sealed record ReglaDeGastoDeEtapaDto(string Base, decimal Rate, decimal? Cap, string Moment);

/// <summary>Una etapa de cobranza de la versión vigente.</summary>
public sealed record EtapaDeCobranzaDto(
    Guid PublicId,
    string Code,
    string Name,
    int Order,
    int DaysFrom,
    int? DaysTo,
    bool IsLegal,
    IReadOnlyList<string> SuggestedActions,
    IReadOnlyList<string> TemplateCodes,
    ReglaDeGastoDeEtapaDto? FeeRule,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool ReviewPending);

/// <summary>La versión vigente de las etapas a una fecha.</summary>
public sealed record VersionDeEtapasDto(int Version, DateOnly? ValidFrom, IReadOnlyList<EtapaDeCobranzaDto> Stages);

/// <summary>Una etapa que se manda en una versión nueva.</summary>
public sealed record EtapaDeCobranzaRequest(
    string Code,
    string Name,
    int DaysFrom,
    int? DaysTo,
    bool IsLegal = false,
    IReadOnlyList<string>? SuggestedActions = null,
    IReadOnlyList<string>? TemplateCodes = null,
    ReglaDeGastoDeEtapaDto? FeeRule = null);

/// <summary>Una versión nueva de las etapas, con su vigencia y motivo.</summary>
public sealed record VersionDeEtapasRequest(IReadOnlyList<EtapaDeCobranzaRequest> Stages, DateOnly ValidFrom, string Reason);

// -------------------------------------------------------------------------------------------- §14.5 reparto --

/// <summary>Un usuario por su nombre.</summary>
public sealed record GestorDeCobranzaDto(Guid UserPublicId, string Name);

public sealed record PesoDeGestorDto(Guid UserPublicId, string Name, decimal Weight);

public sealed record PesoDeGestorRequest(Guid UserPublicId, decimal Weight);

/// <summary>Una regla de reparto con vigencia; <c>Distribution</c> 1 por cantidad, 2 por saldo.</summary>
public sealed record ReglaDeRepartoDto(
    Guid PublicId,
    int Priority,
    int Distribution,
    bool AssignWholeDebtor,
    Guid? OfficePublicId,
    string? StageCode,
    Guid? CreditLinePublicId,
    PersonaDeCarteraDto? Paymaster,
    decimal? MinBalance,
    decimal? MaxBalance,
    IReadOnlyList<PesoDeGestorDto> Collectors,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record ReglaDeRepartoRequest(
    int Priority,
    string Distribution,
    IReadOnlyList<PesoDeGestorRequest> Collectors,
    DateOnly ValidFrom,
    string Reason,
    Guid? OfficePublicId = null,
    string? StageCode = null,
    Guid? CreditLinePublicId = null,
    Guid? PaymasterPersonPublicId = null,
    decimal? MinBalance = null,
    decimal? MaxBalance = null,
    bool AssignWholeDebtor = false);

/// <summary>Una asignación de la historia (sólo inserción).</summary>
public sealed record AsignacionDeGestorDto(
    Guid PublicId,
    CreditoRefDto? Loan,
    PersonaDeCarteraDto Debtor,
    UsuarioDeCarteraDto Collector,
    DateOnly AssignedOn,
    Guid? RulePublicId,
    UsuarioDeCarteraDto? AssignedBy,
    string? Reason,
    Guid? PreviousAssignmentPublicId);

public sealed record AsignarGestorRequest(IReadOnlyList<Guid> LoanPublicIds, Guid CollectorUserPublicId, string Reason);

public sealed record FiltrosDeRepartoRequest(
    Guid? OfficePublicId = null,
    string? StageCode = null,
    Guid? CreditLinePublicId = null,
    Guid? PaymasterPersonPublicId = null,
    decimal? BalanceFrom = null,
    decimal? BalanceTo = null);

public sealed record RepartirRequest(string Basis, FiltrosDeRepartoRequest Filters, IReadOnlyList<Guid> CollectorUserPublicIds, bool PreviewOnly, string Reason);

public sealed record ItemDeRepartoDto(IReadOnlyList<CreditoRefDto> Loans, PersonaDeCarteraDto Debtor, UsuarioDeCarteraDto Collector, bool Kept);

public sealed record RepartoDto(bool PreviewOnly, IReadOnlyList<ItemDeRepartoDto> Assignments, IReadOnlyList<CreditoRefDto> Unassigned);

/// <summary>Un compromiso de la agenda; <c>Kind</c> es «Promise», «FollowUp», «AgreementInstallment» o «Stage».</summary>
public sealed record CompromisoDeAgendaDto(string Kind, CreditoRefDto Loan, PersonaDeCarteraDto Debtor, DateOnly Due, decimal? Amount, string Detail, string Route);

public sealed record AgendaDelGestorDto(DateOnly Date, UsuarioDeCarteraDto? Collector, IReadOnlyList<CompromisoDeAgendaDto> Items);

// ----------------------------------------------------------------------------------- §14.6 gestiones y promesas --

public sealed record PromesaDePagoDto(
    Guid PublicId,
    CreditoRefDto Loan,
    PersonaDeCarteraDto Debtor,
    DateOnly Date,
    decimal Amount,
    decimal Tolerance,
    int? Status,
    bool Voided,
    decimal Paid,
    DateOnly? KeptOn,
    Guid ActionPublicId,
    UsuarioDeCarteraDto Collector);

/// <summary>Una gestión; <c>ContactRole</c> 1 deudor, 2 codeudor, 3 pagaduría.</summary>
public sealed record GestionDeCobranzaDto(
    Guid PublicId,
    CreditoRefDto? Loan,
    DateTimeOffset At,
    int Channel,
    PersonaDeCarteraDto ContactedPerson,
    int ContactRole,
    bool IsDirectContact,
    int Result,
    string? Notes,
    UsuarioDeCarteraDto Collector,
    PromesaDePagoDto? Promise,
    Guid? VoidsActionPublicId,
    string? VoidReason,
    Guid? VoidedByPublicId,
    string? WindowDecision,
    IReadOnlyList<string> WindowReasons);

public sealed record PromesaRequest(DateOnly Date, decimal Amount);

public sealed record GestionRequest(
    Guid LoanPublicId,
    DateTimeOffset At,
    string Channel,
    Guid ContactedPersonPublicId,
    bool IsDirectContact,
    string ResultCode,
    string? Notes,
    PromesaRequest? Promise);

// --------------------------------------------------------------------------------------- §14.7 acuerdos de pago --

public sealed record CuotaDeAcuerdoDto(int Number, DateOnly DueDate, decimal Amount);

public sealed record CumplimientoDeAcuerdoDto(decimal Paid, decimal Total, int CoveredInstallments, int? BrokenInstallment, DateOnly? BrokenOn);

/// <summary>Un acuerdo; <c>Status</c> 1 propuesto, 2 aprobado, 3 rechazado, 4 cancelado; <c>Performance</c> 1 al día, 2 cumplido, 3 incumplido.</summary>
public sealed record AcuerdoDePagoDto(
    Guid PublicId,
    string Number,
    CreditoRefDto Loan,
    PersonaDeCarteraDto Debtor,
    int Status,
    int? Performance,
    decimal PastDueAmountAtAgreement,
    decimal TotalAmount,
    DateOnly StartDate,
    IReadOnlyList<CuotaDeAcuerdoDto> Installments,
    CumplimientoDeAcuerdoDto? Compliance,
    UsuarioDeCarteraDto CreatedBy,
    UsuarioDeCarteraDto? ApprovedBy,
    DateTime? ApprovedAt,
    string? RejectionReason,
    string? CancelReason);

public sealed record CuotaDeAcuerdoRequest(DateOnly DueDate, decimal Amount);

public sealed record AcuerdoDePagoRequest(Guid LoanPublicId, IReadOnlyList<CuotaDeAcuerdoRequest> Installments, string Reason);

/// <summary>Un motivo que puede faltar (al aprobar).</summary>
public sealed record MotivoOpcionalRequest(string? Reason);

// ---------------------------------------------------------------------------------------- §14.8 cobro jurídico --

public sealed record ActuacionJuridicaDto(
    Guid PublicId,
    DateOnly Date,
    int Kind,
    string Description,
    decimal? Amount,
    bool ChargeableToDebtor,
    bool Charged,
    Guid? AttachmentPublicId,
    UsuarioDeCarteraDto RecordedBy);

/// <summary>Un proceso jurídico; <c>Status</c> 1 asignado … 6 retirado (<see cref="TextosDeCobranza.EstadoJuridico"/>).</summary>
public sealed record ProcesoJuridicoDto(
    Guid PublicId,
    string Number,
    IReadOnlyList<CreditoRefDto> Loans,
    PersonaDeCarteraDto Debtor,
    PersonaDeCarteraDto Lawyer,
    string? Court,
    string? FileNumber,
    string? ProcessType,
    int Status,
    DateOnly StartDate,
    Guid? SuspendedByInsolvencyPublicId,
    ActuacionJuridicaDto? LastAction,
    DateTime? ClosedAt,
    string? CloseReason);

public sealed record AbrirProcesoJuridicoRequest(
    IReadOnlyList<Guid> LoanPublicIds,
    Guid LawyerPersonPublicId,
    string? Court,
    string? FileNumber,
    DateOnly StartDate,
    string? ProcessType = null);

public sealed record ActuacionJuridicaRequest(
    DateOnly Date,
    string Kind,
    string Description,
    decimal? Amount = null,
    bool ChargeableToDebtor = false,
    IReadOnlyList<Guid>? AttachmentPublicIds = null);

public sealed record CerrarProcesoJuridicoRequest(string Outcome, string Reason);

// ------------------------------------------------------------------------------------------- §14.9 campañas --

public sealed record FiltrosDeCampanaDto(
    string? StageCode = null,
    Guid? OfficePublicId = null,
    Guid? CreditLinePublicId = null,
    int? MinDaysPastDue = null,
    Guid? PaymasterPersonPublicId = null);

public sealed record ConteosDeCampanaDto(int Queued, int Deferred, int Excluded, int Sent, int Failed, int Cancelled);

/// <summary>Una campaña; <c>Status</c> 1 borrador, 2 programada, 3 en curso, 4 terminada, 5 cancelada.</summary>
public sealed record CampanaDeMensajesDto(
    Guid PublicId,
    string Code,
    string Name,
    string TemplateCode,
    int Channel,
    int Purpose,
    FiltrosDeCampanaDto Filters,
    DateTimeOffset ScheduledFor,
    int Status,
    int TargetCount,
    ConteosDeCampanaDto Counts,
    string? CancelReason);

public sealed record CampanaRequest(
    string Code,
    string Name,
    string TemplateCode,
    string Channel,
    string Purpose,
    FiltrosDeCampanaDto Filters,
    DateTimeOffset ScheduledFor);

public sealed record DestinatarioDeCampanaDto(CreditoRefDto Loan, PersonaDeCarteraDto Debtor, string Decision, DateTimeOffset? SendsAt);

public sealed record ExcluidoDeCampanaDto(CreditoRefDto Loan, PersonaDeCarteraDto Debtor, string Reason);

public sealed record VistaPreviaDeCampanaDto(IReadOnlyList<DestinatarioDeCampanaDto> Recipients, IReadOnlyList<ExcluidoDeCampanaDto> Excluded);

// --------------------------------------------------------------------------------------------------- gastos --

/// <summary>Un gasto informado o por informar que todavía no se causó; <c>NoticeStatus</c> es el último evento del aviso.</summary>
public sealed record GastoPorCausarDto(
    Guid NoticePublicId,
    CreditoRefDto Loan,
    PersonaDeCarteraDto Debtor,
    decimal Amount,
    string StageCode,
    string Origin,
    int NoticeStatus,
    DateTimeOffset? InformedAt,
    bool Causable);

public sealed record GastoCausadoDto(Guid NoticePublicId, CreditoRefDto Loan, decimal Amount, Guid OperationPublicId, string OperationNumber, Guid FactMessagePublicId);

public sealed record CausarGastosRequest(Guid PersonPublicId, IReadOnlyList<Guid>? NoticePublicIds = null);

// ------------------------------------------------------------------------------------------ §14.3 avisos previos --

public sealed record ComunicacionDeAvisoDto(
    Guid DebtorMessagePublicId,
    int Channel,
    int LastEvent,
    DateTimeOffset? SentAt,
    bool Evidence,
    bool ManualEvidence,
    string? TrackingNumber);

/// <summary>Un aviso previo por episodio; <c>Status</c> 1 pendiente, 2 en curso, 3 válido, 4 cerrado; <c>Origin</c> «Migration» o «Process».</summary>
public sealed record AvisoPrevioDto(
    Guid PublicId,
    CreditoRefDto Loan,
    PersonaDeCarteraDto Person,
    DateOnly EpisodeStart,
    int RequiredCommunications,
    int MinimumDays,
    decimal? ThresholdAmount,
    IReadOnlyList<ComunicacionDeAvisoDto> Communications,
    DateOnly? EvidenceDate,
    DateOnly? EarliestReportDate,
    int Status,
    DateOnly? EpisodeClosedOn,
    string Origin);

public sealed record EnviarAvisoRequest(string Channel);

public sealed record ConstanciaDeAvisoRequest(int CommunicationNumber, DateTimeOffset SentAt, string Carrier, string TrackingNumber, Guid AttachmentPublicId);

// ------------------------------------------------------------------------------------------- §14.4 reclamos --

public sealed record RegistrarReclamoRequest(
    Guid PersonPublicId,
    string Kind,
    DateOnly ReceivedAt,
    string Channel,
    string Description,
    Guid? LoanPublicId = null,
    IReadOnlyList<Guid>? AttachmentPublicIds = null);

public sealed record ResponderReclamoRequest(string Answer, bool UpholdsClaim, Guid? AttachmentPublicId = null);
