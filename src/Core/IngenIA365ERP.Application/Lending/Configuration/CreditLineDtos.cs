using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Scoring;

namespace IngenIA365ERP.Application.Lending.Configuration;

// Feature 013, US1 (T0353; api.md §3.1): lo que entra y sale de las rutas de líneas de crédito. Los nombres de bloque son los de
// CreditLineVersionInput del contrato; dentro, los de las columnas de data-model §1.3–§1.7 (las tasas como fracción, los montos en
// pesos, los plazos en meses: la columna es MinTermMonths/MaxTermMonths). Los enums viajan por nombre o número. (nuevo)

/// <summary>La tasa corriente: clase y, si es fija, su efectiva anual (fracción, hasta seis decimales).</summary>
public sealed record CurrentRateInput(RateKind Kind = RateKind.Fixed, decimal? EffectiveAnnual = null);

/// <summary>La tasa variable (D10): índice nacional y su clave, margen en la expresión del índice, piso, techo y reajuste.</summary>
public sealed record VariableRateInput(string IndexCode, string IndexKey, decimal Margin, decimal? Floor = null, decimal? Ceiling = null,
    RepaymentPeriodicity? ResetPeriodicity = null);

/// <summary>La mora: fija (EA) o relativa a la corriente (factor; pendiente de validar por la contadora, §7.7).</summary>
public sealed record DefaultRateInput(DefaultRateKind Kind = DefaultRateKind.RelativeToCurrent, decimal? EffectiveAnnual = null, decimal? Factor = null);

/// <summary>Una banda (data-model §1.4): rango de monto y plazo en meses, segmento y su tasa (fija) o su margen (variable).</summary>
public sealed record RateBandInput(
    decimal? AmountFrom = null,
    decimal? AmountTo = null,
    int? TermFrom = null,
    int? TermTo = null,
    DebtorKind? DebtorKind = null,
    bool? ForEmployees = null,
    int? MinSeniorityMonths = null,
    decimal? EffectiveAnnual = null,
    decimal? Margin = null);

public sealed record CreditLineRatesInput
{
    public CurrentRateInput Current { get; init; } = new();
    public VariableRateInput? Variable { get; init; }
    public DefaultRateInput Default { get; init; } = new(DefaultRateKind.RelativeToCurrent, null, 1m);
    public IReadOnlyList<RateBandInput> Bands { get; init; } = [];
}

public sealed record AmountRangeInput(decimal Min = 0m, decimal Max = 0m);

/// <summary>Plazos en meses (columnas <c>MinTermMonths</c>/<c>MaxTermMonths</c>) y el máximo de cuotas que informa la consulta de cupo.</summary>
public sealed record TermRangeInput(int MinMonths = 0, int MaxMonths = 0, int? MaxInstallments = null);

public sealed record GraceInput(GraceKind Kind = GraceKind.None, int? MaxInstallments = null, GraceInterestTreatment? InterestTreatment = null,
    int? DistributionInstallments = null);

public sealed record AmortizationInput
{
    public IReadOnlyList<AmortizationSystem> Systems { get; init; } = [AmortizationSystem.LevelPayment];
    public AmortizationSystem? Default { get; init; }
    public bool AllowsExtraordinary { get; init; }
    public decimal? MaxExtraordinaryFraction { get; init; }
    public GraceInput Grace { get; init; } = new();
    public bool AllowsTranches { get; init; }
}

/// <summary>El redondeo: unidad (0,01; 1; 10; 100; 1.000; nula = al centavo), modo y momento (data-model §0.4).</summary>
public sealed record RoundingInput(decimal? Unit = null, RoundingMode Mode = RoundingMode.AwayFromZero, RoundingMoment Moment = RoundingMoment.PerInstallment);

public sealed record CreditLimitInput(decimal? ContributionsMultiple = null, decimal? MaxIndebtedness = null, decimal? MinContributions = null);

public sealed record RequirementsInput(int? MinAssociateSeniorityMonths = null, int? MinAge = null, int? MaxAge = null, bool RequiresEmployment = false);

