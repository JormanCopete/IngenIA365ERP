namespace IngenIA365ERP.Domain.Enums.Lending;

// Captaciones y aportes de la Cartera (feature 014, T014; research §0.3, data-model §10). Se guardan por su número; un valor nunca se
// renumera (EnumeracionesDeCarteraTests). Los valores de entregas posteriores existen desde K7, pero ningún comando de K7 los escribe
// (data-model §0.9).

// ------------------------------------------------------------------------------------------------- research §0.3 --

/// <summary>La naturaleza de una línea del catálogo común (R01): colocación (crédito), captación o aportes.</summary>
public enum LineNature
{
    Placement = 1,
    Deposit = 2,
    Contribution = 3,
}

/// <summary>Clase de una cuenta de captación: a la vista, contractual, permanente o CDAT. Nula en aportes.</summary>
public enum DepositClass
{
    Demand = 1,
    Contractual = 2,
    Permanent = 3,
    TermDeposit = 4,
}

/// <summary>Método de saldo para el interés de captación (R05; D31: saldo diario por defecto).</summary>
public enum BalanceMethod
{
    DailyBalance = 1,
    AverageBalance = 2,
    MinimumBalance = 3,
}

/// <summary>Cómo se aplican los tramos de tasa: la tasa del tramo a todo el saldo, o escalonada por porción.</summary>
public enum RateTierMode
{
    WholeBalance = 1,
    Stepped = 2,
}

/// <summary>Momento del interés de un CDAT: vencido o anticipado.</summary>
public enum InterestTiming
{
    Arrears = 1,
    Advance = 2,
}

/// <summary>Con qué tasa se prorroga un CDAT.</summary>
public enum RolloverRateRule
{
    SameRate = 1,
    CurrentTableRate = 2,
    Agreed = 3,
}

/// <summary>Qué pasa con un CDAT vencido y no reclamado al terminar la gracia.</summary>
public enum AfterMaturityTreatment
{
    StopAccruing = 1,
    MoveToDemand = 2,
}

/// <summary>Estado de una cuenta del asociado (data-model §3.1).</summary>
public enum MemberAccountStatus
{
    Opening = 1,
    Active = 2,
    Inactive = 3,
    Matured = 4,
    Closing = 5,
    Closed = 6,
}

/// <summary>Papel de una persona en una cuenta (data-model §3.2).</summary>
public enum MemberAccountPartyRole
{
    Holder = 1,
    CoHolder = 2,
    Signatory = 3,
    LegalRepresentative = 4,
    Beneficiary = 5,
}

/// <summary>Manejo de una cuenta con varios titulares o firmantes.</summary>
public enum HandlingMode
{
    Indistinct = 1,
    Joint = 2,
}

/// <summary>
/// Concepto de saldo de una cuenta (data-model §0.2): del titular (capital, interés por pagar, clases de aporte, bonificación), por
/// cobrar al titular (cargo pendiente) y memoria de lo retenido (retención, GMF).
/// </summary>
public enum MemberAccountConcept
{
    Principal = 1,
    InterestPayable = 2,
    Withholding = 3,
    Gmf = 4,
    ContributionMinimum = 5,
    ContributionOrdinary = 6,
    ContributionExtraordinary = 7,
    ContributionRevaluation = 8,
    ChargeReceivable = 9,
    Bonus = 10,
}

/// <summary>Clase de una fila del libro de cuentas (<c>LND_MemberAccountTransactions</c>, data-model §4.2).</summary>
public enum MemberAccountTransactionKind
{
    OpeningBalance = 1,
    Deposit = 2,
    Withdrawal = 3,
    TransferIn = 4,
    TransferOut = 5,
    InterestAccrued = 6,
    AccrualAdjustment = 7,
    InterestCredited = 8,
    InterestPaid = 9,
    WithholdingApplied = 10,
    GmfCharged = 11,
    ChargeApplied = 12,
    PenaltyApplied = 13,
    Capitalization = 14,
    Reclassification = 15,
    InternalOffset = 16,
    Refund = 17,
    ToUnclaimed = 18,
    GarnishmentConsigned = 19,

