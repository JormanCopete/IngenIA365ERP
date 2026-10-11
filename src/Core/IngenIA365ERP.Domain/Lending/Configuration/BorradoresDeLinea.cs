using System.Globalization;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Configuration;

// Feature 013, US1 (T0351): lo que reciben y devuelven las fábricas de la versión de una línea de crédito y de sus cuatro hijas.
// Viven fuera de Domain.Entities porque no son tablas (PrincipioVII_SoftDeleteAndAuditable, LosSaldosYTasasDeCarteraTienenSuPrecision).
// Los nombres de las propiedades son los de las columnas (data-model §1.3–§1.7) para que la aplicación copie sin traducir. (nuevo)

/// <summary>
/// La versión de una línea que se quiere registrar, completa (data-model §1.3; api.md §3.1 <c>CreditLineVersionInput</c>): una
/// versión nueva se registra entera con sus hijas, partiendo de la vigente. Las listas son de enums (la entidad las guarda como JSON
/// de nombres). (nuevo)
/// </summary>
public sealed record BorradorDeVersionDeLinea(DateOnly ValidFrom, string Reason, string ModalityCode)
{
    /// <summary>La prelación por defecto (data-model §1.8): gastos informados, seguros, mora, corriente, capital.</summary>
    public static IReadOnlyList<LoanConcept> PrelacionPorDefecto { get; } =
        [LoanConcept.CollectionFee, LoanConcept.Insurance, LoanConcept.DefaultInterest, LoanConcept.CurrentInterest, LoanConcept.Principal];

    // ------------------------------------------------------------------------------------------ clasificación --
    public string? SubsegmentCode { get; init; }
    public bool ForEmployees { get; init; }
    public bool AllowsNonAssociates { get; init; }
    public bool AllowsCommercialOrigin { get; init; }
    public DebtorKind? CommercialDefaultFor { get; init; }
    public InterestTaxTreatment? InterestTaxTreatment { get; init; }
    public Guid? InterestTaxSupportAttachmentPublicId { get; init; }

    // ---------------------------------------------------------------------------------------- tasa corriente --
    public RateKind RateKind { get; init; } = RateKind.Fixed;
    public decimal? AnnualRate { get; init; }
    public string? IndexCode { get; init; }
    public string? IndexKey { get; init; }
    public decimal? Margin { get; init; }
    public decimal? RateFloor { get; init; }
    public decimal? RateCeiling { get; init; }
    public RepaymentPeriodicity? ResetPeriodicity { get; init; }

    // --------------------------------------------------------------------------------------------------- mora --
    public DefaultRateKind DefaultRateKind { get; init; } = DefaultRateKind.RelativeToCurrent;
    public decimal? DefaultRate { get; init; }
    public decimal? DefaultRateFactor { get; init; }

    // ------------------------------------------------------------------------ montos, plazos y periodicidades --
    public decimal MinAmount { get; init; }
    public decimal MaxAmount { get; init; }
    public int MinTermMonths { get; init; }
    public int MaxTermMonths { get; init; }
    public int? MaxInstallments { get; init; }
    public IReadOnlyList<RepaymentPeriodicity> Periodicities { get; init; } = [RepaymentPeriodicity.Monthly];
    public RepaymentPeriodicity DefaultPeriodicity { get; init; } = RepaymentPeriodicity.Monthly;
    public byte? PaymentDay { get; init; }
    public byte? CutoffDay { get; init; }