/// <summary>El concepto tributario del interés (D20, T44): gravado o no, con el soporte de la contadora subido como adjunto.</summary>
public sealed record InterestTaxConceptInput(bool Taxed, Guid? SupportAttachmentPublicId);

public sealed record DocumentRequirementInput(
    string Code,
    string Name,
    bool Mandatory = true,
    CreditPartyRole? AppliesTo = null,
    decimal? AmountFrom = null,
    decimal? AmountTo = null,
    DebtorKind? DebtorKind = null,
    RepaymentChannel? PaymentForm = null,
    int? MaxAgeDays = null);

/// <summary>Un seguro (data-model §1.6): la aseguradora es persona del maestro; si la cooperativa retiene parte de la prima, computa para usura.</summary>
public sealed record InsuranceInput(
    string Code,
    string Name,
    Guid InsurerPersonPublicId,
    InsuranceBase Base,
    decimal Rate,
    RepaymentPeriodicity Periodicity,
    bool Mandatory = true,
    decimal? RetainedFraction = null,
    bool SelfInsured = false,
    bool IsDebtorLifeInsurance = false,
    InsuranceAccrualTiming AccrualTiming = InsuranceAccrualTiming.AtDueDate);

public sealed record CollateralRuleInput(
    decimal? AmountFrom = null,
    decimal? AmountTo = null,
    int? TermFrom = null,
    int? TermTo = null,
    int CoDebtors = 0,
    IReadOnlyList<CollateralKind>? AdmittedKinds = null,
    decimal? MinCoverage = null,
    bool ContributionsCount = false,
    PromissoryNoteKind? PromissoryNoteKind = null);

