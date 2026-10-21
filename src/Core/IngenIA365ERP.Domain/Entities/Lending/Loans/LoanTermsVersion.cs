using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Loans;

/// <summary>
/// <c>LND_LoanTermsVersions</c> (feature 013; data-model §4.3; FR-007, FR-010, FR-020–FR-031, D2, T29, T44): las condiciones pactadas
/// del crédito <b>selladas</b> por versión, con su <see cref="RulesVersion"/>. Lo regulatorio (usura, calificación, deterioro,
/// suspensión) no se sella: se lee a cada fecha valor (D2). Hecho de sólo inserción (<see cref="IHechoInmutable"/>): nace con cada
/// operación que cambia condiciones; la reconstrucción toma las versiones con <see cref="RegisteredAt"/> ≤ R (T24). Rige desde
/// <see cref="EffectiveValueDate"/> (no es <c>ValidFrom</c>). La columna <c>NoveltyId</c> nace con K3 (<c>CarteraNovedades</c>).
/// </summary>
public class LoanTermsVersion : AuditableEntity, IHechoInmutable
{
    public int LoanId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>Loan</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Loans.Loan? Loan { get; init; }

    public int VersionNumber { get; init; }

    /// <summary>La operación que la produjo (<c>LND_LoanOperations</c>, bigint).</summary>
    public long OperationId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>LoanOperation</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Operations.LoanOperation? Operation { get; init; }

    /// <summary>Desde qué fecha valor rige.</summary>
    public DateOnly EffectiveValueDate { get; init; }

    /// <summary>Cuándo se supo (R).</summary>
    public DateTime RegisteredAt { get; init; }

    public TermsVersionCause Cause { get; init; }

    /// <summary>K3 (data-model §4.3): la novedad que produjo esta versión; la reconstrucción la reconoce por aquí.</summary>
    public int? NoveltyId { get; init; }

    /// <summary><c>LND-AAAA.n</c> (<c>VersionesDeReglas</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary>Versión de la línea copiada al sellar.</summary>
    public int CreditLineVersionId { get; init; }

    // ------------------------------------------------------------------------------------------ códigos sellados --

    public string ModalityCode { get; init; } = string.Empty;

    public string? SubsegmentCode { get; init; }

    public bool ForEmployees { get; init; }

    public InterestTaxTreatment? InterestTaxTreatment { get; init; }

    // -------------------------------------------------------------------------------------------- tasa corriente --

    public RateKind RateKind { get; init; }

    /// <summary>EA pactada como fracción (o la de la banda que casó).</summary>
    public decimal? AnnualRate { get; init; }

    public string? IndexCode { get; init; }

    public string? IndexKey { get; init; }

    public decimal? Margin { get; init; }

    public decimal? RateFloor { get; init; }

    public decimal? RateCeiling { get; init; }

    public RepaymentPeriodicity? ResetPeriodicity { get; init; }

    /// <summary>Próximo reajuste de la tasa variable (FR-067).</summary>
    public DateOnly? NextResetDate { get; init; }

    // ------------------------------------------------------------------------------------------------------- mora --

    public DefaultRateKind DefaultRateKind { get; init; }

    public decimal? DefaultRate { get; init; }

    public decimal? DefaultRateFactor { get; init; }

    /// <summary><c>Mora.SobreIntereses</c> sellado al desembolsar, dentro del techo nacional; siempre falso en vivienda (FR-063).</summary>
    public bool DefaultOnInterestAllowed { get; init; }

    // ------------------------------------------------------------------------------------------------------- plan --

    public DayCountConvention DayCount { get; init; }

    public RateEquivalence RateEquivalence { get; init; }

    public AmortizationSystem AmortizationSystem { get; init; }

    public RepaymentPeriodicity Periodicity { get; init; }

    public int Installments { get; init; }

    public int TermMonths { get; init; }

    public DateOnly FirstDueDate { get; init; }

    public byte? PaymentDay { get; init; }

    public GraceKind? GraceKind { get; init; }

    public int? GraceInstallments { get; init; }

    public GraceInterestTreatment? GraceInterestTreatment { get; init; }

    public int? GraceDistributionInstallments { get; init; }

    /// <summary>JSON con las cuotas extraordinarias pactadas; nulo = ninguna.</summary>
    public string? ExtraordinaryInstallments { get; init; }

    public decimal RoundingUnit { get; init; }

    public RoundingMode RoundingMode { get; init; }

    public RoundingMoment RoundingMoment { get; init; }

    /// <summary>
    /// K6b (T1048, T1055; FR-024, D18): la unidad del capital sellada de la versión de la línea: <c>Pesos</c> o <c>Uvr</c>. En UVR la reconstrucción
    /// lleva plan y saldos en UVR con la serie diaria y los valora en pesos con la UVR del día.
    /// </summary>
    public DenominationUnit DenominationUnit { get; init; } = DenominationUnit.Pesos;

    // -------------------------------------------------------------------------------- prelación sellada (§1.8) --

    public AllocationOrder AllocationOrder { get; init; }

    /// <summary>JSON con el orden de <see cref="LoanConcept"/> dentro de la cuota.</summary>
    public string ConceptPriority { get; init; } = "[]";

    public bool SuspenseWithItsConcept { get; init; }

    public bool AllowsSpecificInstallmentOutOfOrder { get; init; }

    public PrepaymentChoice NoteRecalculation { get; init; }

    // ---------------------------------------------------------------------------------------- seguros y usura --

    /// <summary>
    /// JSON con los seguros sellados: código, aseguradora (PublicId), base, tarifa, periodicidad, <c>AccrualTiming</c>, retención.
    /// </summary>
    public string Insurances { get; init; } = "[]";

    /// <summary>Efectiva resultante con cargos, sellada al desembolsar (T28).</summary>
    public decimal? ResultingEffectiveRate { get; init; }

    /// <summary>Sobretasa de cargos sellada al desembolsar (T28).</summary>
    public decimal? ChargesSpread { get; init; }

    /// <summary>La usura contra la que se controló (copia de <c>COR_NationalValues</c>, sin FK).</summary>
    public Guid? UsuryValuePublicId { get; init; }

    // --------------------------------------------------------------------------------------- reestructuraciones --

    /// <summary>0 al originar; la migración trae el conteo (FR-139). Lo usa la suspensión «reestructurado más de una vez» (T30).</summary>
    public int RestructuringCount { get; init; }

    public DateOnly? RestructuredOn { get; init; }

    /// <summary>Huella canónica de las entradas.</summary>
    public string InputsSha256 { get; init; } = string.Empty;
}
