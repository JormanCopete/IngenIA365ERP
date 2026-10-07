using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

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
}