/// <summary>
/// Lo que sella cada versión (api.md §3.1 <c>CreditLineVersionInput</c>). El bloque de puntaje no va aquí: lo cambia
/// <c>SetCreditLineScoringCommand</c> con su permiso, y una versión nueva conserva el de la vigente.
/// </summary>
public sealed record CreditLineVersionInput
{
    public DateOnly ValidFrom { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string ModalityCode { get; init; } = string.Empty;
    public string? SubsegmentCode { get; init; }
    public bool ForEmployees { get; init; }
    public bool AllowsNonAssociates { get; init; }
    public bool AllowsCommercialOrigin { get; init; }
    public DebtorKind? CommercialDefaultFor { get; init; }
    public CreditLineRatesInput Rates { get; init; } = new();
    public AmountRangeInput Amounts { get; init; } = new();
    public TermRangeInput Terms { get; init; } = new();
    public IReadOnlyList<RepaymentPeriodicity> Periodicities { get; init; } = [RepaymentPeriodicity.Monthly];
    public RepaymentPeriodicity? DefaultPeriodicity { get; init; }
    public byte? PaymentDay { get; init; }
    public byte? CutoffDay { get; init; }
    public AmortizationInput Amortization { get; init; } = new();
    public DayCountConvention DayCount { get; init; } = DayCountConvention.Actual365;
    public RateEquivalence RateEquivalence { get; init; } = RateEquivalence.Compound;
    public RoundingInput Rounding { get; init; } = new();
    public AllocationOrder AllocationOrder { get; init; } = AllocationOrder.OldestInstallmentFirst;
    public IReadOnlyList<LoanConcept>? ConceptOrder { get; init; }
    public bool SuspenseWithItsConcept { get; init; } = true;
    public bool AllowsSpecificInstallmentOutOfOrder { get; init; }
    public PrepaymentChoice CommercialNoteRecalculation { get; init; } = PrepaymentChoice.ReduceInstallment;
    public PaymentModality AdvanceOptionFirst { get; init; } = PaymentModality.ExtraordinaryPrincipal;
    public IReadOnlyList<RepaymentChannel> PaymentForms { get; init; } = [RepaymentChannel.Cashier];
    public RepaymentChannel? DefaultPaymentForm { get; init; }
    public RequirementsInput Requirements { get; init; } = new();
    public CreditLimitInput CreditLimit { get; init; } = new();
    public bool RequiresBureauQuery { get; init; }
    public InterestTaxConceptInput? InterestTaxConcept { get; init; }
    public IReadOnlyList<DocumentRequirementInput> Documents { get; init; } = [];
    public IReadOnlyList<InsuranceInput> Insurances { get; init; } = [];
    public IReadOnlyList<CollateralRuleInput> CollateralRules { get; init; } = [];

    /// <summary>
    /// El borrador del dominio. <paramref name="aseguradoras"/> traduce el <c>PublicId</c> de cada aseguradora a su Id interno;
    /// <paramref name="puntaje"/> y <paramref name="minimo"/> son los de la versión de la que parte (una versión nueva no cambia el puntaje).
    /// </summary>
    public BorradorDeVersionDeLinea Borrador(IReadOnlyDictionary<Guid, int> aseguradoras, string? puntaje = null, decimal? minimo = null)
    {
        var periodicidades = Periodicities ?? [];
        var sistemas = Amortization?.Systems ?? [];
        var formas = PaymentForms ?? [];
        var tasas = Rates ?? new CreditLineRatesInput();
        var variable = tasas.Current.Kind == RateKind.Variable ? tasas.Variable : null;
        var gracia = Amortization?.Grace ?? new GraceInput();
        return new BorradorDeVersionDeLinea(ValidFrom, Reason?.Trim() ?? string.Empty, ModalityCode?.Trim() ?? string.Empty)
        {
            SubsegmentCode = string.IsNullOrWhiteSpace(SubsegmentCode) ? null : SubsegmentCode.Trim(),
            ForEmployees = ForEmployees,
            AllowsNonAssociates = AllowsNonAssociates,
            AllowsCommercialOrigin = AllowsCommercialOrigin,
            CommercialDefaultFor = CommercialDefaultFor,
            InterestTaxTreatment = InterestTaxConcept is null ? null : InterestTaxConcept.Taxed ? InterestTaxTreatment.Taxed : InterestTaxTreatment.NotTaxed,
            InterestTaxSupportAttachmentPublicId = InterestTaxConcept?.SupportAttachmentPublicId,
            RateKind = tasas.Current.Kind,
            AnnualRate = tasas.Current.Kind == RateKind.Fixed ? tasas.Current.EffectiveAnnual : null,
            IndexCode = variable?.IndexCode,
            IndexKey = variable?.IndexKey,
            Margin = variable?.Margin,
            RateFloor = variable?.Floor,
            RateCeiling = variable?.Ceiling,
            ResetPeriodicity = variable?.ResetPeriodicity,
            DefaultRateKind = tasas.Default.Kind,
            DefaultRate = tasas.Default.Kind == DefaultRateKind.Fixed ? tasas.Default.EffectiveAnnual : null,
            DefaultRateFactor = tasas.Default.Kind == DefaultRateKind.RelativeToCurrent ? tasas.Default.Factor : null,
            MinAmount = Amounts?.Min ?? 0m,
            MaxAmount = Amounts?.Max ?? 0m,
            MinTermMonths = Terms?.MinMonths ?? 0,
            MaxTermMonths = Terms?.MaxMonths ?? 0,
            MaxInstallments = Terms?.MaxInstallments,
            Periodicities = periodicidades,
            DefaultPeriodicity = DefaultPeriodicity ?? periodicidades.FirstOrDefault(),
            PaymentDay = PaymentDay,
            CutoffDay = CutoffDay,
            AmortizationSystems = sistemas,
            DefaultAmortizationSystem = Amortization?.Default ?? sistemas.FirstOrDefault(),
            AllowsExtraordinaryInstallments = Amortization?.AllowsExtraordinary ?? false,
            MaxExtraordinaryFraction = Amortization?.MaxExtraordinaryFraction,
            AllowedGraceKind = gracia.Kind,
            MaxGraceInstallments = gracia.MaxInstallments,
            GraceInterestTreatment = gracia.InterestTreatment,
            GraceDistributionInstallments = gracia.DistributionInstallments,
            AllowsTranches = Amortization?.AllowsTranches ?? false,
            DayCount = DayCount,
            RateEquivalence = RateEquivalence,
            RoundingUnit = Rounding?.Unit ?? RedondeoDeCartera.Centavo,
            RoundingMode = Rounding?.Mode ?? RoundingMode.AwayFromZero,
            RoundingMoment = Rounding?.Moment ?? RoundingMoment.PerInstallment,
            AllocationOrder = AllocationOrder,
            ConceptPriority = ConceptOrder ?? BorradorDeVersionDeLinea.PrelacionPorDefecto,
            SuspenseWithItsConcept = SuspenseWithItsConcept,
            AllowsSpecificInstallmentOutOfOrder = AllowsSpecificInstallmentOutOfOrder,
            PrepaymentOfferedFirst = AdvanceOptionFirst,
            NoteRecalculation = CommercialNoteRecalculation,
            RepaymentChannels = formas,
            DefaultRepaymentChannel = DefaultPaymentForm ?? formas.FirstOrDefault(),
            MinAssociateSeniorityMonths = Requirements?.MinAssociateSeniorityMonths,
            MinAge = Requirements?.MinAge,
            MaxAge = Requirements?.MaxAge,
            RequiresEmployment = Requirements?.RequiresEmployment ?? false,
            ContributionsMultiple = CreditLimit?.ContributionsMultiple,
            MaxIndebtedness = CreditLimit?.MaxIndebtedness,
            MinContributionsBalance = CreditLimit?.MinContributions,
            RequiresBureauQuery = RequiresBureauQuery,
            ScoringModel = puntaje,
            MinimumScore = minimo,
            RateBands = (tasas.Bands ?? []).Select((x, i) => new BorradorDeBanda(i + 1)
            {
                MinAmount = x.AmountFrom, MaxAmount = x.AmountTo, MinTermMonths = x.TermFrom, MaxTermMonths = x.TermTo, DebtorKind = x.DebtorKind,
                ForEmployees = x.ForEmployees, MinSeniorityMonths = x.MinSeniorityMonths,
                AnnualRate = tasas.Current.Kind == RateKind.Fixed ? x.EffectiveAnnual : null,
                Margin = tasas.Current.Kind == RateKind.Variable ? x.Margin : null,
            }).ToList(),
            DocumentRequirements = (Documents ?? []).Select((d, i) => new BorradorDeDocumento(d.Code, d.Name, d.Mandatory, i + 1)
            {
                AppliesTo = d.AppliesTo, MinAmount = d.AmountFrom, MaxAmount = d.AmountTo, DebtorKind = d.DebtorKind, RepaymentChannel = d.PaymentForm,
                MaxAgeDays = d.MaxAgeDays,
            }).ToList(),
            Insurances = (Insurances ?? []).Select(s => new BorradorDeSeguro(s.Code, s.Name,
                aseguradoras.TryGetValue(s.InsurerPersonPublicId, out var id) ? id : 0, s.Base, s.Rate, s.Periodicity)
            {
                AccrualTiming = s.AccrualTiming, IsMandatory = s.Mandatory, RetainedFraction = s.RetainedFraction, SelfInsured = s.SelfInsured,
                IsDebtorLifeInsurance = s.IsDebtorLifeInsurance,
            }).ToList(),
            CollateralRules = (CollateralRules ?? []).Select((g, i) => new BorradorDeGarantia(i + 1, g.CoDebtors)
            {
                MinAmount = g.AmountFrom, MaxAmount = g.AmountTo, MinTermMonths = g.TermFrom, MaxTermMonths = g.TermTo,
                AdmittedKinds = g.AdmittedKinds ?? [], MinimumCoverage = g.MinCoverage, ContributionsCountAsCollateral = g.ContributionsCount,
                RequiredPromissoryNoteKind = g.PromissoryNoteKind,
            }).ToList(),
        };
    }
}

// ------------------------------------------------------------------------------------------------------------- salidas --

public sealed record RateBandDto(int Order, decimal? AmountFrom, decimal? AmountTo, int? TermFrom, int? TermTo, DebtorKind? DebtorKind,
    bool? ForEmployees, int? MinSeniorityMonths, decimal? EffectiveAnnual, decimal? Margin);

public sealed record DocumentRequirementDto(string Code, string Name, bool Mandatory, CreditPartyRole? AppliesTo, decimal? AmountFrom,
    decimal? AmountTo, DebtorKind? DebtorKind, RepaymentChannel? PaymentForm, int? MaxAgeDays, int Order);

public sealed record InsuranceDto(string Code, string Name, Guid InsurerPersonPublicId, string? InsurerName, InsuranceBase Base, decimal Rate,
    RepaymentPeriodicity Periodicity, InsuranceAccrualTiming AccrualTiming, bool Mandatory, decimal? RetainedFraction, bool SelfInsured,
    bool IsDebtorLifeInsurance, bool ComputesForUsury);

public sealed record CollateralRuleDto(int Order, decimal? AmountFrom, decimal? AmountTo, int? TermFrom, int? TermTo, int CoDebtors,
    IReadOnlyList<CollateralKind> AdmittedKinds, decimal? MinCoverage, bool ContributionsCount, PromissoryNoteKind? PromissoryNoteKind);

/// <summary>Una versión completa (api.md §3.1 <c>GET /{id}/versions/{versionId}</c>), con su fin de vigencia implícito y su huella.</summary>
public sealed record CreditLineVersionDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateTime RegisteredAt,
    string? RegisteredBy,
    string Reason,
    string ModalityCode,
    string? SubsegmentCode,
    bool ForEmployees,
    bool AllowsNonAssociates,
    bool AllowsCommercialOrigin,
    DebtorKind? CommercialDefaultFor,
    InterestTaxTreatment? InterestTaxTreatment,
    Guid? InterestTaxSupportAttachmentPublicId,
    RateKind RateKind,
    decimal? EffectiveAnnual,
    string? IndexCode,
    string? IndexKey,
    decimal? Margin,
    decimal? RateFloor,
    decimal? RateCeiling,
    RepaymentPeriodicity? ResetPeriodicity,
    DefaultRateKind DefaultRateKind,
    decimal? DefaultRate,
    decimal? DefaultRateFactor,
    decimal MinAmount,
    decimal MaxAmount,
    int MinTermMonths,
    int MaxTermMonths,
    int? MaxInstallments,
    IReadOnlyList<RepaymentPeriodicity> Periodicities,
    RepaymentPeriodicity DefaultPeriodicity,
    byte? PaymentDay,
    byte? CutoffDay,
    IReadOnlyList<AmortizationSystem> AmortizationSystems,
    AmortizationSystem DefaultAmortizationSystem,
    bool AllowsExtraordinaryInstallments,
    decimal? MaxExtraordinaryFraction,
    GraceKind AllowedGraceKind,
    int? MaxGraceInstallments,
    GraceInterestTreatment? GraceInterestTreatment,
    int? GraceDistributionInstallments,
    bool AllowsTranches,
    DayCountConvention DayCount,
    RateEquivalence RateEquivalence,
    decimal RoundingUnit,
    RoundingMode RoundingMode,
    RoundingMoment RoundingMoment,
    AllocationOrder AllocationOrder,
    IReadOnlyList<LoanConcept> ConceptOrder,
    bool SuspenseWithItsConcept,
    bool AllowsSpecificInstallmentOutOfOrder,
    PaymentModality AdvanceOptionFirst,
    PrepaymentChoice CommercialNoteRecalculation,
    IReadOnlyList<RepaymentChannel> PaymentForms,
    RepaymentChannel DefaultPaymentForm,
    int? MinAssociateSeniorityMonths,
    int? MinAge,
    int? MaxAge,
    bool RequiresEmployment,
    decimal? ContributionsMultiple,
    decimal? MaxIndebtedness,
    decimal? MinContributionsBalance,
    bool RequiresBureauQuery,
    bool HasScoring,
    decimal? MinimumScore,
    string ContentSha256,
    IReadOnlyList<RateBandDto> RateBands,
    IReadOnlyList<DocumentRequirementDto> Documents,
    IReadOnlyList<InsuranceDto> Insurances,
    IReadOnlyList<CollateralRuleDto> CollateralRules)
{
    /// <summary>La versión leída con sus hijas; <paramref name="aseguradoras"/> da el PublicId y el nombre de cada aseguradora por su Id.</summary>
    public static CreditLineVersionDto De(VersionDeLineaLeida l, DateOnly? validTo, IReadOnlyDictionary<int, (Guid PublicId, string? Nombre)> aseguradoras)
    {
        var v = l.Version;
        return new CreditLineVersionDto(v.PublicId, v.VersionNumber, v.ValidFrom, validTo, v.RegisteredAt, v.CreatedBy, v.Reason, v.ModalityCode,
            v.SubsegmentCode, v.ForEmployees, v.AllowsNonAssociates, v.AllowsCommercialOrigin, v.CommercialDefaultFor, v.InterestTaxTreatment,
            v.InterestTaxSupportAttachmentPublicId, v.RateKind, v.AnnualRate, v.IndexCode, v.IndexKey, v.Margin, v.RateFloor, v.RateCeiling,
            v.ResetPeriodicity, v.DefaultRateKind, v.DefaultRate, v.DefaultRateFactor, v.MinAmount, v.MaxAmount, v.MinTermMonths, v.MaxTermMonths,
            v.MaxInstallments, v.LeerPeriodicidades(), v.DefaultPeriodicity, v.PaymentDay, v.CutoffDay, v.LeerSistemas(), v.DefaultAmortizationSystem,
            v.AllowsExtraordinaryInstallments, v.MaxExtraordinaryFraction, v.AllowedGraceKind, v.MaxGraceInstallments, v.GraceInterestTreatment,
            v.GraceDistributionInstallments, v.AllowsTranches, v.DayCount, v.RateEquivalence, v.RoundingUnit, v.RoundingMode, v.RoundingMoment,
            v.AllocationOrder, v.LeerPrelacion(), v.SuspenseWithItsConcept, v.AllowsSpecificInstallmentOutOfOrder, v.PrepaymentOfferedFirst,
            v.NoteRecalculation, v.LeerFormasDePago(), v.DefaultRepaymentChannel, v.MinAssociateSeniorityMonths, v.MinAge, v.MaxAge,
            v.RequiresEmployment, v.ContributionsMultiple, v.MaxIndebtedness, v.MinContributionsBalance, v.RequiresBureauQuery,
            v.ScoringModel is not null, v.MinimumScore, v.ContentSha256,
            l.Bandas.Select(b => new RateBandDto(b.Order, b.MinAmount, b.MaxAmount, b.MinTermMonths, b.MaxTermMonths, b.DebtorKind, b.ForEmployees,
                b.MinSeniorityMonths, b.AnnualRate, b.Margin)).ToList(),
            l.Documentos.Select(d => new DocumentRequirementDto(d.Code, d.Name, d.IsMandatory, d.AppliesTo, d.MinAmount, d.MaxAmount, d.DebtorKind,
                d.RepaymentChannel, d.MaxAgeDays, d.Order)).ToList(),
            l.Seguros.Select(s =>
            {
                var a = aseguradoras.GetValueOrDefault(s.InsurerPersonId);
                return new InsuranceDto(s.Code, s.Name, a.PublicId, a.Nombre, s.Base, s.Rate, s.ChargePeriodicity, s.AccrualTiming, s.IsMandatory,
                    s.RetainedFraction, s.SelfInsured, s.IsDebtorLifeInsurance, s.Computa());
            }).ToList(),
            l.Garantias.Select(g => new CollateralRuleDto(g.Order, g.MinAmount, g.MaxAmount, g.MinTermMonths, g.MaxTermMonths, g.RequiredCoDebtors,
                ListaDeEnums.Leer<CollateralKind>(g.AdmittedKinds), g.MinimumCoverage, g.ContributionsCountAsCollateral, g.RequiredPromissoryNoteKind)).ToList());
    }
}

