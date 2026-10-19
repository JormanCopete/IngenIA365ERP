using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

// Feature 014, US6 (T170, T171; api.md §5, §6): las entradas y respuestas del CDAT y de la tasa negociada. Las tasas viajan como fracción (0,10 = 10 % EA). (nuevo)

/// <summary>El destino de los intereses y del vencimiento: una cuenta de ahorro del titular o una orden de giro con su beneficiario. (nuevo)</summary>
public sealed record TermDepositDestinationInput(
    TermDepositDestinationKind Kind,
    Guid? AccountPublicId = null,
    Guid? PayeePersonPublicId = null,
    Guid? BankPublicId = null,
    byte? AccountType = null,
    string? AccountNumber = null);

/// <summary>El fondeo: por recaudador (el contrato de cuentas consigna el monto exacto) o desde otra cuenta de ahorro (con sus firmantes). (nuevo)</summary>
public sealed record TermDepositFundingInput(
    TermDepositFundingKind Kind,
    Guid? SourceAccountPublicId = null,
    IReadOnlyList<Guid>? Signers = null,
    Guid? ApprovalPublicId = null);

/// <summary>El margen pedido sobre la tabla (fracción, puntos EA) y por qué. (nuevo)</summary>
public sealed record TermDepositNegotiationInput(decimal RequestedMarginEa, string Reason);

/// <summary>El cuerpo de <c>POST /api/lending/term-deposits</c> (api.md §5, <c>ConstituteTermDepositInput</c>). (nuevo)</summary>
public sealed record ConstituteTermDepositInput
{
    public Guid LinePublicId { get; init; }
    public Guid HolderPersonPublicId { get; init; }
    public Guid OfficePublicId { get; init; }
    public HandlingMode HandlingMode { get; init; } = HandlingMode.Indistinct;
    public IReadOnlyList<MemberAccountPartyInput> Parties { get; init; } = [];
    public IReadOnlyList<MemberAccountBeneficiaryInput> Beneficiaries { get; init; } = [];
    public Guid? BeneficiaryRecordAttachmentPublicId { get; init; }
    public decimal Amount { get; init; }
    public int TermDays { get; init; }
    public RepaymentPeriodicity InterestPeriodicity { get; init; } = RepaymentPeriodicity.SinglePayment;
    public InterestTiming Timing { get; init; } = InterestTiming.Arrears;
    public DateOnly? StartDate { get; init; }
    public TermDepositDestinationInput PaymentDestination { get; init; } = new(TermDepositDestinationKind.Account);
    public MaturityInstruction MaturityInstruction { get; init; } = MaturityInstruction.Rollover;
    public TermDepositFundingInput Funding { get; init; } = new(TermDepositFundingKind.Collector);
    public TermDepositNegotiationInput? Negotiation { get; init; }
    public string? Channel { get; init; }
    public string? ExpectedPreviewSha256 { get; init; }
}

/// <summary>Un período del plan (api.md §5, <c>schedule</c>): fechas, días, tasa del período, interés, retención estimada, neto y, si se pagó, cuándo. (nuevo)</summary>
public sealed record TermDepositScheduleLineDto(
    int Number,
    DateOnly From,
    DateOnly To,
    DateOnly PaymentDate,
    int Days,
    decimal Rate,
    decimal Interest,
    decimal WithholdingEstimate,
    decimal Net,
    DateOnly? PaidOn = null,
    Guid? OperationPublicId = null);

/// <summary>La retención estimada del plan: si el umbral diario alcanza al CDAT y el valor (api.md §5). (nuevo)</summary>
public sealed record TermDepositWithholdingEstimateDto(bool AppliesThreshold, decimal Amount);

/// <summary>Lo que la línea admite negociar y si cabe en la atribución de quien pregunta (api.md §5, <c>negotiation</c>). (nuevo)</summary>
public sealed record TermDepositNegotiationDto(
    bool Allowed,
    Guid? RulePublicId,
    string? RuleCode,
    decimal? MaxMarginEa,
    bool WithinUserAttribution,
    string? Code,
    string Explanation,
    string? LevelCode);

/// <summary>La simulación de un CDAT (api.md §5, <c>POST /term-deposits/simulate</c>). (nuevo)</summary>
public sealed record TermDepositSimulationDto(
    decimal TableRate,
    decimal Rate,
    IReadOnlyList<TermDepositScheduleLineDto> Schedule,
    decimal TotalInterest,
    TermDepositWithholdingEstimateDto WithholdingEstimate,
    DateOnly StartDate,
    DateOnly MaturityDate,
    DateOnly PaymentDate,
    IssueTermBand IssueTermBand,
    TermDepositNegotiationDto? Negotiation,
    string Fingerprint);

