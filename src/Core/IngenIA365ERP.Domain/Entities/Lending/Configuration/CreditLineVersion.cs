using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLineVersions</c> (feature 013, T0210; data-model §1.3; FR-007, FR-019–FR-032, FR-037, D20, T44): una versión de la
/// línea con su fecha desde y su fecha de registro. Es lo que el crédito <b>copia y sella</b> al desembolsar. Hecho de sólo
/// inserción (<see cref="IHechoInmutable"/>, C42) con sus cuatro hijas; la corrige una versión nueva y se lee con
/// <see cref="VigenciaBitemporal.VigenteDeCartera{T}"/>. Las listas (<see cref="Periodicities"/>, <see cref="AmortizationSystems"/>,
/// <see cref="ConceptPriority"/>, <see cref="RepaymentChannels"/>) y el modelo de puntaje van en JSON canónico.
/// </summary>
public class CreditLineVersion : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public int CreditLineId { get; init; }

    public int VersionNumber { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateTime RegisteredAt { get; init; }

    public string Reason { get; init; } = string.Empty;

    // ---------------------------------------------------------------------------------------------- clasificación --

    /// <summary>Código de modalidad del <c>CatalogoDeModalidades</c> vigente en <see cref="ValidFrom"/> (T44); se sella, sin FK.</summary>
    public string ModalityCode { get; init; } = string.Empty;

    public string? SubsegmentCode { get; init; }

    /// <summary>Línea para empleados de la cooperativa (FR-019, C96).</summary>
    public bool ForEmployees { get; init; }

    public bool AllowsNonAssociates { get; init; }

    public bool AllowsCommercialOrigin { get; init; }

    public DebtorKind? CommercialDefaultFor { get; init; }

    public InterestTaxTreatment? InterestTaxTreatment { get; init; }

    public Guid? InterestTaxSupportAttachmentPublicId { get; init; }

    // -------------------------------------------------------------------------------------------- tasa corriente --

    public RateKind RateKind { get; init; }

    public decimal? AnnualRate { get; init; }

    public string? IndexCode { get; init; }

    public string? IndexKey { get; init; }

    public decimal? Margin { get; init; }

    public decimal? RateFloor { get; init; }

    public decimal? RateCeiling { get; init; }

    public RepaymentPeriodicity? ResetPeriodicity { get; init; }

    // ------------------------------------------------------------------------------------------------------- mora --

    public DefaultRateKind DefaultRateKind { get; init; }

    public decimal? DefaultRate { get; init; }

    public decimal? DefaultRateFactor { get; init; }

    // ----------------------------------------------------------------------------- montos, plazos y periodicidades --

    public decimal MinAmount { get; init; }

    public decimal MaxAmount { get; init; }

    public int MinTermMonths { get; init; }

    public int MaxTermMonths { get; init; }

    public int? MaxInstallments { get; init; }

    /// <summary>JSON con la lista de <see cref="RepaymentPeriodicity"/> admitidas.</summary>
    public string Periodicities { get; init; } = "[]";

    public RepaymentPeriodicity DefaultPeriodicity { get; init; }

    public byte? PaymentDay { get; init; }

    public byte? CutoffDay { get; init; }

    // ------------------------------------------------------------------------------------------------------- plan --

    /// <summary>JSON con la lista de <see cref="AmortizationSystem"/> admitidos.</summary>
    public string AmortizationSystems { get; init; } = "[]";

    public AmortizationSystem DefaultAmortizationSystem { get; init; }

    public bool AllowsExtraordinaryInstallments { get; init; }

    public decimal? MaxExtraordinaryFraction { get; init; }

    public GraceKind AllowedGraceKind { get; init; }

    public int? MaxGraceInstallments { get; init; }

    public GraceInterestTreatment? GraceInterestTreatment { get; init; }

    public int? GraceDistributionInstallments { get; init; }

    public bool AllowsTranches { get; init; }

    public DayCountConvention DayCount { get; init; }

    public RateEquivalence RateEquivalence { get; init; } = RateEquivalence.Compound;

    public decimal RoundingUnit { get; init; }

    public RoundingMode RoundingMode { get; init; }

    public RoundingMoment RoundingMoment { get; init; }

    // -------------------------------------------------------------------------------------------------- prelación --

    public AllocationOrder AllocationOrder { get; init; } = AllocationOrder.OldestInstallmentFirst;

    /// <summary>JSON con el orden de <see cref="LoanConcept"/> dentro de la cuota.</summary>
    public string ConceptPriority { get; init; } = "[]";

    public bool SuspenseWithItsConcept { get; init; } = true;

    public bool AllowsSpecificInstallmentOutOfOrder { get; init; }

    public PaymentModality PrepaymentOfferedFirst { get; init; }

    public PrepaymentChoice NoteRecalculation { get; init; }

    // -------------------------------------------------------------------------------------------- formas de pago --

    /// <summary>JSON con la lista de <see cref="RepaymentChannel"/>.</summary>
    public string RepaymentChannels { get; init; } = "[]";

    public RepaymentChannel DefaultRepaymentChannel { get; init; }

    // ------------------------------------------------------------------------------------ requisitos, cupo y aportes --

    public int? MinAssociateSeniorityMonths { get; init; }

    public int? MinAge { get; init; }

    public int? MaxAge { get; init; }

    public bool RequiresEmployment { get; init; }

    public decimal? ContributionsMultiple { get; init; }

    public decimal? MaxIndebtedness { get; init; }

    public decimal? MinContributionsBalance { get; init; }

    /// <summary>«Exige consulta a centrales»: sin canal real no aprueba (T73).</summary>
    public bool RequiresBureauQuery { get; init; }

    // ---------------------------------------------------------------------------------------------------- puntaje --

    /// <summary>JSON del modelo de puntaje (variables, rangos, pesos, escala); nulo = sin puntaje.</summary>
    public string? ScoringModel { get; init; }

    public decimal? MinimumScore { get; init; }

    /// <summary>Huella canónica de la versión con sus hijas; la sella la solicitud.</summary>
    public string ContentSha256 { get; init; } = string.Empty;

    // ----------------------------------------------------------------------------------------------- listas JSON --

    public IReadOnlyList<RepaymentPeriodicity> LeerPeriodicidades() => ListaDeEnums.Leer<RepaymentPeriodicity>(Periodicities);

    public IReadOnlyList<AmortizationSystem> LeerSistemas() => ListaDeEnums.Leer<AmortizationSystem>(AmortizationSystems);

    public IReadOnlyList<LoanConcept> LeerPrelacion() => ListaDeEnums.Leer<LoanConcept>(ConceptPriority);

    public IReadOnlyList<RepaymentChannel> LeerFormasDePago() => ListaDeEnums.Leer<RepaymentChannel>(RepaymentChannels);

    /// <summary>El redondeo sellado, listo para el motor.</summary>
    public RedondeoDeCartera Redondeo() => new(RoundingUnit, RoundingMode, RoundingMoment);

    // ---------------------------------------------------------------------------------------------------- fábrica --

    public const string CodigoRetroactiva = "Lending.CreditLine.RetroactiveVersion";
    public const string CodigoBandasCruzadas = "Lending.CreditLine.RateBandsOverlap";
    public const string CodigoSinConceptoTributario = "Lending.CreditLine.InterestTaxConceptMissing";
    public const string CodigoInvalida = "Validation.Invalid";

    /// <summary>
    /// Lo que está mal en la versión (sin consultar nada fuera de ella), o nulo si se puede registrar (data-model §1.3–§1.7; T0333):
    /// <c>ValidFrom</c> no anterior a <paramref name="hoy"/> (<see cref="CodigoRetroactiva"/>); toda tasa con a lo sumo seis decimales
    /// de fracción (<see cref="EquivalenciaDeTasas.CodigoTasaDemasiadoPrecisa"/>); origen comercial con interés sin su concepto
    /// tributario y soporte (<see cref="CodigoSinConceptoTributario"/>, D20, T44); bandas de las mismas dimensiones que se cruzan
    /// (<see cref="CodigoBandasCruzadas"/>); y <see cref="CodigoInvalida"/> con el campo para lo demás: mínimos sobre máximos, defectos
    /// fuera de su lista, un concepto repetido en la prelación, la tasa que no corresponde a la clase, la terna del índice incompleta,
    /// la gracia sin sus datos, una unidad de redondeo no admitida, códigos u órdenes repetidos en las hijas. La modalidad contra el
    /// catálogo vigente, la vivienda, los no asociados y la usura los decide la aplicación, que tiene con qué.
    /// </summary>
    public static ProblemaDeCartera? Validar(BorradorDeVersionDeLinea b, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (b.ValidFrom < hoy)
            return ProblemaDeCartera.Con(CodigoRetroactiva,
                $"La versión no puede regir desde {b.ValidFrom:yyyy-MM-dd}, antes de hoy ({hoy:yyyy-MM-dd}): registre una desde hoy o después.",
                "validFrom", b.ValidFrom);
        if (string.IsNullOrWhiteSpace(b.Reason)) return Invalida("reason", "Diga el motivo de la versión.");
        if (string.IsNullOrWhiteSpace(b.ModalityCode)) return Invalida("modalityCode", "Elija la modalidad de la línea.");

        foreach (var (campo, tasa) in TasasPactadas(b))
            if (tasa is { } t && !EquivalenciaDeTasas.EsTasaPactable(t))
                return ProblemaDeCartera.Con(EquivalenciaDeTasas.CodigoTasaDemasiadoPrecisa,
                    $"La tasa de «{campo}» tiene más de seis decimales de fracción; escríbala con seis como máximo.", "field", campo);
        foreach (var (campo, tasa) in TasasPactadas(b))
            if (tasa < 0m) return Invalida(campo, "Una tasa no puede ser negativa.");

        switch (b.RateKind)
        {
            case RateKind.Fixed:
                if (b.AnnualRate is null) return Invalida("annualRate", "La tasa fija necesita su efectiva anual.");
                if (b.IndexCode is not null || b.Margin is not null) return Invalida("indexCode", "La tasa fija no lleva índice ni margen.");
                break;
            case RateKind.Variable:
                if (string.IsNullOrWhiteSpace(b.IndexCode) || string.IsNullOrWhiteSpace(b.IndexKey) || b.Margin is null)
                    return Invalida("indexCode", "La tasa variable necesita el índice, su clave y el margen.");
                break;
            default:
                return Invalida("rateKind", "Clase de tasa desconocida.");
        }
        if (b.RateFloor is { } piso && b.RateCeiling is { } techo && piso > techo)
            return Invalida("rateFloor", "El piso de la tasa supera el techo.");
        if (b.DefaultRateKind == DefaultRateKind.Fixed && b.DefaultRate is null) return Invalida("defaultRate", "La mora fija necesita su tasa.");
        if (b.DefaultRateKind == DefaultRateKind.RelativeToCurrent && b.DefaultRateFactor is null)
            return Invalida("defaultRateFactor", "La mora relativa necesita su factor sobre la corriente.");

        if (b.MinAmount <= 0m || b.MinAmount > b.MaxAmount) return Invalida("minAmount", "El monto mínimo es mayor que cero y no supera el máximo.");
        if (b.MinTermMonths < 1 || b.MinTermMonths > b.MaxTermMonths) return Invalida("minTermMonths", "El plazo mínimo es de al menos un mes y no supera el máximo.");
        if (b.MaxInstallments is < 1) return Invalida("maxInstallments", "El máximo de cuotas es de al menos una.");
        if (b.PaymentDay is < 1 or > 31) return Invalida("paymentDay", "El día de pago va de 1 a 31.");
        if (b.CutoffDay is < 1 or > 31) return Invalida("cutoffDay", "El día de corte va de 1 a 31.");

        if (b.Periodicities.Count == 0 || b.Periodicities.Any(p => !Enum.IsDefined(p)) || !b.Periodicities.Contains(b.DefaultPeriodicity))
            return Invalida("defaultPeriodicity", "Las periodicidades admitidas incluyen la de defecto.");
        if (b.AmortizationSystems.Count == 0 || b.AmortizationSystems.Any(s => !Enum.IsDefined(s)) || !b.AmortizationSystems.Contains(b.DefaultAmortizationSystem))
            return Invalida("defaultAmortizationSystem", "Los sistemas de amortización admitidos incluyen el de defecto.");
        if (b.RepaymentChannels.Count == 0 || b.RepaymentChannels.Any(c => !Enum.IsDefined(c)) || !b.RepaymentChannels.Contains(b.DefaultRepaymentChannel))
            return Invalida("defaultRepaymentChannel", "Las formas de pago admitidas incluyen la de defecto.");
        if (b.ConceptPriority.Count == 0 || b.ConceptPriority.Distinct().Count() != b.ConceptPriority.Count || b.ConceptPriority.Any(c => !Enum.IsDefined(c)))
            return Invalida("conceptPriority", "La prelación nombra cada concepto una sola vez.");

        if (!b.AllowsExtraordinaryInstallments && b.MaxExtraordinaryFraction is not null)
            return Invalida("maxExtraordinaryFraction", "Sin extraordinarias no hay tope de extraordinarias.");
        if (b.MaxExtraordinaryFraction is <= 0m or >= 1m) return Invalida("maxExtraordinaryFraction", "El tope de extraordinarias es una fracción del monto entre 0 y 1.");
        switch (b.AllowedGraceKind)
        {
            case GraceKind.None:
                if (b.MaxGraceInstallments is not null) return Invalida("maxGraceInstallments", "Sin gracia no hay cuotas de gracia.");
                break;
            case GraceKind.Principal or GraceKind.Total:
                if (b.MaxGraceInstallments is not >= 1) return Invalida("maxGraceInstallments", "La gracia dice cuántas cuotas puede durar.");
                if (b.AllowedGraceKind == GraceKind.Total && b.GraceInterestTreatment is null)
                    return Invalida("graceInterestTreatment", "La gracia total dice qué se hace con su interés.");
                if (b.GraceInterestTreatment == Enums.Lending.GraceInterestTreatment.Distributed && b.GraceDistributionInstallments is not >= 1)
                    return Invalida("graceDistributionInstallments", "El interés de la gracia distribuido dice en cuántas cuotas.");
                break;
            default:
                return Invalida("allowedGraceKind", "Clase de gracia desconocida.");
        }
        if (!RedondeoDeCartera.UnidadesAdmitidas.Contains(b.RoundingUnit))
            return Invalida("roundingUnit", "La unidad de redondeo es 0,01; 1; 10; 100 o 1.000.");

        if (b.AllowsCommercialOrigin && b.CobraInteres && (b.InterestTaxTreatment is null || b.InterestTaxSupportAttachmentPublicId is null))
            return ProblemaDeCartera.Con(CodigoSinConceptoTributario,
                "Una línea de origen comercial que cobra interés necesita el concepto tributario del interés con el soporte de la contadora.",
                "field", "interestTaxTreatment");

        var problemaDeBandas = ValidarBandas(b);
        if (problemaDeBandas is not null) return problemaDeBandas;

        if (b.DocumentRequirements.Any(d => string.IsNullOrWhiteSpace(d.Code) || string.IsNullOrWhiteSpace(d.Name))
            || b.DocumentRequirements.Select(d => d.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != b.DocumentRequirements.Count)
            return Invalida("documentRequirements", "Cada documento lleva código y nombre, y el código no se repite.");
        if (b.Insurances.Any(s => string.IsNullOrWhiteSpace(s.Code) || string.IsNullOrWhiteSpace(s.Name))
            || b.Insurances.Select(s => s.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != b.Insurances.Count)
            return Invalida("insurances", "Cada seguro lleva código y nombre, y el código no se repite.");
        if (b.Insurances.Any(s => s.RetainedFraction is < 0m or > 1m))
            return Invalida("insurances", "La parte retenida de la prima es una fracción entre 0 y 1.");
        if (b.CollateralRules.Select(g => g.Order).Distinct().Count() != b.CollateralRules.Count
            || b.CollateralRules.Any(g => g.RequiredCoDebtors < 0 || g.MinAmount > g.MaxAmount || g.MinTermMonths > g.MaxTermMonths))
            return Invalida("collateralRules", "Las reglas de garantía tienen órdenes distintos, rangos bien formados y codeudores no negativos.");

        return null;
    }

    /// <summary>
    /// Registra la versión <paramref name="versionNumber"/> de la línea: la valida entera (lanza <see cref="ReglaDeCarteraException"/>
    /// si quien llama no lo hizo) y sella <see cref="ContentSha256"/>. Las hijas se arman con el Id que la base le asigne
    /// (<see cref="VersionDeLineaRegistrada"/>), en la misma transacción.
    /// </summary>
    public static VersionDeLineaRegistrada Registrar(int creditLineId, BorradorDeVersionDeLinea b, int versionNumber, DateOnly hoy, DateTime registeredAt)
    {
        ReglaDeCarteraException.SiHay(Validar(b, hoy));
        ArgumentOutOfRangeException.ThrowIfLessThan(creditLineId, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNumber, 1);

        var version = new CreditLineVersion
        {
            CreditLineId = creditLineId,
            VersionNumber = versionNumber,
            ValidFrom = b.ValidFrom,
            RegisteredAt = registeredAt,
            Reason = b.Reason.Trim(),
            ModalityCode = b.ModalityCode.Trim().ToUpperInvariant(),
            SubsegmentCode = string.IsNullOrWhiteSpace(b.SubsegmentCode) ? null : b.SubsegmentCode.Trim(),
            ForEmployees = b.ForEmployees,
            AllowsNonAssociates = b.AllowsNonAssociates,
            AllowsCommercialOrigin = b.AllowsCommercialOrigin,
            CommercialDefaultFor = b.CommercialDefaultFor,
            InterestTaxTreatment = b.InterestTaxTreatment,
            InterestTaxSupportAttachmentPublicId = b.InterestTaxSupportAttachmentPublicId,
            RateKind = b.RateKind,
            AnnualRate = b.AnnualRate,
            IndexCode = b.IndexCode?.Trim().ToUpperInvariant(),
            IndexKey = b.IndexKey?.Trim(),
            Margin = b.Margin,
            RateFloor = b.RateFloor,
            RateCeiling = b.RateCeiling,
            ResetPeriodicity = b.ResetPeriodicity,
            DefaultRateKind = b.DefaultRateKind,
            DefaultRate = b.DefaultRate,
            DefaultRateFactor = b.DefaultRateFactor,
            MinAmount = b.MinAmount,
            MaxAmount = b.MaxAmount,
            MinTermMonths = b.MinTermMonths,
            MaxTermMonths = b.MaxTermMonths,
            MaxInstallments = b.MaxInstallments,
            Periodicities = ListaDeEnums.Conjunto(b.Periodicities),
            DefaultPeriodicity = b.DefaultPeriodicity,
            PaymentDay = b.PaymentDay,
            CutoffDay = b.CutoffDay,
            AmortizationSystems = ListaDeEnums.Conjunto(b.AmortizationSystems),
            DefaultAmortizationSystem = b.DefaultAmortizationSystem,
            AllowsExtraordinaryInstallments = b.AllowsExtraordinaryInstallments,
            MaxExtraordinaryFraction = b.MaxExtraordinaryFraction,
            AllowedGraceKind = b.AllowedGraceKind,
            MaxGraceInstallments = b.MaxGraceInstallments,
            GraceInterestTreatment = b.GraceInterestTreatment,
            GraceDistributionInstallments = b.GraceDistributionInstallments,
            AllowsTranches = b.AllowsTranches,
            DayCount = b.DayCount,
            RateEquivalence = b.RateEquivalence,
            RoundingUnit = b.RoundingUnit,
            RoundingMode = b.RoundingMode,
            RoundingMoment = b.RoundingMoment,
            AllocationOrder = b.AllocationOrder,
            ConceptPriority = ListaDeEnums.Secuencia(b.ConceptPriority),
            SuspenseWithItsConcept = b.SuspenseWithItsConcept,
            AllowsSpecificInstallmentOutOfOrder = b.AllowsSpecificInstallmentOutOfOrder,
            PrepaymentOfferedFirst = b.PrepaymentOfferedFirst,
            NoteRecalculation = b.NoteRecalculation,
            RepaymentChannels = ListaDeEnums.Conjunto(b.RepaymentChannels),
            DefaultRepaymentChannel = b.DefaultRepaymentChannel,
            MinAssociateSeniorityMonths = b.MinAssociateSeniorityMonths,
            MinAge = b.MinAge,
            MaxAge = b.MaxAge,
            RequiresEmployment = b.RequiresEmployment,
            ContributionsMultiple = b.ContributionsMultiple,
            MaxIndebtedness = b.MaxIndebtedness,
            MinContributionsBalance = b.MinContributionsBalance,
            RequiresBureauQuery = b.RequiresBureauQuery,
            ScoringModel = b.ScoringModel,
            MinimumScore = b.MinimumScore,
            ContentSha256 = Huella(b),
        };
        return new VersionDeLineaRegistrada(version, b);
    }

    /// <summary>
    /// La huella canónica de la versión con sus hijas (data-model §0.13): todo su contenido —clasificación, tasas, montos, plan,
    /// prelación, formas de pago, requisitos, puntaje— y las hijas en orden fijo (bandas y garantías por orden, documentos por orden,
    /// seguros por código). El motivo y el número de versión no son contenido. La sella la solicitud (US2).
    /// </summary>
    public static string Huella(BorradorDeVersionDeLinea b) =>
        new HuellaDeCartera()
            .Fecha("validFrom", b.ValidFrom)
            .Texto("modalityCode", b.ModalityCode.Trim().ToUpperInvariant())
            .Texto("subsegmentCode", string.IsNullOrWhiteSpace(b.SubsegmentCode) ? null : b.SubsegmentCode.Trim())
            .Logico("forEmployees", b.ForEmployees)
            .Logico("allowsNonAssociates", b.AllowsNonAssociates)
            .Logico("allowsCommercialOrigin", b.AllowsCommercialOrigin)
            .Enumeracion("commercialDefaultFor", b.CommercialDefaultFor)
            .Enumeracion("interestTaxTreatment", b.InterestTaxTreatment)
            .Identificador("interestTaxSupportAttachmentPublicId", b.InterestTaxSupportAttachmentPublicId)
            .Enumeracion("rateKind", (RateKind?)b.RateKind)
            .Decimal("annualRate", b.AnnualRate, 6)
            .Texto("indexCode", b.IndexCode?.Trim().ToUpperInvariant())
            .Texto("indexKey", b.IndexKey?.Trim())
            .Decimal("margin", b.Margin, 6)
            .Decimal("rateFloor", b.RateFloor, 6)
            .Decimal("rateCeiling", b.RateCeiling, 6)
            .Enumeracion("resetPeriodicity", b.ResetPeriodicity)
            .Enumeracion("defaultRateKind", (DefaultRateKind?)b.DefaultRateKind)
            .Decimal("defaultRate", b.DefaultRate, 6)
            .Decimal("defaultRateFactor", b.DefaultRateFactor, 6)
            .Decimal("minAmount", b.MinAmount, 2)
            .Decimal("maxAmount", b.MaxAmount, 2)
            .Entero("minTermMonths", b.MinTermMonths)
            .Entero("maxTermMonths", b.MaxTermMonths)
            .Entero("maxInstallments", b.MaxInstallments)
            .Texto("periodicities", ListaDeEnums.Conjunto(b.Periodicities))
            .Enumeracion("defaultPeriodicity", (RepaymentPeriodicity?)b.DefaultPeriodicity)
            .Entero("paymentDay", b.PaymentDay)
            .Entero("cutoffDay", b.CutoffDay)
            .Texto("amortizationSystems", ListaDeEnums.Conjunto(b.AmortizationSystems))
            .Enumeracion("defaultAmortizationSystem", (AmortizationSystem?)b.DefaultAmortizationSystem)
            .Logico("allowsExtraordinaryInstallments", b.AllowsExtraordinaryInstallments)
            .Decimal("maxExtraordinaryFraction", b.MaxExtraordinaryFraction, 6)
            .Enumeracion("allowedGraceKind", (GraceKind?)b.AllowedGraceKind)
            .Entero("maxGraceInstallments", b.MaxGraceInstallments)
            .Enumeracion("graceInterestTreatment", b.GraceInterestTreatment)
            .Entero("graceDistributionInstallments", b.GraceDistributionInstallments)
            .Logico("allowsTranches", b.AllowsTranches)
            .Enumeracion("dayCount", (DayCountConvention?)b.DayCount)
            .Enumeracion("rateEquivalence", (RateEquivalence?)b.RateEquivalence)
            .Decimal("roundingUnit", b.RoundingUnit, 2)
            .Enumeracion("roundingMode", (RoundingMode?)b.RoundingMode)
            .Enumeracion("roundingMoment", (RoundingMoment?)b.RoundingMoment)
            .Enumeracion("allocationOrder", (AllocationOrder?)b.AllocationOrder)
            .Texto("conceptPriority", ListaDeEnums.Secuencia(b.ConceptPriority))
            .Logico("suspenseWithItsConcept", b.SuspenseWithItsConcept)
            .Logico("allowsSpecificInstallmentOutOfOrder", b.AllowsSpecificInstallmentOutOfOrder)
            .Enumeracion("prepaymentOfferedFirst", (PaymentModality?)b.PrepaymentOfferedFirst)
            .Enumeracion("noteRecalculation", (PrepaymentChoice?)b.NoteRecalculation)
            .Texto("repaymentChannels", ListaDeEnums.Conjunto(b.RepaymentChannels))
            .Enumeracion("defaultRepaymentChannel", (RepaymentChannel?)b.DefaultRepaymentChannel)
            .Entero("minAssociateSeniorityMonths", b.MinAssociateSeniorityMonths)
            .Entero("minAge", b.MinAge)
            .Entero("maxAge", b.MaxAge)
            .Logico("requiresEmployment", b.RequiresEmployment)
            .Decimal("contributionsMultiple", b.ContributionsMultiple, 6)
            .Decimal("maxIndebtedness", b.MaxIndebtedness, 2)
            .Decimal("minContributionsBalance", b.MinContributionsBalance, 2)
            .Logico("requiresBureauQuery", b.RequiresBureauQuery)
            .Texto("scoringModel", b.ScoringModel)
            .Decimal("minimumScore", b.MinimumScore, 4)
            .Lista("rateBands", b.RateBands.OrderBy(x => x.Order), (h, x) => h
                .Entero("order", x.Order)
                .Decimal("minAmount", x.MinAmount, 2)
                .Decimal("maxAmount", x.MaxAmount, 2)
                .Entero("minTermMonths", x.MinTermMonths)
                .Entero("maxTermMonths", x.MaxTermMonths)
                .Enumeracion("debtorKind", x.DebtorKind)
                .Logico("forEmployees", x.ForEmployees)
                .Entero("minSeniorityMonths", x.MinSeniorityMonths)
                .Decimal("annualRate", x.AnnualRate, 6)
                .Decimal("margin", x.Margin, 6))
            .Lista("documentRequirements", b.DocumentRequirements.OrderBy(x => x.Order).ThenBy(x => x.Code.Trim().ToUpperInvariant(), StringComparer.Ordinal), (h, x) => h
                .Texto("code", x.Code.Trim().ToUpperInvariant())
                .Texto("name", x.Name.Trim())
                .Logico("isMandatory", x.IsMandatory)
                .Enumeracion("appliesTo", x.AppliesTo)
                .Decimal("minAmount", x.MinAmount, 2)
                .Decimal("maxAmount", x.MaxAmount, 2)
                .Enumeracion("debtorKind", x.DebtorKind)
                .Enumeracion("repaymentChannel", x.RepaymentChannel)
                .Entero("maxAgeDays", x.MaxAgeDays)
                .Entero("order", x.Order))
            .Lista("insurances", b.Insurances.OrderBy(x => x.Code.Trim().ToUpperInvariant(), StringComparer.Ordinal), (h, x) => h
                .Texto("code", x.Code.Trim().ToUpperInvariant())
                .Texto("name", x.Name.Trim())
                .Entero("insurerPersonId", x.InsurerPersonId)
                .Enumeracion("base", (InsuranceBase?)x.Base)
                .Decimal("rate", x.Rate, 6)
                .Enumeracion("chargePeriodicity", (RepaymentPeriodicity?)x.ChargePeriodicity)
                .Enumeracion("accrualTiming", (InsuranceAccrualTiming?)x.AccrualTiming)
                .Logico("isMandatory", x.IsMandatory)
                .Decimal("retainedFraction", x.RetainedFraction, 6)
                .Logico("selfInsured", x.SelfInsured)
                .Logico("isDebtorLifeInsurance", x.IsDebtorLifeInsurance))
            .Lista("collateralRules", b.CollateralRules.OrderBy(x => x.Order), (h, x) => h
                .Entero("order", x.Order)
                .Decimal("minAmount", x.MinAmount, 2)
                .Decimal("maxAmount", x.MaxAmount, 2)
                .Entero("minTermMonths", x.MinTermMonths)
                .Entero("maxTermMonths", x.MaxTermMonths)
                .Entero("requiredCoDebtors", x.RequiredCoDebtors)
                .Texto("admittedKinds", ListaDeEnums.Conjunto(x.AdmittedKinds))
                .Decimal("minimumCoverage", x.MinimumCoverage, 6)
                .Logico("contributionsCountAsCollateral", x.ContributionsCountAsCollateral)
                .Enumeracion("requiredPromissoryNoteKind", x.RequiredPromissoryNoteKind))
            .Calcular();

    /// <summary>La huella de esta versión ya guardada con sus hijas, para verificar que nadie la cambió.</summary>
    public string HuellaDeLoGuardado(IEnumerable<CreditLineRateBand> bandas, IEnumerable<CreditLineDocumentRequirement> documentos,
        IEnumerable<CreditLineInsurance> seguros, IEnumerable<CreditLineCollateralRule> garantias) =>
        Huella(BorradorDeVersionDeLinea.De(this, bandas, documentos, seguros, garantias));

    private static IEnumerable<(string Campo, decimal? Tasa)> TasasPactadas(BorradorDeVersionDeLinea b)
    {
        yield return ("annualRate", b.AnnualRate);
        yield return ("margin", b.Margin);
        yield return ("rateFloor", b.RateFloor);
        yield return ("rateCeiling", b.RateCeiling);
        yield return ("defaultRate", b.DefaultRate);
        yield return ("defaultRateFactor", b.DefaultRateFactor);
        foreach (var banda in b.RateBands)
        {
            yield return ($"rateBands[{banda.Order}].annualRate", banda.AnnualRate);
            yield return ($"rateBands[{banda.Order}].margin", banda.Margin);
        }
        foreach (var seguro in b.Insurances) yield return ($"insurances[{seguro.Code}].rate", seguro.Rate);
    }

    private static ProblemaDeCartera? ValidarBandas(BorradorDeVersionDeLinea b)
    {
        var bandas = b.RateBands;
        if (bandas.Select(x => x.Order).Distinct().Count() != bandas.Count) return Invalida("rateBands", "Cada banda lleva un orden distinto.");
        foreach (var banda in bandas)
        {
            var conTasa = b.RateKind == RateKind.Fixed
                ? banda.AnnualRate is not null && banda.Margin is null
                : banda.Margin is not null && banda.AnnualRate is null;
            if (!conTasa)
                return Invalida($"rateBands[{banda.Order}]",
                    b.RateKind == RateKind.Fixed ? "Con tasa fija cada banda lleva su efectiva anual, sin margen." : "Con tasa variable cada banda lleva su margen, sin efectiva anual.");
            if (banda.MinAmount > banda.MaxAmount || banda.MinTermMonths > banda.MaxTermMonths)
                return Invalida($"rateBands[{banda.Order}]", "El rango de la banda tiene el mínimo por encima del máximo.");
        }
        for (var i = 0; i < bandas.Count; i++)
            for (var j = i + 1; j < bandas.Count; j++)
                if (bandas[i].SeCruzaCon(bandas[j]))
                    return ProblemaDeCartera.Con(CodigoBandasCruzadas,
                        $"Las bandas {bandas[i].Order} y {bandas[j].Order} se cruzan en monto y plazo para el mismo segmento.", "orders",
                        new[] { bandas[i].Order, bandas[j].Order });
        return null;
    }

    private static ProblemaDeCartera Invalida(string campo, string mensaje) => ProblemaDeCartera.Con(CodigoInvalida, mensaje, "field", campo);
}
