using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

// Feature 013, US2 (T0396; api.md §4.2): la forma de la solicitud de crédito. Propiedades en inglés (C26); personas, oficinas, líneas,
// adjuntos y créditos por PublicId (Principio VI). (nuevo)

/// <summary>Una parte de la solicitud (codeudor o avalista) con lo que declara de capacidad (FR-036; extensión aditiva de api.md §4.2). (nuevo)</summary>
public sealed record CreditApplicationPartyInput(
    Guid PersonPublicId,
    decimal? MonthlyIncome = null,
    decimal? MonthlyExpenses = null,
    decimal? ExternalInstallments = null,
    CreditPartyRole Role = CreditPartyRole.CoDebtor);

/// <summary>La capacidad declarada del solicitante: ingreso, gasto y cuotas con terceros, mensuales (FR-036). (nuevo)</summary>
public sealed record CapacityInput(decimal? MonthlyIncome = null, decimal? MonthlyExpenses = null, decimal? ExternalInstallments = null);

/// <summary>La libranza de la solicitud (api.md §4.2): en K1 sólo la nómina propia; la pagaduría externa llega con K2. (nuevo)</summary>
public sealed record PayrollInput(
    Guid? PayrollAgreementPublicId = null,
    string? EmployeeId = null,
    Guid? CertificationAttachmentPublicId = null,
    DateOnly? CertificationDate = null);

/// <summary>Una garantía pedida (api.md §4.2): en K1 sólo el codeudor, que va como parte. (nuevo)</summary>
public sealed record CollateralInput(CollateralKind Kind, Guid? PersonPublicId = null);

/// <summary>Un crédito propio a recoger con el desembolso (FR-046): total o parcial. (nuevo)</summary>
public sealed record OwnLoanPayoffInput(Guid LoanPublicId, string Mode = "Full", decimal? Amount = null);

/// <summary>Una autorización de datos que llega con la solicitud (FR-006, T69): se agrega en la misma transacción si no está vigente. (nuevo)</summary>
public sealed record AuthorizationInput(
    Guid PersonPublicId,
    DataAuthorizationPurpose Purpose,
    DataAuthorizationChannel? ContactChannel = null,
    Guid? PolicyVersionPublicId = null,
    Guid? EvidenceAttachmentPublicId = null,
    DateTime? GrantedAt = null);

/// <summary>
/// La solicitud que arma el asesor (api.md §4.2 <c>CreditApplicationInput</c>): el solicitante, la oficina, la línea y lo simulado, la forma de
/// pago, la preferencia de abono a capital, los codeudores, las autorizaciones y la huella de la simulación que se mostró. (nuevo)
/// </summary>
public sealed record CreditApplicationInput
{
    public Guid ApplicantPersonPublicId { get; init; }
    public Guid OfficePublicId { get; init; }
    public Guid CreditLinePublicId { get; init; }
    public ApplicationKind Kind { get; init; } = ApplicationKind.Loan;
    public string? Purpose { get; init; }
    public decimal Amount { get; init; }
    public int Installments { get; init; }
    public RepaymentPeriodicity? Periodicity { get; init; }
    public DateOnly? ExpectedDisbursementDate { get; init; }
    public DateOnly? FirstDueDate { get; init; }
    public AmortizationSystem? AmortizationSystem { get; init; }
    public IReadOnlyList<ScheduleExtraordinaryInput>? Extraordinary { get; init; }
    public ScheduleGraceInput? Grace { get; init; }
    public IReadOnlyList<string>? Insurances { get; init; }
    public RepaymentChannel PaymentForm { get; init; } = RepaymentChannel.Cashier;
    public PayrollInput? Payroll { get; init; }
    public PrepaymentChoice? PrepaymentPreference { get; init; }
    public CapacityInput? ApplicantCapacity { get; init; }
    public IReadOnlyList<CreditApplicationPartyInput>? CoDebtors { get; init; }
    public IReadOnlyList<CollateralInput>? Collaterals { get; init; }
    public IReadOnlyList<OwnLoanPayoffInput>? OwnLoanPayoffs { get; init; }
    public IReadOnlyList<AuthorizationInput>? Authorizations { get; init; }
    public bool PolicyExceptionRequested { get; init; }
    public string? PolicyExceptionReason { get; init; }
    public string? SimulationSha256 { get; init; }