    // ------------------------------------------------------------------------------------------------- plan --
    public IReadOnlyList<AmortizationSystem> AmortizationSystems { get; init; } = [AmortizationSystem.LevelPayment];
    public AmortizationSystem DefaultAmortizationSystem { get; init; } = AmortizationSystem.LevelPayment;
    public bool AllowsExtraordinaryInstallments { get; init; }
    public decimal? MaxExtraordinaryFraction { get; init; }
    public GraceKind AllowedGraceKind { get; init; } = GraceKind.None;
    public int? MaxGraceInstallments { get; init; }
    public GraceInterestTreatment? GraceInterestTreatment { get; init; }
    public int? GraceDistributionInstallments { get; init; }
    public bool AllowsTranches { get; init; }
    public DayCountConvention DayCount { get; init; } = DayCountConvention.Thirty360;
    public RateEquivalence RateEquivalence { get; init; } = RateEquivalence.Compound;
    public decimal RoundingUnit { get; init; } = RedondeoDeCartera.Centavo;
    public RoundingMode RoundingMode { get; init; } = RoundingMode.AwayFromZero;
    public RoundingMoment RoundingMoment { get; init; } = RoundingMoment.PerConcept;

    // -------------------------------------------------------------------------------------------- prelación --
    public AllocationOrder AllocationOrder { get; init; } = AllocationOrder.OldestInstallmentFirst;
    public IReadOnlyList<LoanConcept> ConceptPriority { get; init; } = PrelacionPorDefecto;
    public bool SuspenseWithItsConcept { get; init; } = true;
    public bool AllowsSpecificInstallmentOutOfOrder { get; init; }
    public PaymentModality PrepaymentOfferedFirst { get; init; } = PaymentModality.ExtraordinaryPrincipal;
    public PrepaymentChoice NoteRecalculation { get; init; } = PrepaymentChoice.ReduceInstallment;

    // -------------------------------------------------------------------------------------- formas de pago --
    public IReadOnlyList<RepaymentChannel> RepaymentChannels { get; init; } = [RepaymentChannel.Cashier];
    public RepaymentChannel DefaultRepaymentChannel { get; init; } = RepaymentChannel.Cashier;

    // ------------------------------------------------------------------------------ requisitos, cupo y aportes --
    public int? MinAssociateSeniorityMonths { get; init; }
    public int? MinAge { get; init; }
    public int? MaxAge { get; init; }
    public bool RequiresEmployment { get; init; }
    public decimal? ContributionsMultiple { get; init; }
    public decimal? MaxIndebtedness { get; init; }
    public decimal? MinContributionsBalance { get; init; }
    public bool RequiresBureauQuery { get; init; }

    // ---------------------------------------------------------------------------------------------- puntaje --
    public string? ScoringModel { get; init; }
    public decimal? MinimumScore { get; init; }

    // ----------------------------------------------------------------------------------------------- hijas --
    public IReadOnlyList<BorradorDeBanda> RateBands { get; init; } = [];
    public IReadOnlyList<BorradorDeDocumento> DocumentRequirements { get; init; } = [];
    public IReadOnlyList<BorradorDeSeguro> Insurances { get; init; } = [];
    public IReadOnlyList<BorradorDeGarantia> CollateralRules { get; init; } = [];

    /// <summary>¿La versión cobra interés? Variable siempre; fija si la tasa base o alguna banda es mayor que cero (D20, T44).</summary>
    public bool CobraInteres =>
        RateKind == RateKind.Variable || AnnualRate > 0m || RateBands.Any(b => b.AnnualRate > 0m);