/// <summary>Una versión en el historial de la línea (api.md §3.1 <c>GET /{id}</c>).</summary>
public sealed record CreditLineVersionSummaryDto(Guid PublicId, int VersionNumber, DateOnly ValidFrom, DateOnly? ValidTo, DateTime RegisteredAt,
    string? RegisteredBy, string Reason, bool IsCurrent);

/// <summary>Una línea con la versión vigente en la fecha pedida y el historial de versiones (api.md §3.1).</summary>
public sealed record CreditLineDto(
    Guid PublicId,
    string Code,
    string Name,
    string? Purpose,
    CreditLineStatus Status,
    CreditLineClosedReason? ClosedReason,
    DateTime? ClosedAt,
    string? CloseNotes,
    DateOnly? UsuryBlockedSince,
    Guid? UsuryBlockedByValuePublicId,
    bool AdmitsNewCredits,
    CreditLineVersionDto? CurrentVersion,
    IReadOnlyList<CreditLineVersionSummaryDto> Versions);

/// <summary>La versión vigente resumida para la lista (api.md §3.1); <c>usuryHeadroom</c> es la menor holgura de su plan tipo hoy.</summary>
public sealed record CreditLineCurrentVersionDto(Guid PublicId, DateOnly ValidFrom, RateKind RateKind, decimal? CurrentRate, string? IndexCode,
    decimal? Margin, decimal? UsuryHeadroom);