    // data-model §4.2 (nuevos en data-model).
    ChargeWaived = 20,
    BonusRecognized = 21,
    AssemblyCredit = 22,
}

/// <summary>Política de exención del GMF (R07, P25).</summary>
public enum GmfExemptionPolicy
{
    SingleMarkedAccount = 1,
    PerHolderLimit = 2,
    NotApplicable = 3,
}

/// <summary>A qué clases de captación aplica el umbral diario de retención (R06, D10).</summary>
public enum WithholdingThresholdScope
{
    AllDepositClasses = 1,
    ExceptTermDeposit = 2,
    None = 3,
}

/// <summary>Motivo de un bloqueo de cuenta (data-model §5.1).</summary>
public enum HoldReason
{
    Internal = 1,
    LoanCollateral = 2,
    SignaturePending = 3,
    Deceased = 4,
    ScreeningMatch = 5,

    /// <summary>data-model §5.1 (nuevo en data-model): el bloqueo que deja un embargo.</summary>
    Garnishment = 6,
    Other = 99,
}

/// <summary>Clase de cargo de una cuenta (FR-128).</summary>
public enum ChargeKind
{
    MaintenanceFee = 1,
    ExtraOperation = 2,
    ExtraDocument = 3,
    MediumReplacement = 4,
    Inactivity = 5,
    Other = 99,
}

/// <summary>Causa del retiro del asociado (R15).</summary>
public enum MembershipWithdrawalCause
{
    Voluntary = 1,
    Exclusion = 2,
    Death = 3,
    Other = 99,
}

/// <summary>
/// Qué pasa con el saldo de los créditos que el cruce al retiro no alcanza a cubrir (feature 014, decisión del dueño del 2026-10-18, D45): lo elige
/// quien liquida. Las obligaciones siguen con su saldo pendiente; la diferencia es por dónde reciben los pagos. (nuevo)
/// </summary>
public enum PendingLoanBalanceHandling
{
    /// <summary>Pasan a recibir los pagos por caja: las libranzas y la nómina pasan a la forma de pago alterna del convenio (caja por defecto).</summary>
    MoveToCashier = 1,

    /// <summary>Conservan su forma de pago (la libranza o el descuento de nómina siguen si el deudor sigue empleado).</summary>
    KeepRepaymentChannel = 2,
}

/// <summary>Estado del trámite de retiro del asociado (R15).</summary>
public enum MembershipWithdrawalStatus
{
    Filed = 1,
    Approved = 2,
    Settled = 3,
    Paid = 4,
    Desisted = 5,
    Rejected = 6,
    ToRedo = 7,
}

/// <summary>Clase de distribución de la asamblea (FR-045, FR-046).</summary>
public enum AssemblyDistributionKind
{
    Revaluation = 1,
    SurplusDistribution = 2,
    Amortization = 3,
}

/// <summary>Clase de aporte social; es dimensión de la matriz (<c>ContributionClass</c>).</summary>
public enum ContributionClass
{
    Minimum = 1,
    Ordinary = 2,
    Extraordinary = 3,
    Revalued = 4,
}

/// <summary>Banda del plazo de emisión de un CDAT o contractual; es dimensión de la matriz (<c>IssueTermBand</c>).</summary>
public enum IssueTermBand
{
    UnderSixMonths = 1,
    SixToTwelve = 2,
    TwelveToEighteen = 3,
    EighteenOrMore = 4,
}

/// <summary>
/// La marca de la cuenta en la matriz contable (research C20; contracts/contabilidad.md §1, dimensión 18): una sola, con precedencia
/// <c>Garnished</c> &gt; <c>GmfExempt</c> &gt; <c>Standard</c>. En la regla viaja por nombre; la inactividad no es marca, cambia el rol.
/// </summary>
public enum AccountFlag
{
    Standard = 1,
    GmfExempt = 2,
    Garnished = 3,
}

// ---------------------------------------------------------------------------------------------- data-model §10 --

