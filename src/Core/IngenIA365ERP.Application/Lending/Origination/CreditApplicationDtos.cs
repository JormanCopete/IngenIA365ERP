using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Origination;
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

/// <summary>
/// Un crédito propio a recoger con el desembolso (FR-046): total o parcial. 2026-11-27 (feature 028, R1): es la lista vieja; la solicitud la sigue
/// aceptando y la traduce a <see cref="Payoffs.PayoffInput"/> (<c>RecogidasPedidas.Unificar</c>). (nuevo)
/// </summary>
public sealed record OwnLoanPayoffInput(Guid LoanPublicId, string Mode = "Full", decimal? Amount = null);

/// <summary>
/// 2026-11-27 (feature 028, T023; api.md §2 <c>payoffProjection</c>): a qué fecha se proyectan las recogidas (D, la fecha proyectada de desembolso; nula,
/// la fecha esperada de desembolso de la solicitud o, sin ella, hoy) y hasta cuándo se tomó la información (R; nula, ahora). La pantalla manda lo que
/// simuló el asesor. (nuevo)
/// </summary>
public sealed record PayoffProjectionInput(DateOnly? ProjectedDisbursementDate = null, DateTime? RecordedAsOf = null);

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

    /// <summary>
    /// 2026-11-27 (feature 028, T023; R1): los productos que recoge el crédito nuevo con su desembolso. La lista vieja <see cref="OwnLoanPayoffs"/> se
    /// sigue aceptando; las dos con elementos es un cuerpo ilegible (<c>Request.BodyInvalid</c>). Sin ninguna, la solicitud no recoge nada (al corregir,
    /// se quitan las que tenía).
    /// </summary>
    public IReadOnlyList<Payoffs.PayoffInput>? Payoffs { get; init; }

    /// <summary>2026-11-27 (feature 028, T023): a qué fecha (D) y con la información hasta cuándo (R) se proyectan las recogidas.</summary>
    public PayoffProjectionInput? PayoffProjection { get; init; }

    public IReadOnlyList<AuthorizationInput>? Authorizations { get; init; }
    public bool PolicyExceptionRequested { get; init; }
    public string? PolicyExceptionReason { get; init; }
    public string? SimulationSha256 { get; init; }

    /// <summary>K6b (T1055): en una solicitud de vivienda, si el inmueble es de interés social (elige el límite de financiación VIS).</summary>
    public bool? IsSocialHousing { get; init; }

    /// <summary>
    /// 2026-11-23 (descuento condicionado): en una línea con opciones de descuento, el valor del bien, el proveedor y la opción. El monto debe ser
    /// el valor del bien menos lo que paga el fondo.
    /// </summary>
    public decimal? AssetValue { get; init; }
    public Guid? SupplierPersonPublicId { get; init; }
    public string? DiscountOptionCode { get; init; }

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
    string ContentSha256)
{
    /// <summary>2026-11-17: si quien consulta puede decidir el nivel efectivo individualmente; si no, <see cref="DecisionNote"/> dice por qué.</summary>
    public bool CanDecide { get; init; }

    public string? DecisionNote { get; init; }
}

public sealed record CreditApplicationDecisionDto(Guid PublicId, CreditDecisionOutcome Outcome, string? Conditions, string? Reason, Guid? DecidedByUserPublicId,
    DateTime At);

public sealed record CreditApplicationApprovalDto(decimal? Amount, string? Conditions, DateOnly? ExpiresAt, bool Expired);