/// <summary>Lo negociado sellado (api.md §5, <c>negotiated</c>). (nuevo)</summary>
public sealed record TermDepositNegotiatedDto(decimal MarginEa, Guid? RulePublicId, string? ApprovedBy, string? Reason);

/// <summary>Una versión de condiciones del CDAT (api.md §5, <c>currentTerms</c> y <c>termsHistory</c>). (nuevo)</summary>
public sealed record TermDepositTermsDto(
    int Version,
    string TitleNumber,
    AccountTermsCause Cause,
    decimal Amount,
    int TermDays,
    DateOnly StartDate,
    DateOnly MaturityDate,
    DateOnly PaymentDate,
    decimal EffectiveAnnual,
    decimal TableRate,
    TermDepositNegotiatedDto? Negotiated,
    RepaymentPeriodicity InterestPeriodicity,
    InterestTiming Timing,
    InterestDestination? PaymentDestination,
    MaturityInstruction? MaturityInstruction,
    DateOnly? GraceUntil,
    IssueTermBand? IssueTermBand,
    int RolloverCount,
    string RulesVersion);

/// <summary>La propuesta de un CDAT que espera el fondeo. (nuevo)</summary>
public sealed record TermDepositProposalDto(
    decimal Amount,
    int TermDays,
    RepaymentPeriodicity InterestPeriodicity,
    InterestTiming Timing,
    decimal TableRate,
    decimal? MarginEa,
    decimal Rate,
    TermDepositFundingKind Funding,
    MaturityInstruction MaturityInstruction,
    DateOnly RequestedStart);

/// <summary>Un título emitido del CDAT (los emite US13; api.md §5, <c>titles</c>). (nuevo)</summary>
public sealed record TermDepositTitleDto(Guid DocumentPublicId, string Number, int? TermsVersion, string Status, DateTime IssuedAt);

/// <summary>Un CDAT (api.md §5, <c>TermDepositDto</c>). (nuevo)</summary>
public sealed record TermDepositDto(
    MemberAccountDto Account,
    TermDepositTermsDto? CurrentTerms,
    TermDepositProposalDto? Proposal,
    IReadOnlyList<TermDepositTermsDto> TermsHistory,
    IReadOnlyList<TermDepositTitleDto> Titles,
    MemberAccountStatus Status,
    NegotiatedRateRequestDto? PendingNegotiation,
    MaturityInstruction? EffectiveInstruction);

/// <summary>Un CDAT en la lista (api.md §5, <c>GET /term-deposits</c>). (nuevo)</summary>
public sealed record TermDepositListItemDto(
    Guid PublicId,
    string Number,
    string? TitleNumber,
    MemberAccountPersonDto Holder,
    LendingLineRefDto Line,
    decimal Amount,
    int? TermDays,
    decimal? EffectiveAnnual,
    decimal? NegotiatedMargin,
    IssueTermBand? IssueTermBand,
    DateOnly OpenedOn,
    DateOnly? MaturityDate,
    MemberAccountStatus Status,
    MaturityInstruction? Instruction,
    int RolloverCount);

/// <summary>Lo que vence un día por plazo de emisión, sin titulares (api.md §5, <c>maturity-calendar</c>). (nuevo)</summary>
public sealed record TermDepositMaturityDayDto(DateOnly Date, IssueTermBand IssueTermBand, int Count, decimal Principal);

/// <summary>Una solicitud de tasa negociada (api.md §6). (nuevo)</summary>
public sealed record NegotiatedRateRequestDto(
    Guid PublicId,
    Guid AccountPublicId,
    string AccountNumber,
    MemberAccountPersonDto Holder,
    decimal Amount,
    int? TermDays,
    decimal TableRate,
    decimal RequestedMarginEa,
    decimal MaxMarginEa,
    string Rule,
    string RequestedBy,
    NegotiatedRateRequestStatus Status,
    string? LevelCode,
    string? RoutingKind,
    string Reason,
    DateTime RequestedAt,
    string? DecidedBy,
    DateTime? DecidedAt,
    string? DecisionReason)
{
    /// <summary>El costo frente a la tabla: el margen sobre el monto en un año (informativo).</summary>
    public decimal CostVsTable => decimal.Round(Amount * RequestedMarginEa, 2, MidpointRounding.AwayFromZero);
}