    /// <summary>La simulación que corresponde a esta solicitud (las mismas entradas del simulador).</summary>
    public ScheduleSimulationInput Simulacion() => new(CreditLinePublicId, Amount, Installments, Periodicity, ExpectedDisbursementDate, FirstDueDate,
        AmortizationSystem, Extraordinary, Grace, Insurances, ApplicantPersonPublicId);
}

public sealed record CreditApplicationLineDto(Guid PublicId, string Code, string Name, Guid? VersionPublicId);

public sealed record CreditApplicationOfficeDto(Guid PublicId, string Name);

public sealed record CreditApplicationPartyDto(PersonaRefDto Person, CreditPartyRole Role, int Order, decimal? MonthlyIncome, decimal? MonthlyExpenses,
    decimal? ExternalInstallments);

public sealed record CreditApplicationDocumentDto(Guid PublicId, string? RequirementCode, string Name, bool Mandatory, Guid? PersonPublicId,
    Guid AttachmentPublicId, DateOnly? DocumentDate, string? RejectionReason);

/// <summary>Un documento que pide la línea, con el que lo cumple si ya se adjuntó (api.md §4.2 <c>documents[]</c>).</summary>
public sealed record CreditApplicationRequirementDto(string Code, string Name, bool Mandatory, CreditPartyRole? AppliesTo, int? MaxAgeDays);

public sealed record CreditApplicationCheckDto(ApplicationCheckKind Kind, CheckStage Stage, PersonaRefDto? Person, CheckOutcome Outcome,
    bool IsExceptionable, string? ErrorCode, JsonElement? Detail, string EvidenceSha256, DateTime At);

public sealed record CreditScoreVariableDto(string Code, decimal Points, decimal Contribution, string Explanation);

public sealed record CreditApplicationScoreDto(decimal Value, decimal? Minimum, bool BelowMinimum, IReadOnlyList<CreditScoreVariableDto> Variables);

/// <summary>El enrutamiento de la solicitud (api.md §4.2 <c>GET /{id}/routing</c>): lo que el decisor ve y la huella que compara.</summary>
public sealed record CreditApplicationRoutingDto(
    int MatrixVersion,
    string EffectiveLevelCode,
    string EffectiveLevelName,
    AttributionLevelKind EffectiveLevelKind,
    IReadOnlyList<RoutingReasonDto> Reasons,
    decimal ConsolidatedAmount,
    bool RelatedParty,
    IReadOnlyList<MotivoNoExceptuable> NonExceptionable,
    string ContentSha256);

public sealed record CreditApplicationDecisionDto(Guid PublicId, CreditDecisionOutcome Outcome, string? Conditions, string? Reason, Guid? DecidedByUserPublicId,
    DateTime At);

public sealed record CreditApplicationApprovalDto(decimal? Amount, string? Conditions, DateOnly? ExpiresAt, bool Expired);

