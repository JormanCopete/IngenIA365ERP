using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_MemberAccountTermsVersions</c> (feature 014, K9, US6, T169; data-model §3.3; FR-007, FR-023, FR-067, FR-070, FR-127; research R11): las condiciones
/// <b>selladas</b> de un CDAT —y, con K9, del contractual o de una cuenta con tasa pactada o negociada—, en el patrón de <c>LoanTermsVersion</c>. Nace con la
/// constitución (al fondearse), cada prórroga, cada renovación y la migración; nunca se edita (hecho de sólo inserción): una prórroga o una renovación es una
/// versión nueva enlazada a la anterior por <see cref="PreviousVersionId"/>, y el número del título es el de la cuenta con el sufijo de la versión
/// (<c>{Number}-{VersionNumber}</c>, data-model §0.4). La escribe sólo <c>RegistroDeCuentas</c>. (nuevo)
/// </summary>
public class MemberAccountTermsVersion : AuditableEntity, IHechoInmutable
{
    public const int LargoDelMotivo = 300;

    public int MemberAccountId { get; init; }

    /// <summary>1, 2, 3…: el sufijo del título (UK por cuenta).</summary>
    public int VersionNumber { get; init; }

    /// <summary>La operación que la produjo (la constitución, la prórroga, la renovación, el saldo inicial migrado).</summary>
    public long OperationId { get; init; }

    public MemberAccountOperation? Operation { get; init; }

    public DateOnly EffectiveValueDate { get; init; }

    public DateTime RegisteredAt { get; init; }

    public AccountTermsCause Cause { get; init; }

    /// <summary>La versión de la línea copiada.</summary>
    public int DepositLineVersionId { get; init; }

    public string RulesVersion { get; init; } = string.Empty;

    public string InputsSha256 { get; init; } = string.Empty;

    // ------------------------------------------------------------------------------------------- monto, plazo y fechas --

    /// <summary>Capital al iniciar (con lo capitalizado en una prórroga).</summary>
    public decimal PrincipalAmount { get; init; }

    /// <summary>CDAT: el plazo en días.</summary>
    public int? TermDays { get; init; }

