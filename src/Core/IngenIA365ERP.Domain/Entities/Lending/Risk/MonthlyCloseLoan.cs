using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_MonthlyCloseLoans</c> (feature 013, US9, T0696; data-model §23.2; FR-099–FR-106; R13): una línea por crédito vivo del cierre. En el
/// mensual la escriben las tandas y se enlaza a la <b>corrida</b> (<see cref="ProcessRunId"/>, el encabezado todavía no existe); en el base, la
/// activación la enlaza al <b>cierre</b> (<see cref="MonthlyCloseId"/>). Exactamente una de las dos (<see cref="EnlaceValido"/>, y un CHECK en la
/// base). Hecho de alto volumen sin diff de auditoría: el evento resumen es el de la corrida. Las columnas de la pérdida esperada (K6a) nacen
/// nulas en K1 para no alterar una tabla grande después. (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class MonthlyCloseLoan : AuditableEntity, IHechoInmutable
{
    /// <summary>Mensual; nulo en el base.</summary>
    public int? ProcessRunId { get; init; }

    /// <summary>La tanda que la escribió (mensual).</summary>
    public int? BatchNumber { get; init; }

    /// <summary>Sólo en el base.</summary>
    public int? MonthlyCloseId { get; init; }

    public int LoanId { get; init; }

    /// <summary>Deudor del crédito (arrastre por deudor y modalidad).</summary>
    public int PersonId { get; init; }

    public Guid OfficePublicId { get; init; }

    public int BranchId { get; init; }

    public int CreditLineId { get; init; }

    public string ModalityCode { get; init; } = string.Empty;

    public string? SubsegmentCode { get; init; }

    public string SarcSegmentCode { get; init; } = string.Empty;

    public string? ExpectedLossSegmentCode { get; init; }

    public ImpairmentModel ImpairmentModel { get; init; }

    public ModelBasis ModelBasis { get; init; }

    public bool IsEmployee { get; init; }

    public bool IsRestructured { get; init; }

    public int RestructuringCount { get; init; }

    public bool IsSuspended { get; init; }

    public bool IsWrittenOff { get; init; }

    public int DaysPastDueAtClose { get; init; }

    public int MaxDaysPastDueInMonth { get; init; }

    /// <summary>Para las subcategorías por tiempo en E.</summary>
    public int? MonthsInE { get; init; }

    /// <summary>La candidata del modelo (K6a); nula en el Anexo 1.</summary>
    public RatingCategory? ModelRating { get; init; }

    public RatingCategory ArrearsRating { get; init; }

    /// <summary>La peor candidata con arrastre.</summary>
    public RatingCategory FinalRating { get; init; }

    public RatingSubcategory FinalSubcategory { get; init; }

    /// <summary>La que usa la matriz (homologada en PE, T48).</summary>
    public RatingCategory AccountingRating { get; init; }

    public RatingCause RatingCause { get; init; }

    public string CandidatesJson { get; init; } = "[]";

    /// <summary>La de la línea vigente del cierre anterior; nula si el crédito es nuevo.</summary>
    public RatingCategory? PreviousAccountingRating { get; init; }

    public decimal PrincipalBalance { get; init; }

    public decimal CurrentInterestBalance { get; init; }

    public decimal DefaultInterestBalance { get; init; }

    /// <summary>Corriente y mora en suspenso.</summary>
    public decimal SuspendedInterestBalance { get; init; }

    public decimal OtherBalance { get; init; }

    /// <summary>Todos los conceptos de <c>LoanConcept</c>, con anticipo y saldo a favor.</summary>
    public string BalancesByConceptJson { get; init; } = "{}";

    public decimal CollateralAdmissibleValue { get; init; }

    /// <summary>Cero mientras rija D19.</summary>
    public decimal ContributionsDeducted { get; init; }

    public decimal ImpairmentBase { get; init; }

    public decimal ImpairmentPercentage { get; init; }

    public decimal PrincipalImpairment { get; init; }

    public decimal InterestImpairment { get; init; }

    public decimal OtherImpairment { get; init; }

    public decimal TotalImpairment { get; init; }

    /// <summary>Contra la línea vigente del cierre anterior o del base.</summary>
    public decimal PreviousTotalImpairment { get; init; }

    public decimal ImpairmentDelta { get; init; }

    /// <summary>K6a.</summary>
    public decimal? ProbabilityOfDefault { get; init; }

    /// <summary>K6a.</summary>
    public decimal? LossGivenDefault { get; init; }

    /// <summary>K6a.</summary>
    public decimal? ExposureAtDefault { get; init; }

    /// <summary>La explicación crédito por crédito (FR-106).</summary>
    public string ExplanationJson { get; init; } = "[]";

    /// <summary>Exactamente una de <see cref="ProcessRunId"/> y <see cref="MonthlyCloseId"/> (lo valida el escritor y la base).</summary>
    public bool EnlaceValido => ProcessRunId is null != MonthlyCloseId is null;
}