/// <summary>La solicitud (api.md §4.2 <c>CreditApplicationDto</c>). «Vencida» y «bloqueada por listas» se calculan al leer (C53).</summary>
public sealed record CreditApplicationDto(
    Guid PublicId,
    string? Number,
    ApplicationKind Kind,
    CreditApplicationStatus Status,
    byte[] RowVersion,
    PersonaRefDto Applicant,
    DebtorKind DebtorKind,
    CreditApplicationOfficeDto? Office,
    CreditApplicationLineDto Line,
    string? Purpose,
    decimal Amount,
    int? Installments,
    int? TermMonths,
    RepaymentPeriodicity? Periodicity,
    AmortizationSystem? AmortizationSystem,
    DateOnly? ExpectedDisbursementDate,
    DateOnly? FirstDueDate,
    RepaymentChannel PaymentForm,
    PrepaymentChoice? PrepaymentPreference,
    bool PolicyExceptionRequested,
    string? SimulationSha256,
    IReadOnlyList<CreditApplicationPartyDto> Parties,
    IReadOnlyList<CreditApplicationRequirementDto> Requirements,
    IReadOnlyList<CreditApplicationDocumentDto> Documents,
    IReadOnlyList<CreditApplicationCheckDto> Checks,
    CreditApplicationScoreDto? Score,
    string? RoutingSha256,
    decimal? ConsolidatedExposure,
    bool IsRelatedParty,
    IReadOnlyList<CreditApplicationDecisionDto> Decisions,
    CreditApplicationApprovalDto? Approval,
    bool BlockedByRestrictiveLists,
    DateTime? SubmittedAt,
    string CreatedBy,
    DateTime CreatedAt);

/// <summary>Una fila de la lista de solicitudes (api.md §4.2 <c>CreditApplicationListItemDto</c>).</summary>
public sealed record CreditApplicationListItemDto(
    Guid PublicId,
    string? Number,
    PersonaRefDto Applicant,
    CreditApplicationLineDto Line,
    CreditApplicationOfficeDto? Office,
    decimal Amount,
    int? Installments,
    CreditApplicationStatus Status,
    string? LevelCode,
    DateTime? SubmittedAt,
    DateOnly? ApprovalExpiresAt,
    bool ApprovalExpired);