    /// <summary>La versión completa a partir de una ya guardada y sus hijas (para versionar desde la vigente o verificar la huella).</summary>
    public static BorradorDeVersionDeLinea De(CreditLineVersion v, IEnumerable<CreditLineRateBand> bandas,
        IEnumerable<CreditLineDocumentRequirement> documentos, IEnumerable<CreditLineInsurance> seguros, IEnumerable<CreditLineCollateralRule> garantias) =>
        new(v.ValidFrom, v.Reason, v.ModalityCode)
        {
            SubsegmentCode = v.SubsegmentCode,
            ForEmployees = v.ForEmployees,
            AllowsNonAssociates = v.AllowsNonAssociates,
            AllowsCommercialOrigin = v.AllowsCommercialOrigin,
            CommercialDefaultFor = v.CommercialDefaultFor,
            InterestTaxTreatment = v.InterestTaxTreatment,
            InterestTaxSupportAttachmentPublicId = v.InterestTaxSupportAttachmentPublicId,
            RateKind = v.RateKind,
            AnnualRate = v.AnnualRate,
            IndexCode = v.IndexCode,
            IndexKey = v.IndexKey,
            Margin = v.Margin,
            RateFloor = v.RateFloor,
            RateCeiling = v.RateCeiling,
            ResetPeriodicity = v.ResetPeriodicity,
            DefaultRateKind = v.DefaultRateKind,
            DefaultRate = v.DefaultRate,
            DefaultRateFactor = v.DefaultRateFactor,
            MinAmount = v.MinAmount,
            MaxAmount = v.MaxAmount,
            MinTermMonths = v.MinTermMonths,
            MaxTermMonths = v.MaxTermMonths,
            MaxInstallments = v.MaxInstallments,
            Periodicities = v.LeerPeriodicidades(),
            DefaultPeriodicity = v.DefaultPeriodicity,
            PaymentDay = v.PaymentDay,
            CutoffDay = v.CutoffDay,
            AmortizationSystems = v.LeerSistemas(),
            DefaultAmortizationSystem = v.DefaultAmortizationSystem,
            AllowsExtraordinaryInstallments = v.AllowsExtraordinaryInstallments,
            MaxExtraordinaryFraction = v.MaxExtraordinaryFraction,
            AllowedGraceKind = v.AllowedGraceKind,
            MaxGraceInstallments = v.MaxGraceInstallments,
            GraceInterestTreatment = v.GraceInterestTreatment,
            GraceDistributionInstallments = v.GraceDistributionInstallments,
            AllowsTranches = v.AllowsTranches,
            DayCount = v.DayCount,
            RateEquivalence = v.RateEquivalence,
            RoundingUnit = v.RoundingUnit,
            RoundingMode = v.RoundingMode,
            RoundingMoment = v.RoundingMoment,
            AllocationOrder = v.AllocationOrder,
            ConceptPriority = v.LeerPrelacion(),
            SuspenseWithItsConcept = v.SuspenseWithItsConcept,
            AllowsSpecificInstallmentOutOfOrder = v.AllowsSpecificInstallmentOutOfOrder,
            PrepaymentOfferedFirst = v.PrepaymentOfferedFirst,
            NoteRecalculation = v.NoteRecalculation,
            RepaymentChannels = v.LeerFormasDePago(),
            DefaultRepaymentChannel = v.DefaultRepaymentChannel,
            MinAssociateSeniorityMonths = v.MinAssociateSeniorityMonths,
            MinAge = v.MinAge,
            MaxAge = v.MaxAge,
            RequiresEmployment = v.RequiresEmployment,
            ContributionsMultiple = v.ContributionsMultiple,
            MaxIndebtedness = v.MaxIndebtedness,
            MinContributionsBalance = v.MinContributionsBalance,
            RequiresBureauQuery = v.RequiresBureauQuery,
            ScoringModel = v.ScoringModel,
            MinimumScore = v.MinimumScore,
            RateBands = bandas.Where(b => b.CreditLineVersionId == v.Id).OrderBy(b => b.Order).Select(BorradorDeBanda.De).ToList(),
            DocumentRequirements = documentos.Where(d => d.CreditLineVersionId == v.Id).OrderBy(d => d.Order).Select(BorradorDeDocumento.De).ToList(),
            Insurances = seguros.Where(s => s.CreditLineVersionId == v.Id).OrderBy(s => s.Code, StringComparer.Ordinal).Select(BorradorDeSeguro.De).ToList(),
            CollateralRules = garantias.Where(g => g.CreditLineVersionId == v.Id).OrderBy(g => g.Order).Select(BorradorDeGarantia.De).ToList(),
        };
}