/// <summary>Una línea en la lista (api.md §3.1), por código; sólo las del alcance.</summary>
public sealed record CreditLineListItemDto(
    Guid PublicId,
    string Code,
    string Name,
    string? Purpose,
    CreditLineStatus Status,
    CreditLineClosedReason? ClosedReason,
    DateOnly? UsuryBlockedSince,
    string? ModalityCode,
    string? SubsegmentCode,
    bool CommercialOrigin,
    CreditLineCurrentVersionDto? CurrentVersion);

/// <summary>Un cargo que computa para la usura en la consulta.</summary>
public sealed record UsuryChargeDto(string Code, decimal Amount);

/// <summary>La consulta de usura (api.md §3.1 <c>usury-check</c>): no escribe.</summary>
public sealed record CreditLineUsuryCheckDto(
    DateOnly Date,
    Guid VersionPublicId,
    string ModalityCode,
    string? UsuryKey,
    string Field,
    decimal Rate,
    decimal EffectiveRate,
    decimal EffectiveRateWithoutCharges,
    decimal ChargesSpread,
    decimal? UsuryRate,
    Guid? NationalValuePublicId,
    decimal? Headroom,
    IReadOnlyList<UsuryChargeDto> ComputableCharges,
    IReadOnlyList<string> Explanation,
    bool Passes);