/// <summary>Clase de una operación de cuenta (data-model §4.1); su nombre es el <c>ScopeKey</c> de su numeración.</summary>
public enum MemberAccountOperationKind
{
    OpeningBalance = 1,
    Deposit = 2,
    Withdrawal = 3,
    Transfer = 4,
    Reversal = 5,
    InterestSettlement = 6,
    TermDepositConstitution = 7,
    TermDepositInterestPayment = 8,
    TermDepositRollover = 9,
    TermDepositRenewal = 10,
    TermDepositRedemption = 11,
    Cancellation = 12,
    Charge = 13,
    ChargeWaiver = 14,
    InternalOffset = 15,
    DirectDebit = 16,
    ContributionRefund = 17,
    AssemblyCredit = 18,
    Reclassification = 19,
    ContractualMaturity = 20,
    ToUnclaimed = 21,
    DeceasedDelivery = 22,
    GarnishmentConsignment = 23,
}

/// <summary>De dónde nació una cuenta (data-model §3.1).</summary>
public enum MemberAccountOrigin
{
    Opening = 1,
    Migration = 2,
}

/// <summary>Estado del compromiso de un ahorro contractual.</summary>
public enum ContractualStatus
{
    Current = 1,
    Breached = 2,
    Fulfilled = 3,
}

/// <summary>Por qué se selló una versión de condiciones de la cuenta (data-model §3.3).</summary>
public enum AccountTermsCause
{
    Constitution = 1,
    Rollover = 2,
    Renewal = 3,
    Migration = 4,
    ContractualSetup = 5,
    ContractualBreach = 6,
    AgreedRate = 7,
    RateNegotiation = 8,
}

/// <summary>Adónde van los intereses liquidados de un CDAT o contractual.</summary>
public enum InterestDestination
{
    ToAccount = 1,
    PaymentOrder = 2,
    Capitalize = 3,
}

/// <summary>Instrucción al vencimiento de un CDAT.</summary>
public enum MaturityInstruction
{
    Rollover = 1,
    RenewWithNewTerms = 2,
    CancelAndPay = 3,
}

/// <summary>Cómo se penaliza la redención anticipada de un CDAT.</summary>
public enum EarlyRedemptionPenaltyMode
{
    PenaltyRate = 1,
    Discount = 2,
}

/// <summary>Qué pasa al incumplir un ahorro contractual.</summary>
public enum ContractualBreachEffect
{
    BreachRate = 1,
    MoveToDemand = 2,
    Cancel = 3,
}

/// <summary>Cuándo se reconoce la bonificación del contractual (P26, a validar).</summary>
public enum BonusRecognition
{
    AtFulfillment = 1,
    Provisioned = 2,
}

/// <summary>Qué dispara un cargo de la línea (FR-128).</summary>
public enum ChargeTrigger
{
    Periodic = 1,
    WithdrawalOverFree = 2,
    DepositOverFree = 3,
    ExtraDocument = 4,
    MediumReplacement = 5,
    Inactivity = 6,
    Manual = 7,
}

/// <summary>Cómo se calcula un cargo.</summary>
public enum ChargeCalculation
{
    FixedAmount = 1,
    Percentage = 2,
}

/// <summary>Sobre qué base se calcula un cargo porcentual.</summary>
public enum ChargeBase
{
    OperationAmount = 1,
    AverageBalance = 2,
}

/// <summary>Qué pasa con un cargo cuando el saldo no alcanza.</summary>
public enum ChargeShortfallPolicy
{
    KeepPending = 1,
    Waive = 2,
}

/// <summary>Estado de un cargo causado.</summary>
public enum AccountChargeStatus
{
    Collected = 1,
    Pending = 2,
    Waived = 3,
    Reversed = 4,
}

/// <summary>Cómo se fija el mínimo o la cuota de aportes (data-model §2.5).</summary>
public enum ContributionAmountMode
{
    FixedAmount = 1,
    IncomeFraction = 2,
    MinimumWageFraction = 3,
}

/// <summary>Qué pasa con un aporte que excede el límite de participación.</summary>
public enum ExcessContributionAction
{
    Reject = 1,
    RefundByDecision = 2,
}

/// <summary>Cómo se reparte lo que la pagaduría informa descontado por un código (data-model §1.4.2).</summary>
public enum ConceptAllocationRule
{
    Priority = 1,
    Proportional = 2,
}