/// <summary>Una banda de tasa (data-model §1.4). (nuevo)</summary>
public sealed record BorradorDeBanda(int Order)
{
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public int? MinTermMonths { get; init; }
    public int? MaxTermMonths { get; init; }
    public DebtorKind? DebtorKind { get; init; }
    public bool? ForEmployees { get; init; }
    public int? MinSeniorityMonths { get; init; }
    public decimal? AnnualRate { get; init; }
    public decimal? Margin { get; init; }

    public static BorradorDeBanda De(CreditLineRateBand b) => new(b.Order)
    {
        MinAmount = b.MinAmount, MaxAmount = b.MaxAmount, MinTermMonths = b.MinTermMonths, MaxTermMonths = b.MaxTermMonths,
        DebtorKind = b.DebtorKind, ForEmployees = b.ForEmployees, MinSeniorityMonths = b.MinSeniorityMonths,
        AnnualRate = b.AnnualRate, Margin = b.Margin,
    };

    /// <summary>¿Se cruza con otra banda de las mismas dimensiones (tipo de deudor, empleados, antigüedad) en monto y en plazo?</summary>
    public bool SeCruzaCon(BorradorDeBanda otra) =>
        DebtorKind == otra.DebtorKind && ForEmployees == otra.ForEmployees && MinSeniorityMonths == otra.MinSeniorityMonths
        && Intersecan(MinAmount, MaxAmount, otra.MinAmount, otra.MaxAmount)
        && Intersecan(MinTermMonths, MaxTermMonths, otra.MinTermMonths, otra.MaxTermMonths);

    private static bool Intersecan(decimal? a1, decimal? a2, decimal? b1, decimal? b2) =>
        (a2 is null || b1 is null || b1 <= a2) && (b2 is null || a1 is null || a1 <= b2);

    private static bool Intersecan(int? a1, int? a2, int? b1, int? b2) =>
        (a2 is null || b1 is null || b1 <= a2) && (b2 is null || a1 is null || a1 <= b2);
}

/// <summary>Un documento exigido (data-model §1.5). (nuevo)</summary>
public sealed record BorradorDeDocumento(string Code, string Name, bool IsMandatory, int Order)
{
    public CreditPartyRole? AppliesTo { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public DebtorKind? DebtorKind { get; init; }
    public RepaymentChannel? RepaymentChannel { get; init; }
    public int? MaxAgeDays { get; init; }

    public static BorradorDeDocumento De(CreditLineDocumentRequirement d) => new(d.Code, d.Name, d.IsMandatory, d.Order)
    {
        AppliesTo = d.AppliesTo, MinAmount = d.MinAmount, MaxAmount = d.MaxAmount, DebtorKind = d.DebtorKind,
        RepaymentChannel = d.RepaymentChannel, MaxAgeDays = d.MaxAgeDays,
    };
}

/// <summary>Un seguro de la línea (data-model §1.6); la aseguradora es persona del maestro (su Id interno, I-1). (nuevo)</summary>
public sealed record BorradorDeSeguro(string Code, string Name, int InsurerPersonId, InsuranceBase Base, decimal Rate, RepaymentPeriodicity ChargePeriodicity)
{
    public InsuranceAccrualTiming AccrualTiming { get; init; } = InsuranceAccrualTiming.AtDueDate;
    public bool IsMandatory { get; init; }
    public decimal? RetainedFraction { get; init; }
    public bool SelfInsured { get; init; }
    public bool IsDebtorLifeInsurance { get; init; }

    public static BorradorDeSeguro De(CreditLineInsurance s) => new(s.Code, s.Name, s.InsurerPersonId, s.Base, s.Rate, s.ChargePeriodicity)
    {
        AccrualTiming = s.AccrualTiming, IsMandatory = s.IsMandatory, RetainedFraction = s.RetainedFraction, SelfInsured = s.SelfInsured,
        IsDebtorLifeInsurance = s.IsDebtorLifeInsurance,
    };
}

/// <summary>Una regla de codeudores y garantías (data-model §1.7). (nuevo)</summary>
public sealed record BorradorDeGarantia(int Order, int RequiredCoDebtors)
{
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public int? MinTermMonths { get; init; }
    public int? MaxTermMonths { get; init; }
    public IReadOnlyList<CollateralKind> AdmittedKinds { get; init; } = [];
    public decimal? MinimumCoverage { get; init; }
    public bool ContributionsCountAsCollateral { get; init; }
    public PromissoryNoteKind? RequiredPromissoryNoteKind { get; init; }