/// <summary>
/// Un rango de una variable del bloque de puntaje (api.md §3.3): desde incluido, hasta excluido. <see cref="NoData"/> (US2, T0387) marca
/// el rango «sin dato»: los puntos que toma la variable cuando la solicitud no trae su valor (sin él, cero).
/// </summary>
public sealed record ScoringRangeDto(decimal? From, decimal? To, decimal Points, bool NoData = false);

public sealed record ScoringVariableDto(string Code, string Name, string Source, decimal Weight, IReadOnlyList<ScoringRangeDto> Ranges)
{
    public VariableDePuntaje Modelo() => new(Code ?? string.Empty, Name ?? string.Empty, Source ?? string.Empty, Weight,
        (Ranges ?? []).Select(r => new RangoDePuntaje(r.From, r.To, r.Points, r.NoData)).ToList());

    public static ScoringVariableDto De(VariableDePuntaje v) =>
        new(v.Code, v.Name, v.Source, v.Weight, v.Ranges.Select(r => new ScoringRangeDto(r.From, r.To, r.Points, r.NoData)).ToList());
}

/// <summary>El bloque de puntaje de la versión vigente (api.md §3.3 <c>GET</c>).</summary>
public sealed record CreditLineScoringDto(Guid VersionPublicId, DateOnly ValidFrom, decimal? MinimumScore, string? ExceptionLevelCode,
    IReadOnlyList<ScoringVariableDto> Variables, IReadOnlyList<string> AllowedSources);