/// <summary>Alcance de una autorización de débito automático.</summary>
public enum DirectDebitScope
{
    SingleLoan = 1,
    AllLoansOfHolder = 2,
}

/// <summary>Para qué es una orden de giro de cuentas (data-model §3.9).</summary>
public enum PayoutPurpose
{
    Withdrawal = 1,
    ContributionRefund = 2,
    DeceasedDelivery = 3,
    TermDepositInterest = 4,
    TermDepositRedemption = 5,
    GarnishmentConsignment = 6,
    Cancellation = 7,
}

/// <summary>Estado de un embargo.</summary>
public enum GarnishmentStatus
{
    Registered = 1,
    Active = 2,
    Released = 3,
    Rejected = 4,
}

/// <summary>Clase de respuesta al juzgado (data-model §5.2.1).</summary>
public enum GarnishmentResponseKind
{
    Acknowledgement = 1,
    BalanceReport = 2,
    ContributionsNotGarnishable = 3,
    ConsignmentNotice = 4,
    ReleaseAcknowledgement = 5,
}

/// <summary>Línea de la liquidación del retiro del asociado (data-model §6.2).</summary>
public enum MembershipWithdrawalLineKind
{
    ContributionBalance = 1,
    SavingsBalance = 2,
    TermDepositBalance = 3,
    InterestToDate = 4,
    Withholding = 5,
    Gmf = 6,
    LoanOffset = 7,
    LossDeduction = 8,
    Refund = 9,
}

/// <summary>Base de la revalorización de aportes (D13).</summary>
public enum RevaluationBase
{
    AverageBalance = 1,
    YearEndBalance = 2,
}

/// <summary>Destino de los excedentes distribuidos.</summary>
public enum SurplusDestination
{
    Contributions = 1,
    Savings = 2,
    Payment = 3,
}

/// <summary>Estado de una distribución de la asamblea.</summary>
public enum AssemblyDistributionStatus
{
    Simulated = 1,
    Approved = 2,
    Applied = 3,
    Voided = 4,
}

/// <summary>Por dónde se entrega lo de un titular fallecido.</summary>
public enum DeliveryRoute
{
    Beneficiaries = 1,
    WithoutProbate = 2,
    Probate = 3,
}

/// <summary>Estado de un caso de fallecimiento.</summary>
public enum DeceasedCaseStatus
{
    Registered = 1,
    Approved = 2,
    Delivered = 3,
    Cancelled = 4,
}

/// <summary>De dónde viene un saldo no reclamado.</summary>
public enum UnclaimedSource
{
    UncashedRefund = 1,
    InactiveBalance = 2,
    UncashedPayout = 3,
}

/// <summary>Estado de un saldo no reclamado.</summary>
public enum UnclaimedStatus
{
    Held = 1,
    Claimed = 2,
}

/// <summary>Clase de inversión del fondo de liquidez constituido.</summary>
public enum LiquidityHoldingKind
{
    BankDeposit = 1,
    TermInvestment = 2,
    TrustFund = 3,
    Other = 99,
}

/// <summary>Qué pide una solicitud de aportes (feature 014, US3, T086; research E58): la devolución sin retiro o la cesión a otro asociado.</summary>
public enum ContributionRequestKind
{
    Refund = 1,
    Assignment = 2,
}

/// <summary>Estado de una solicitud de devolución o de cesión de aportes (api.md §7: se crea en espera de aprobación).</summary>
public enum ContributionRequestStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
}

/// <summary>Por qué se pide la devolución de aportes sin retiro (api.md §7: exceso sobre el límite de participación u otra causa).</summary>
public enum ContributionRefundCause
{
    ParticipationExcess = 1,
    Other = 99,
}

/// <summary>
/// Qué trae una fila de la parte <c>tax-year-to-date</c> de la migración de captaciones (feature 014, US9, T151; contracts/plantillas-y-archivos.md
/// §4.8): los rendimientos, la retención, el GMF y el componente inflacionario del año del corte anteriores al corte, para el certificado tributario.
/// </summary>
public enum MigratedTaxConcept
{
    Interest = 1,
    Withholding = 2,
    Gmf = 3,
    InflationaryComponent = 4,
}