    /// <summary>Contractual: el plazo en meses (K9, US7).</summary>
    public int? TermMonths { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? MaturityDate { get; init; }

    /// <summary>El día en que se paga el vencimiento: el siguiente hábil si vence en no hábil, sin interés adicional (D19).</summary>
    public DateOnly? PaymentDate { get; init; }

    /// <summary>CDAT y contractual: el plazo de emisión, dimensión contable del capital (FR-073).</summary>
    public IssueTermBand? IssueTermBand { get; init; }

    // ------------------------------------------------------------------------------------------------------------ tasa --

    /// <summary>La tasa de la tabla (versión o tramo) a la fecha de la constitución, la prórroga o la renovación.</summary>
    public decimal TableAnnualRate { get; init; }

    /// <summary>El margen aprobado sobre la tabla (puntos EA, fracción); nulo = sin negociación.</summary>
    public decimal? NegotiatedSpread { get; init; }

    /// <summary>La tasa aplicada: la tabla más el margen.</summary>
    public decimal AppliedAnnualRate { get; init; }

    /// <summary>La versión de la regla de tasa negociada que se aplicó.</summary>
    public int? NegotiatedRateRuleId { get; init; }

    /// <summary>La solicitud de tasa negociada que se decidió por encima de la atribución de quien la pidió.</summary>
    public int? NegotiatedRateRequestId { get; init; }

    public int? NegotiationApprovedByUserId { get; init; }

    public string? NegotiationReason { get; init; }

    /// <summary>La tasa diaria sellada (<c>TasaEquivalente</c>), de la que sale <c>i_p</c> de cada período.</summary>
    public decimal? PeriodRate { get; init; }

    // ----------------------------------------------------------------------------------------------------------- intereses --

    public RepaymentPeriodicity? InterestPeriodicity { get; init; }

    public InterestTiming? InterestTiming { get; init; }

    public DayCountConvention DayCount { get; init; }

    public RateEquivalence RateEquivalence { get; init; } = RateEquivalence.Compound;

    public decimal RoundingUnit { get; init; }

    public RoundingMode RoundingMode { get; init; }

    public RoundingMoment RoundingMoment { get; init; }

    public InterestDestination? InterestDestination { get; init; }

    /// <summary>Con <c>ToAccount</c>: la cuenta de ahorro del titular donde se abonan los intereses (y el vencimiento si se cancela).</summary>
    public int? DestinationAccountId { get; init; }

    /// <summary>Con <c>PaymentOrder</c>: quién recibe el giro (tercero admitido, FR-091).</summary>
    public int? DestinationPersonId { get; init; }

    public int? DestinationBankId { get; init; }

    public byte? DestinationAccountType { get; init; }

    public string? DestinationBankAccountNumber { get; init; }

    // --------------------------------------------------------------------------------------------------------- vencimiento --

    public MaturityInstruction? MaturityInstruction { get; init; }

    public bool? AutoRollover { get; init; }

    public int? MaxRollovers { get; init; }

    /// <summary>Las prórrogas acumuladas hasta esta versión.</summary>
    public int RolloverCount { get; init; }

    public RolloverRateRule? RolloverRateRule { get; init; }

    public bool? KeepsNegotiatedMargin { get; init; }

    public bool? CapitalizesOnRollover { get; init; }

    public int? GraceBusinessDays { get; init; }

    public bool? GraceAccrues { get; init; }

    public AfterMaturityTreatment? AfterMaturityTreatment { get; init; }

    public bool? EarlyRedemptionAllowed { get; init; }

    public EarlyRedemptionPenaltyMode? EarlyRedemptionPenaltyMode { get; init; }

    public decimal? PenaltyAnnualRate { get; init; }

    public decimal? PenaltyDiscountFraction { get; init; }

    public int? MinDaysBeforeRedemption { get; init; }

    // ---------------------------------------------------------------------------------------------------------- contractual --

    public decimal? InstallmentAmount { get; init; }

    public RepaymentPeriodicity? InstallmentPeriodicity { get; init; }

    public int? InstallmentCount { get; init; }

    public decimal? BonusAnnualRate { get; init; }

    public decimal? BreachAnnualRate { get; init; }

    public int? ToleratedLateInstallments { get; init; }

    public ContractualBreachEffect? BreachEffect { get; init; }

    public BonusRecognition? BonusRecognition { get; init; }

    public decimal? ProjectedFinalAmount { get; init; }

    /// <summary>La versión anterior (cadena de prórrogas y renovaciones; el título anterior).</summary>
    public int? PreviousVersionId { get; init; }

    /// <summary>El número del título de esta versión: el de la cuenta con el sufijo de la versión (R11).</summary>
    public static string NumeroDeTitulo(string numeroDeCuenta, int version) => $"{numeroDeCuenta}-{version}";
}

/// <summary>
/// <c>LND_TermDepositSchedules</c> (feature 014, K9, US6, T169; data-model §3.4; FR-023, FR-069): un período del plan de intereses de una versión de
/// condiciones del CDAT. Proyección inmutable de <c>PlanDeCdat</c> (la autoridad es el motor): no lleva estado de pago —pagado o pendiente sale del libro
/// (<c>InterestPaid</c> con <c>TermsVersionId</c> y <c>SchedulePeriodNumber</c>)—. La escribe sólo <c>RegistroDeCuentas</c>. (nuevo)
/// </summary>
public class TermDepositScheduleItem : AuditableEntity, IHechoInmutable
{
    public int TermsVersionId { get; init; }

    public MemberAccountTermsVersion? TermsVersion { get; init; }

    public int PeriodNumber { get; init; }

    /// <summary>El inicio del período (excluido: el período es (inicio, fin]).</summary>
    public DateOnly PeriodStart { get; init; }

    public DateOnly PeriodEnd { get; init; }

    /// <summary>Vencido: el fin o el siguiente hábil (D19); anticipado: el inicio.</summary>
    public DateOnly PaymentDate { get; init; }

    public int Days { get; init; }

    /// <summary>La tasa del período (vencida o anticipada), a 18 decimales.</summary>
    public decimal PeriodRate { get; init; }

    /// <summary>El interés del período, redondeado con el redondeo sellado.</summary>
    public decimal Interest { get; init; }

    public bool IsLastPeriod { get; init; }
}