/// <summary>
/// 2026-11-27 (feature 028, T027; api.md §2): un producto que la solicitud recoge, con lo proyectado al guardarla o radicarla: el número, el modo, el valor
/// pedido (parcial o abono), lo que se recogería a D con la información hasta R y su desglose, la cuota causada y qué se hace con ella, el modo de
/// intereses y, si otra solicitud viva lo recoge también, su número (<see cref="InOtherApplication"/>: «» si es un borrador sin número; es un aviso).
/// <see cref="Projected"/> es falso en las filas que copió la migración desde <c>PayoffRequest</c>: no tienen proyección hasta radicar. (nuevo)
/// </summary>
public sealed record CreditApplicationPayoffDto(
    PayoffProductKind Kind,
    Guid? ProductPublicId,
    string? Reference,
    string Number,
    PayoffMode Mode,
    decimal? RequestedAmount,
    decimal ProjectedAmount,
    Payoffs.PayoffBreakdownDto Breakdown,
    InstallmentTreatment? InstallmentTreatment,
    string? InterestMode,
    Payoffs.PayoffCurrentInstallmentDto? CurrentInstallment,
    DateOnly ProjectedDate,
    DateTime RecordedAsOf,
    bool Projected,
    string? InOtherApplication,
    int Order);

/// <summary>
/// 2026-11-27 (feature 028, T027; api.md §2 <c>payoffSummary</c>): lo que recoge la solicitud en total, el neto a girar (el monto aprobado, o el pedido,
/// menos lo recogido), la fecha proyectada de desembolso (D) y hasta cuándo se tomó la información (R). (nuevo)
/// </summary>
public sealed record CreditApplicationPayoffSummaryDto(decimal Total, decimal NetToDisburse, DateOnly ProjectedDate, DateTime RecordedAsOf);

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
    DateTime CreatedAt)
{
    /// <summary>K6b (bloque 2; FR-024): en una solicitud de vivienda, si el inmueble es de interés social (VIS); nulo en las demás.</summary>
    public bool? IsSocialHousing { get; init; }

    /// <summary>2026-11-23: los seguros elegidos (códigos), para volver a mandarlos al corregir.</summary>
    public IReadOnlyList<string> Insurances { get; init; } = [];

    /// <summary>2026-11-23: el motivo de la excepción a la política, si la pidió.</summary>
    public string? PolicyExceptionReason { get; init; }

    /// <summary>
    /// 2026-11-23: la solicitud trae condiciones que la pantalla no edita (cuotas extraordinarias, gracia, convenio de libranza): la pantalla de
    /// corrección no la toca para no borrarlas. 2026-11-27 (feature 028, T027): las recogidas ya no la encienden: la pantalla las edita.
    /// </summary>
    public bool UsesAdvancedTerms { get; init; }

    /// <summary>2026-11-27 (feature 028, T027): los productos que recoge, con lo proyectado, en su orden; vacío si no recoge nada.</summary>
    public IReadOnlyList<CreditApplicationPayoffDto> Payoffs { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, T027): el total recogido, el neto a girar, D y R; nulo si no recoge nada.</summary>
    public CreditApplicationPayoffSummaryDto? PayoffSummary { get; init; }

    /// <summary>2026-11-23: los grupos que se pueden corregir ahora (nombres de <c>GrupoEditableAlDevolver</c>); vacío si no se edita.</summary>
    public IReadOnlyList<string> EditableGroups { get; init; } = [];

    /// <summary>2026-11-23: lo que cambió desde la última devolución para corregir.</summary>
    public IReadOnlyList<CreditApplicationChangeDto> ChangesSinceReturn { get; init; } = [];

    /// <summary>2026-11-23: la compra con descuento condicionado (valor del bien, proveedor, opción y lo que paga el fondo); nula si no hay.</summary>
    public SubsidizedPurchaseDto? SubsidizedPurchase { get; init; }

    /// <summary>
    /// 2026-11-25 (pedido del dueño): las ediciones de la solicitud ya radicada, sin devolverla, de la más reciente a la más antigua: quién,
    /// cuándo, por qué y qué cambió (<c>LND_CreditApplicationEdits</c>).
    /// </summary>
    public IReadOnlyList<CreditApplicationEditDto> SubmittedEdits { get; init; } = [];
}