    public static BorradorDeGarantia De(CreditLineCollateralRule g) => new(g.Order, g.RequiredCoDebtors)
    {
        MinAmount = g.MinAmount, MaxAmount = g.MaxAmount, MinTermMonths = g.MinTermMonths, MaxTermMonths = g.MaxTermMonths,
        AdmittedKinds = ListaDeEnums.Leer<CollateralKind>(g.AdmittedKinds), MinimumCoverage = g.MinimumCoverage,
        ContributionsCountAsCollateral = g.ContributionsCountAsCollateral, RequiredPromissoryNoteKind = g.RequiredPromissoryNoteKind,
    };
}

/// <summary>
/// Las listas de enums que la versión guarda en una columna de texto: JSON de <b>nombres</b> (<c>["Monthly","Biweekly"]</c>, como la
/// prelación por defecto de data-model §1.8). Los conjuntos se escriben ordenados por su número, para que la misma lista dé el mismo
/// texto; la prelación, en el orden dado, porque el orden es el dato. (nuevo)
/// </summary>
public static class ListaDeEnums
{
    /// <summary>Un conjunto, sin repetidos y ordenado por número.</summary>
    public static string Conjunto<T>(IEnumerable<T> valores) where T : struct, Enum =>
        Escribir(valores.Distinct().OrderBy(v => Convert.ToInt64(v, CultureInfo.InvariantCulture)));

    /// <summary>Una secuencia, en el orden dado.</summary>
    public static string Secuencia<T>(IEnumerable<T> valores) where T : struct, Enum => Escribir(valores);

    /// <summary>Lee una lista escrita por <see cref="Conjunto{T}"/> o <see cref="Secuencia{T}"/>.</summary>
    public static IReadOnlyList<T> Leer<T>(string? json) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        var nombres = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        return nombres.Select(n => Enum.Parse<T>(n, ignoreCase: false)).ToList();
    }

    private static string Escribir<T>(IEnumerable<T> valores) where T : struct, Enum =>
        "[" + string.Join(",", valores.Select(v => "\"" + v.ToString() + "\"")) + "]";
}

/// <summary>
/// La versión recién registrada (aún sin Id) y las hijas que se arman cuando la base se lo asigna: quien registra guarda la versión
/// y después sus hijas, en una sola transacción (molde <c>MatrizPublicada</c>). (nuevo)
/// </summary>
public sealed class VersionDeLineaRegistrada(CreditLineVersion version, BorradorDeVersionDeLinea borrador)
{
    public CreditLineVersion Version { get; } = version;

    public IReadOnlyList<CreditLineRateBand> Bandas(int versionId) => Hijas(versionId, borrador.RateBands.Select(b => CreditLineRateBand.Nueva(versionId, b)));

    public IReadOnlyList<CreditLineDocumentRequirement> Documentos(int versionId) =>
        Hijas(versionId, borrador.DocumentRequirements.Select(d => CreditLineDocumentRequirement.Nuevo(versionId, d)));

    public IReadOnlyList<CreditLineInsurance> Seguros(int versionId) => Hijas(versionId, borrador.Insurances.Select(s => CreditLineInsurance.Nuevo(versionId, s)));

    public IReadOnlyList<CreditLineCollateralRule> Garantias(int versionId) =>
        Hijas(versionId, borrador.CollateralRules.Select(g => CreditLineCollateralRule.Nueva(versionId, g)));

    private static IReadOnlyList<T> Hijas<T>(int versionId, IEnumerable<T> hijas)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(versionId, 1);
        return hijas.ToList();
    }
}