/// <summary>Arma el DTO de una solicitud con todo lo suyo. (nuevo)</summary>
public static class ConsultasDeSolicitudes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<CreditApplicationDto> DtoAsync(IApplicationDbContext db, CreditApplication s, DateOnly hoy, CancellationToken ct)
    {
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().FirstAsync(l => l.Id == s.CreditLineId, ct);
        var version = await db.CreditLineVersions.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == s.CreditLineVersionId, ct);
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == s.BranchId)
            .Select(b => new CreditApplicationOfficeDto(b.PublicId, b.Name)).FirstOrDefaultAsync(ct);
        var partes = await db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == s.Id && !p.IsDeleted).OrderBy(p => p.Order)
            .ToListAsync(ct);
        var requisitos = await db.CreditLineDocumentRequirements.AsNoTracking().Where(r => r.CreditLineVersionId == s.CreditLineVersionId)
            .OrderBy(r => r.Order).ToListAsync(ct);
        var documentos = await db.CreditApplicationDocuments.AsNoTracking().Where(d => d.ApplicationId == s.Id && !d.IsDeleted).OrderBy(d => d.Id)
            .ToListAsync(ct);
        var validaciones = await db.CreditApplicationChecks.AsNoTracking().Where(c => c.ApplicationId == s.Id && !c.IsDeleted).ToListAsync(ct);
        var puntaje = await db.CreditScores.AsNoTracking().Where(x => x.ApplicationId == s.Id && !x.IsDeleted)
            .OrderByDescending(x => x.CalculatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var decisiones = await db.CreditDecisions.AsNoTracking()
            .Where(d => d.SubjectType == LendingDecisionSubjects.CreditApplication && d.SubjectPublicId == s.PublicId && !d.IsDeleted)
            .OrderBy(d => d.DecidedAt).ToListAsync(ct);
        var listas = await db.ScreeningResults.AsNoTracking().AnyAsync(r => r.ApplicationId == s.Id && !r.IsDeleted
            && (r.Outcome == Domain.Enums.Lending.ScreeningOutcome.Match || r.Outcome == Domain.Enums.Lending.ScreeningOutcome.PossibleMatch) && r.ReleasedAt == null, ct);

        var personas = await ReferenciasDeGobierno.PersonasAsync(db, partes.Select(p => p.PersonId).Append(s.ApplicantPersonId), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, decisiones.Where(d => d.DecidedByUserId is not null).Select(d => d.DecidedByUserId!.Value), ct);
        var personaDeParte = partes.ToDictionary(p => p.Id, p => personas[p.PersonId]);
        var porId = requisitos.ToDictionary(r => r.Id);

        return new CreditApplicationDto(
            s.PublicId, s.Number, s.Kind, s.Status, s.RowVersion, personas[s.ApplicantPersonId], s.DebtorKind, oficina,
            new CreditApplicationLineDto(linea.PublicId, linea.Code, linea.Name, version?.PublicId), s.Purpose, s.RequestedAmount, s.Installments,
            s.TermMonths, s.Periodicity, s.AmortizationSystem, s.ExpectedDisbursementDate, s.FirstDueDate, s.RepaymentChannel, s.PrepaymentChoice,
            s.PolicyExceptionRequested, s.SimulationSha256,
            partes.Select(p => new CreditApplicationPartyDto(personas[p.PersonId], p.Role, p.Order, p.MonthlyIncome, p.MonthlyExpenses, p.ExternalInstallments))
                .ToList(),
            requisitos.Select(r => new CreditApplicationRequirementDto(r.Code, r.Name, r.IsMandatory, r.AppliesTo, r.MaxAgeDays)).ToList(),
            documentos.Select(d =>
            {
                var r = d.RequirementId is { } rid ? porId.GetValueOrDefault(rid) : null;
                return new CreditApplicationDocumentDto(d.PublicId, r?.Code, r?.Name ?? "Documento adicional", r?.IsMandatory ?? false,
                    d.PartyId is { } pid && personaDeParte.TryGetValue(pid, out var pp) ? pp.PersonPublicId : null, d.AttachmentPublicId, d.DocumentDate,
                    d.RejectionReason);
            }).ToList(),
            CreditApplicationCheck.Vigentes(validaciones).Select(c => new CreditApplicationCheckDto(c.Kind, c.Stage,
                c.PartyId is { } parte && personaDeParte.TryGetValue(parte, out var persona) ? persona : null, c.Outcome, c.IsExceptionable, c.ErrorCode,
                c.Detail is null ? null : JsonSerializer.Deserialize<JsonElement>(c.Detail), c.EvidenceSha256, c.EvaluatedAt)).ToList(),
            puntaje is null ? null : Puntaje(puntaje),
            s.RoutingSha256, s.ConsolidatedExposure, s.IsRelatedParty,
            decisiones.Select(d => new CreditApplicationDecisionDto(d.PublicId, d.Outcome, d.Conditions, d.Reason,
                d.DecidedByUserId is { } u && usuarios.TryGetValue(u, out var usuario) ? usuario.UserPublicId : null, d.DecidedAt)).ToList(),
            s.Status == CreditApplicationStatus.Approved || s.ApprovalDecisionId is not null
                ? new CreditApplicationApprovalDto(s.ApprovedAmount, s.ApprovedTerms, s.ApprovalValidUntil, s.AprobacionVencida(hoy))
                : null,
            listas, s.SubmittedAt, s.CreatedBy ?? string.Empty, s.CreatedAt);
    }

    private static CreditApplicationScoreDto Puntaje(CreditScore p)
    {
        var variables = new List<CreditScoreVariableDto>();
        using var doc = JsonDocument.Parse(p.Explanation);
        foreach (var v in doc.RootElement.EnumerateArray())
            variables.Add(new CreditScoreVariableDto(v.GetProperty("code").GetString() ?? string.Empty, v.GetProperty("points").GetDecimal(),
                v.GetProperty("contribution").GetDecimal(), v.GetProperty("explanation").GetString() ?? string.Empty));
        return new CreditApplicationScoreDto(p.Score, p.MinimumScore, p.BelowMinimum, variables);
    }

    /// <summary>JSON canónico de una lista para las columnas de texto de la solicitud.</summary>
    public static string? Serializar(object? valor) => valor is null ? null : JsonSerializer.Serialize(valor, Json);
}