/// <summary>2026-11-25: una edición de la solicitud radicada: quién, cuándo, en qué estado, el motivo, lo que cambió y el nivel antes y después.</summary>
public sealed record CreditApplicationEditDto(DateTime EditedAt, string EditedBy, CreditApplicationStatus StatusAtEdit, string Reason,
    IReadOnlyList<CreditApplicationChangeDto> Changes, string? LevelBefore, string? LevelAfter);

/// <summary>Una compra con descuento condicionado: el bien, el proveedor, la opción y lo que ponen el crédito y el fondo.</summary>
public sealed record SubsidizedPurchaseDto(decimal AssetValue, PersonaRefDto? Supplier, string OptionCode, string OptionName, decimal Fraction,
    string? FundCode, decimal FundedAmount, decimal LoanAmount)
{
    /// <summary>2026-11-26: lo que paga el fondo en pesos fijos, si la opción no es un porcentaje.</summary>
    public decimal? FixedAmount { get; init; }
}

/// <summary>Un dato que cambió desde la devolución: su grupo, el campo y los valores, ya legibles.</summary>
public sealed record CreditApplicationChangeDto(string Group, string Field, string? Before, string? After);

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

        var recogidas = await RecogidasAsync(db, s, ct);

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
            listas, s.SubmittedAt, s.CreatedBy ?? string.Empty, s.CreatedAt)
        {
            IsSocialHousing = s.IsSocialHousing,
            Insurances = s.SelectedInsurances is { } seguros ? JsonSerializer.Deserialize<List<string>>(seguros, Json) ?? [] : [],
            PolicyExceptionReason = s.PolicyExceptionReason,
            // 2026-11-27 (feature 028, T027): las recogidas ya no la encienden (la pantalla las corrige).
            UsesAdvancedTerms = s.ExtraordinaryInstallments is not null || s.GraceKind is not null || s.PayrollAgreementId is not null,
            Payoffs = recogidas,
            PayoffSummary = Resumen(s, recogidas),
            ChangesSinceReturn = FotoDeSolicitud.Leer(s.ReturnSnapshot) is { } foto
                ? await CambiosAsync(db, foto, FotoDeSolicitud.De(s, partes, FotoDeSolicitud.TextoDeRecogidas(recogidas
                    .Select(r => new RecogidaEnLaFoto(r.Number, r.Mode, r.RequestedAmount, r.InstallmentTreatment)))) with
                {
                    PrimerPagoPorDefecto = s.ExpectedDisbursementDate is { } desembolso && s.Periodicity is { } periodicidad
                        ? FotoDeSolicitud.PrimerPagoSinFecha(desembolso, periodicidad, version?.PaymentDay)
                        : null,
                }, ct)
                : [],
            SubsidizedPurchase = await CompraAsync(db, s, version, ct),
            SubmittedEdits = await EdicionesAsync(db, s.Id, ct),
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, T027): las recogidas vivas de la solicitud, en su orden, con lo proyectado y el aviso de otra solicitud viva que recoge el
    /// mismo crédito (FR-009). El desglose y la cuota causada salen de lo guardado; no se vuelve a liquidar al leer.
    /// </summary>
    public static async Task<IReadOnlyList<CreditApplicationPayoffDto>> RecogidasAsync(IApplicationDbContext db, CreditApplication s, CancellationToken ct)
    {
        var filas = await (from f in db.CreditApplicationPayoffs.AsNoTracking()
                           join l in db.Loans.AsNoTracking().IgnoreQueryFilters() on f.LoanId equals l.Id into creditos
                           from l in creditos.DefaultIfEmpty()
                           where f.CreditApplicationId == s.Id && !f.IsDeleted
                           orderby f.Order, f.Id
                           select new { Fila = f, LoanPublicId = l == null ? (Guid?)null : l.PublicId })
            .ToListAsync(ct);
        if (filas.Count == 0) return [];
        var enOtras = await Payoffs.RecogidasDeLaSolicitud.EnOtrasSolicitudesAsync(db,
            filas.Where(f => f.Fila.LoanId is not null).Select(f => f.Fila.LoanId!.Value).ToList(), s.Id, null, ct);
        return filas.Select(x =>
        {
            var f = x.Fila;
            return new CreditApplicationPayoffDto(f.ProductKind, x.LoanPublicId, f.CommercialReceivableReference, f.ProductNumber, f.Mode, f.RequestedAmount,
                f.ProjectedAmount, Payoffs.PayoffBreakdownDto.De(f.Desglose()), f.InstallmentTreatment, f.InterestMode,
                f.CurrentInstallmentNumber is { } numero
                    ? new Payoffs.PayoffCurrentInstallmentDto(numero, null, f.CurrentInstallmentAmount ?? 0m, f.InstallmentTreatment ?? InstallmentTreatment.Include,
                        Choosable: false, Sent: null)
                    : null,
                f.ProjectedDate, f.RecordedAsOf, f.TieneProyeccion,
                f.LoanId is { } credito && enOtras.TryGetValue(credito, out var otra) ? otra : null, f.Order);
        }).ToList();
    }

    /// <summary>2026-11-27 (feature 028, T027): el total recogido, el neto a girar contra el monto aprobado (o el pedido), D y R.</summary>
    private static CreditApplicationPayoffSummaryDto? Resumen(CreditApplication s, IReadOnlyList<CreditApplicationPayoffDto> recogidas)
    {
        if (recogidas.Count == 0) return null;
        var total = recogidas.Sum(r => r.ProjectedAmount);
        return new CreditApplicationPayoffSummaryDto(total, (s.ApprovedAmount ?? s.RequestedAmount) - total, recogidas.Max(r => r.ProjectedDate),
            recogidas.Max(r => r.RecordedAsOf));
    }

    /// <summary>2026-11-25: el historial de ediciones de la radicada, de la más reciente a la más antigua, con quién y los niveles por su nombre.</summary>
    private static async Task<IReadOnlyList<CreditApplicationEditDto>> EdicionesAsync(IApplicationDbContext db, int solicitudId, CancellationToken ct)
    {
        var ediciones = await db.CreditApplicationEdits.AsNoTracking().Where(e => e.ApplicationId == solicitudId && !e.IsDeleted)
            .OrderByDescending(e => e.EditedAt).ThenByDescending(e => e.Id).ToListAsync(ct);
        if (ediciones.Count == 0) return [];
        var usuarioIds = ediciones.Select(e => e.EditedByUserId).Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarioIds.Contains(u.Id))
            .Select(u => new { u.Id, u.PersonId, u.Username }).ToListAsync(ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, usuarios.Where(u => u.PersonId is not null).Select(u => u.PersonId!.Value), ct);
        var nombres = usuarios.ToDictionary(u => u.Id,
            u => u.PersonId is { } p && personas.TryGetValue(p, out var persona) ? persona.FullName : u.Username);
        var nivelIds = ediciones.SelectMany(e => new[] { e.LevelBeforeId, e.LevelAfterId }).OfType<int>().Distinct().ToList();
        var niveles = await db.AttributionLevels.AsNoTracking().IgnoreQueryFilters().Where(n => nivelIds.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, n => n.Name, ct);
        return ediciones.Select(e => new CreditApplicationEditDto(e.EditedAt, nombres.GetValueOrDefault(e.EditedByUserId, "?"), e.StatusAtEdit, e.Reason,
            JsonSerializer.Deserialize<List<CreditApplicationChangeDto>>(e.Changes, Json) ?? [],
            e.LevelBeforeId is { } antes ? niveles.GetValueOrDefault(antes) : null,
            e.LevelAfterId is { } despues ? niveles.GetValueOrDefault(despues) : null)).ToList();
    }

    /// <summary>La compra con descuento de la solicitud, con la opción leída de la versión de la línea que selló.</summary>
    private static async Task<SubsidizedPurchaseDto?> CompraAsync(IApplicationDbContext db, CreditApplication s,
        Domain.Entities.Lending.Configuration.CreditLineVersion? version, CancellationToken ct)
    {
        if (s.AssetValue is not { } valor || s.DiscountOptionCode is not { } codigo) return null;
        var opcion = version?.LeerOpcionesDeDescuento().FirstOrDefault(o => o.Code == codigo);
        var proveedor = s.SupplierPersonId is { } id ? (await ReferenciasDeGobierno.PersonasAsync(db, [id], ct)).GetValueOrDefault(id) : null;
        var fraccion = opcion?.Fraction ?? 0m;
        var fondo = Domain.Lending.Configuration.OpcionesDeDescuento.Subsidio(valor, opcion);
        return new SubsidizedPurchaseDto(valor, proveedor, codigo, opcion?.Name ?? codigo, fraccion, opcion?.FundCode, fondo, valor - fondo) { FixedAmount = opcion?.Amount };
    }

    /// <summary>Las diferencias entre la foto de la devolución y la solicitud de ahora, con líneas, oficinas y personas por su nombre.</summary>
    internal static async Task<IReadOnlyList<CreditApplicationChangeDto>> CambiosAsync(IApplicationDbContext db, FotoDeSolicitud antes,
        FotoDeSolicitud ahora, CancellationToken ct)
    {
        // 2026-11-27 (feature 028, T026): una foto de antes de la feature trae las recogidas como el JSON de PayoffRequest.
        antes = await FotoDeSolicitud.NormalizarAsync(db, antes, ct);
        var diferencias = antes.Diferencias(ahora);
        if (diferencias.Count == 0) return [];
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == antes.LineaId || l.Id == ahora.LineaId)
            .ToDictionaryAsync(l => l.Id, l => $"{l.Code} · {l.Name}", ct);
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == antes.OficinaId || b.Id == ahora.OficinaId)
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, antes.Codeudores.Concat(ahora.Codeudores).Select(p => p.PersonId)
            .Concat(new[] { antes.ProveedorId, ahora.ProveedorId }.OfType<int>()), ct);
        string? Nombre(int? id) => id is { } x && personas.TryGetValue(x, out var r) ? r.FullName : null;
        string Partes(IReadOnlyList<ParteEnLaFoto> partes) => partes.Count == 0
            ? "Ninguno"
            : string.Join("; ", partes.Select(p => $"{(personas.TryGetValue(p.PersonId, out var r) ? r.FullName : "?")}"
                + $"{(p.Role == CreditPartyRole.Guarantor ? " (avalista)" : "")}"
                + $"{(p.Ingreso is { } i ? $", ingreso {i:N0}" : "")}{(p.Gasto is { } g ? $", gasto {g:N0}" : "")}"));

        return diferencias.Select(d => d.Grupo switch
        {
            GrupoEditableAlDevolver.Linea => new CreditApplicationChangeDto(GruposEditablesAlDevolver.Texto(d.Grupo), d.Campo,
                lineas.GetValueOrDefault(antes.LineaId), lineas.GetValueOrDefault(ahora.LineaId)),
            GrupoEditableAlDevolver.Oficina => new CreditApplicationChangeDto(GruposEditablesAlDevolver.Texto(d.Grupo), d.Campo,
                oficinas.GetValueOrDefault(antes.OficinaId), oficinas.GetValueOrDefault(ahora.OficinaId)),
            GrupoEditableAlDevolver.Codeudores => new CreditApplicationChangeDto(GruposEditablesAlDevolver.Texto(d.Grupo), d.Campo,
                Partes(antes.Codeudores), Partes(ahora.Codeudores)),
            _ when d.Campo == "Proveedor del bien" => new CreditApplicationChangeDto(GruposEditablesAlDevolver.Texto(d.Grupo), d.Campo,
                Nombre(antes.ProveedorId), Nombre(ahora.ProveedorId)),
            _ => new CreditApplicationChangeDto(GruposEditablesAlDevolver.Texto(d.Grupo), d.Campo, d.Antes, d.Despues),
        }).ToList();
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
