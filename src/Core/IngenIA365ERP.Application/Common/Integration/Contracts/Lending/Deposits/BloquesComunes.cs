namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

// Feature 014 (T047; contracts/mensajes.md §3, research R19): los bloques comunes de los hechos de captaciones y aportes, en v1. Se
// serializan como los de la 013 (OpcionesDeMensajes: camelCase en el orden en que se declaran, nulos escritos, decimales exactos). Los
// valores de los enums de la Cartera (LineNature, DepositClass, IssueTermBand, ContributionClass, AccountFlag, MemberAccountConcept,
// InterestTiming, GmfExemptionPolicy…) viajan como texto con su nombre, igual que en la 013: el JSON es el mismo con el enum o sin él y el
// contrato no depende del dominio. «Account» aquí nombra la cuenta del asociado (MemberAccountRefV1), nunca una cuenta del plan: ningún
// bloque lleva cuentas contables (FR-097, SC-009; LosHechosDeCarteraNoLlevanCuentas). Se reutilizan de la 013 CollectorRefV1,
// ReceiptRefV1, OriginalFactRefV1, PayeeV1, LoanRefV1 y UserRefV1. Un cambio incompatible de un bloque es un bloque V2 y, con él, una V2 de
// cada mensaje que lo usa.

/// <summary><c>MemberAccountRefV1</c> (§3): la cuenta del asociado por su <c>PublicId</c> y su número, que es el documento cruce <c>CTA</c>.</summary>
public sealed record MemberAccountRefV1
{
    public Guid AccountPublicId { get; init; }

    public string AccountNumber { get; init; } = string.Empty;
}

/// <summary>
/// <c>DepositFactDimensionsV1</c> (§3; FR-097, R19): las dimensiones de la matriz que no dependen del concepto, <b>vigentes</b> a la fecha
/// contable del hecho.
/// </summary>
public sealed record DepositFactDimensionsV1
{
    /// <summary>Código de la línea (la identidad es común, R01); dimensión <c>CreditLineCode</c>.</summary>
    public string CreditLineCode { get; init; } = string.Empty;

    /// <summary>Valor de <c>LineNature</c>: <c>Deposit</c> o <c>Contribution</c>.</summary>
    public string LineNature { get; init; } = string.Empty;

    /// <summary>Valor de <c>DepositClass</c> (<c>Demand</c>, <c>Contractual</c>, <c>Permanent</c>, <c>TermDeposit</c>); nulo en aportes.</summary>
    public string? DepositClass { get; init; }

    /// <summary>Valor de <c>IssueTermBand</c> por el plazo <b>pactado</b> de la versión vigente; sólo en CDAT y contractual.</summary>
    public string? IssueTermBand { get; init; }

    /// <summary>Valor de <c>AccountFlag</c>: <c>Standard</c>, <c>GmfExempt</c> o <c>Garnished</c> (precedencia de research C20).</summary>
    public string AccountFlag { get; init; } = string.Empty;

    /// <summary>La cuenta está inactiva: decide <c>CapitalDeCaptacion</c> o <c>CapitalDeCaptacionInactiva</c> (C20).</summary>
    public bool Inactive { get; init; }

    /// <summary>Si el titular es empleado, según la persona maestra hoy (C8 de la 013).</summary>
    public bool IsEmployee { get; init; }

    /// <summary>Sucursal contable de la oficina de la cuenta.</summary>
    public Guid BranchPublicId { get; init; }
}

/// <summary><c>DepositConceptAmountV1</c> (§3): un valor por concepto del libro de cuentas, con su signo (+ = la Cartera le debe más al titular, R03).</summary>
public sealed record DepositConceptAmountV1
{
    /// <summary>Valor de <c>MemberAccountConcept</c> (<c>Principal</c>, <c>InterestPayable</c>, <c>Withholding</c>, <c>Gmf</c>, <c>Contribution*</c>…).</summary>
    public string Concept { get; init; } = string.Empty;

    /// <summary>Valor de <c>ContributionClass</c> en los conceptos de aportes (viaja para no recalcularla); dimensión <c>ContributionClass</c>.</summary>
    public string? ContributionClass { get; init; }

    public decimal Amount { get; init; }

    /// <summary>El valor con los decimales de la causación, sólo en <c>InteresDeCaptacionCausado</c> (la suma redondeada va en <see cref="Amount"/>).</summary>
    public decimal? AccruedAmount { get; init; }

