using System.Text.Json;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Application.Lending.Novelties;

// Feature 013, K3 (T0878–T0881; api.md §11.1): lo que entra y sale de las rutas de novedades. Los enums viajan por nombre o número y salen como
// número; los estados derivados y los textos, por nombre. (nuevo)

/// <summary>
/// Las condiciones nuevas que pide la novedad (api.md §11.1, <c>newTerms</c>): tasa (fracción EA), cuotas del plan nuevo desde la fecha de la
/// decisión, periodicidad, día de pago o primer vencimiento, si capitaliza el interés que se debe, en una refinanciación los créditos que recoge y, en
/// un cambio de oficina (T0883), la oficina nueva (<c>COR_Branches</c>). Se guarda como <c>ProposedTermsJson</c>. (nuevo)
/// </summary>
public sealed record NewTermsInput(
    decimal? Rate = null,
    int? Installments = null,
    RepaymentPeriodicity? Periodicity = null,
    int? PaymentDay = null,
    DateOnly? FirstDueDate = null,
    bool CapitalizeInterest = false,
    IReadOnlyList<Guid>? PayoffLoans = null,
    Guid? BranchPublicId = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ComoJson() => JsonSerializer.Serialize(this, Json);

    public static NewTermsInput DeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<NewTermsInput>(json, Json) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}

/// <summary>Un crédito de la novedad y su papel (api.md §11.1, <c>loans[]</c>). (nuevo)</summary>
public sealed record NoveltyLoanDto(Guid LoanPublicId, string LoanNumber, NoveltyLoanRole Role);

/// <summary>Un consentimiento de codeudor, avalista o propietario de garantía (api.md §11.1, <c>consents[]</c>). (nuevo)</summary>
public sealed record NoveltyConsentDto(
    PersonaRefDto Party,
    ConsentPartyRole Role,
    ConsentStatus Status,
    Guid? CollateralPublicId,
    Guid? AttachmentPublicId,
    DateTime? At,
    ConsentRefusalResolution? RefusalResolution,
    string? ResolutionReason);

/// <summary>Una cuota del plan simulado. (nuevo)</summary>
public sealed record NoveltyScheduleRowDto(int Number, DateOnly DueDate, int Days, decimal Principal, decimal Interest, decimal Insurance, decimal Balance);

/// <summary>El control de usura del plan nuevo (fracciones EA). (nuevo)</summary>
public sealed record NoveltyUsuryDto(string Verdict, decimal? EffectiveRate, decimal? UsuryRate, decimal? Margin);

/// <summary>
/// La simulación de una novedad (api.md §11.1, <c>POST /{id}/simulate</c>; FR-091): la causa con su explicación, el plan nuevo (las cuotas desde la
/// fecha de la decisión) con su capital y lo capitalizado, la calificación hoy y después (un reestructurado nunca mejora, FR-093), el conteo de
/// reestructuraciones, la usura, la concentración, la validación previa contable, los avisos y la huella (<c>HuellaDeCartera</c>) que se sella al
/// radicar. (nuevo)
/// </summary>
public sealed record NoveltySimulationDto(
    NoveltyCause Cause,
    string CauseExplanation,
    DateOnly Date,
    decimal PrincipalBalance,
    decimal CollectedPrincipal,
    decimal Capitalized,
    decimal NewPrincipal,
    int FirstInstallment,
    IReadOnlyList<NoveltyScheduleRowDto> NewSchedule,
    string RatingNow,
    string RatingAfter,
    int RestructuringCount,
    NoveltyUsuryDto Usury,
    string? Concentration,
    string Prevalidation,
    IReadOnlyList<string> Warnings,
    string SimulationSha256);

/// <summary>El enrutamiento de una novedad o una condonación radicada (api.md §4.3, <c>routing</c>). (nuevo)</summary>
public sealed record SubjectRoutingDto(
    string LevelCode,
    string LevelName,
    AttributionLevelKind LevelKind,
    string? GoverningBody,
    decimal Amount,
    bool RelatedParty,
    IReadOnlyList<RoutingReasonDto> Reasons,
    string ContentSha256);

/// <summary>Una decisión sobre el asunto (individual o de acta). (nuevo)</summary>
public sealed record SubjectDecisionDto(Guid DecisionPublicId, CreditDecisionOutcome Outcome, string? Reason, DateTime DecidedAt, bool Collegiate, string? DecidedBy);

/// <summary>Una novedad en la lista (api.md §11.1). (nuevo)</summary>
public sealed record LoanNoveltyListItemDto(
    Guid PublicId,
    string Number,
    IReadOnlyList<NoveltyLoanDto> Loans,
    NoveltyMechanism Mechanism,
    NoveltyCause Cause,
    LoanNoveltyStatus Status,
    string RequestedBy,
    DateTime RequestedAt,
    string? Level);

/// <summary>La novedad completa (api.md §11.1, <c>LoanNoveltyDto</c>). (nuevo)</summary>
public sealed record LoanNoveltyDto(
    Guid PublicId,
    string Number,
    IReadOnlyList<NoveltyLoanDto> Loans,
    NoveltyMechanism Mechanism,
    NoveltyCause Cause,
    string CauseDetermination,
    LoanNoveltyStatus Status,
    string Reason,
    NewTermsInput NewTerms,
    NoveltySimulationDto? Simulation,
    IReadOnlyList<NoveltyConsentDto> Consents,
    SubjectRoutingDto? Routing,
    IReadOnlyList<SubjectDecisionDto> Decisions,
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
    string? CancelReason,
    byte[]? RowVersion = null);

/// <summary>Lo que dejó aplicar una novedad (api.md §11.1, <c>POST /{id}/apply</c>). (nuevo)</summary>
public sealed record ApplyNoveltyResultDto(
    Guid? OperationPublicId,
    string? OperationNumber,
    int? ScheduleVersion,
    Guid? NewLoanPublicId,
    string? NewLoanNumber,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Warnings);

/// <summary>La decisión de quien aplica ante un codeudor o garante que no consintió (api.md §11.1, <c>withoutConsent[]</c>). (nuevo)</summary>
/// <summary>
/// La decisión de quien aplica ante una parte que no consintió (FR-092, api.md §11.1 <c>withoutConsent[]</c>). Con
/// <see cref="ApproveCoverageException"/> (bloque B2), si la cobertura restante no cumple la línea, quien aplica aprueba la excepción —con la
/// atribución del nivel de excepción de la matriz vigente— en lugar de recibir <c>Lending.Novelty.CoverageInsufficient</c>.
/// </summary>
public sealed record WithoutConsentInput(Guid PartyPersonPublicId, ConsentRefusalResolution Decision, string Reason, Guid? CollateralPublicId = null,
    bool ApproveCoverageException = false);

/// <summary>El motivo por el que el deudor pide la novedad (para la causa; no se guarda como columna). (nuevo)</summary>
public static class MotivosDeNovedad
{
    public static NoveltyRequestMotive Leer(NoveltyRequestMotive? motivo) => motivo ?? NoveltyRequestMotive.DebtorRequest;
}
