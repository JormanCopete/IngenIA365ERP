namespace IngenIA365ERP.Domain.Enums.Lending;

// Condiciones financieras de la línea y del crédito (feature 013, T0201; decisiones-transversales §2.7 «M»; data-model §9.1 y
// parte 1 §10, C65). Las sellan la versión de la línea y la versión de condiciones del crédito.

/// <summary>Convención de días.</summary>
public enum DayCountConvention
{
    Thirty360 = 1,
    Actual365 = 2,
    Actual360 = 3,
}

/// <summary>Equivalencia de tasas (FR-010): por defecto compuesta.</summary>
public enum RateEquivalence
{
    Compound = 1,
    Linear = 2,
}

/// <summary>Sistema de amortización.</summary>
public enum AmortizationSystem
{
    LevelPayment = 1,
    ConstantPrincipal = 2,
    Bullet = 3,
}

/// <summary>Clase de gracia.</summary>
public enum GraceKind
{
    None = 0,
    Principal = 1,
    Total = 2,
}

/// <summary>Qué se hace con el interés de la gracia: en la primera cuota o distribuido en N.</summary>
public enum GraceInterestTreatment
{
    FirstInstallment = 1,
    Distributed = 2,
}

/// <summary>Modalidad de un pago (FR-071); <c>ModalidadDePago</c> del contrato lleva los mismos nombres y números.</summary>
public enum PaymentModality
{
    FullPayoff = 1,
    PastDue = 2,
    CycleInstallment = 3,
    Advance = 4,
    ExtraordinaryPrincipal = 5,
    SpecificInstallment = 6,
    FreeAmount = 7,
}

/// <summary>Elección del deudor ante un abono extraordinario a capital (FR-075, D24).</summary>
public enum PrepaymentChoice
{
    ReduceTerm = 1,
    ReduceInstallment = 2,
}

/// <summary>Orden de la prelación (FR-030).</summary>
public enum AllocationOrder
{
    OldestInstallmentFirst = 1,
    ConceptFirst = 2,
}

/// <summary>Momento en que se redondea.</summary>
public enum RoundingMoment
{
    PerConcept = 1,
    PerInstallment = 2,
    PerTotal = 3,
}

/// <summary>Modo de redondeo.</summary>
public enum RoundingMode
{
    AwayFromZero = 1,
    ToEven = 2,
    Up = 3,
    Down = 4,
}

/// <summary>Tasa corriente fija o variable (FR-020, FR-067).</summary>
public enum RateKind
{
    Fixed = 1,
    Variable = 2,
}

/// <summary>Tasa de mora fija o relativa a la corriente.</summary>
public enum DefaultRateKind
{
    Fixed = 1,
    RelativeToCurrent = 2,
}

/// <summary>
/// Periodicidad de pago y de reajuste. <b>El valor es la base de proporción</b> (días del período comercial), precedente de
/// <c>PayrollPeriodicity</c>: así 7 y 10 nunca son literales del motor (CH39). <c>SinglePayment</c> = pago único.
/// </summary>
public enum RepaymentPeriodicity
{
    SinglePayment = 0,
    Weekly = 7,
    TenDay = 10,
    Biweekly = 15,
    Monthly = 30,
    Bimonthly = 60,
    Quarterly = 90,
    SemiAnnual = 180,
    Annual = 360,
}

/// <summary>Forma de pago de un crédito (FR-027). <c>PaymasterDeduction</c> se opera desde K2.</summary>
public enum RepaymentChannel
{
    Cashier = 1,
    OwnPayroll = 2,
    PaymasterDeduction = 3,
    DirectDebit = 4,
}

/// <summary>Base de la prima de un seguro de la línea.</summary>
public enum InsuranceBase
{
    OutstandingBalance = 1,
    InitialAmount = 2,
}

/// <summary>Cuándo se causa el seguro (D9: al vencer la cuota del período).</summary>
public enum InsuranceAccrualTiming
{
    AtDueDate = 1,
    Daily = 2,
}

/// <summary>Concepto tributario del interés (D20, T44).</summary>
public enum InterestTaxTreatment
{
    NotTaxed = 1,
    Taxed = 2,
}