    /// <summary>Dimensión <c>ReasonCode</c> cuando el rol la exige: el <c>OrderKind</c> de una orden, el <c>ChargeKind</c> de un cargo, la causa de una reclasificación.</summary>
    public string? ReasonCode { get; init; }
}

/// <summary><c>DepositFactLineV1</c> (§3): una línea por cuenta en las partes del paso de captaciones y en las formas masivas (asamblea).</summary>
public sealed record DepositFactLineV1
{
    public MemberAccountRefV1 Account { get; init; } = new();

    public Guid HolderPersonPublicId { get; init; }

    public DepositFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>En la reclasificación: las dimensiones anteriores.</summary>
    public DepositFactDimensionsV1? PreviousDimensions { get; init; }

    public IReadOnlyList<DepositConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El día valor de un ajuste.</summary>
    public DateOnly? ValueDate { get; init; }
}

/// <summary><c>DepositFactTotalV1</c> (§3): los totales de una parte por combinación de dimensiones.</summary>
public sealed record DepositFactTotalV1
{
    public DepositFactDimensionsV1 Dimensions { get; init; } = new();

    public IReadOnlyList<DepositConceptAmountV1> Amounts { get; init; } = [];

    public int AccountCount { get; init; }
}

/// <summary><c>TermDepositTermsV1</c> (§3): la versión sellada de las condiciones de un CDAT.</summary>
public sealed record TermDepositTermsV1
{
    public int TermsVersion { get; init; }

    public string TitleNumber { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public int TermDays { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly MaturityDate { get; init; }

    /// <summary>Tasa efectiva anual pactada, como fracción.</summary>
    public decimal EffectiveAnnual { get; init; }

    /// <summary>Tasa de la tabla de la línea, como fracción.</summary>
    public decimal TableRate { get; init; }

    /// <summary>Margen negociado sobre la tabla, en puntos efectivos anuales, como fracción.</summary>
    public decimal? NegotiatedMarginEa { get; init; }

    /// <summary>Cada cuántos meses se pagan los intereses (0 = al vencimiento).</summary>
    public int InterestPeriodicity { get; init; }

    /// <summary>Valor de <c>InterestTiming</c>: <c>Arrears</c> o <c>Advance</c>.</summary>
    public string Timing { get; init; } = string.Empty;

    /// <summary>Base de días (360 o 365).</summary>
    public int DayCount { get; init; }

    /// <summary>Valor de <c>IssueTermBand</c>.</summary>
    public string IssueTermBand { get; init; } = string.Empty;

    public int RolloverNumber { get; init; }

    public string RulesVersion { get; init; } = string.Empty;
}

/// <summary><c>WithholdingDetailV1</c> (§3; FR-021, FR-063): el detalle de la retención practicada, para el certificado.</summary>
public sealed record WithholdingDetailV1
{
    public decimal Base { get; init; }

    public decimal? InflationaryComponentPercent { get; init; }

    public decimal TaxableBase { get; init; }

    public decimal Rate { get; init; }

    public decimal ThresholdUvtPerDay { get; init; }

    public decimal Uvt { get; init; }

    public bool ThresholdReached { get; init; }

    /// <summary><c>Standard</c>, <c>NonTaxpayer</c>, <c>SelfWithholder</c> o <c>NoProfile</c>.</summary>
    public string HolderTaxProfile { get; init; } = string.Empty;

    public int Year { get; init; }

    public string ConceptCode { get; init; } = string.Empty;
}

/// <summary>
/// <c>GmfDetailV1</c> (§3; FR-022): el detalle del GMF. Las dimensiones del hecho son las de la cuenta <b>debitada</b>;
/// <see cref="GeneratingDepositClass"/> es la clase del producto que generó el hecho y decide la clase de <c>GmfPorPagar</c>.
/// </summary>
public sealed record GmfDetailV1
{
    public decimal Base { get; init; }

    /// <summary>Valor de <c>GmfExemptionPolicy</c>.</summary>
    public string Policy { get; init; } = string.Empty;

    /// <summary>La cuenta marcada exenta del titular, si la hay (la cuenta del asociado, no una cuenta del plan).</summary>
    public Guid? ExemptAccountPublicId { get; init; }

    public decimal ExemptUsedBefore { get; init; }

    public decimal ExemptApplied { get; init; }

    public decimal TaxedBase { get; init; }

    public decimal Rate { get; init; }

    public string GeneratingEvent { get; init; } = string.Empty;

    /// <summary>Valor de <c>DepositClass</c> del producto que generó el hecho.</summary>
    public string GeneratingDepositClass { get; init; } = string.Empty;
}
