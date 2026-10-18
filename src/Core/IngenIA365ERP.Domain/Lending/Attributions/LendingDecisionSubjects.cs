namespace IngenIA365ERP.Domain.Lending.Attributions;

/// <summary>
/// Los sujetos de una decisión de Cartera (feature 013, S4, C52; data-model parte 1 §3; T0201/T0275): texto en
/// <c>LND_CreditDecisions.SubjectType</c>, <c>LND_AttributionRules.Subject</c>, <c>LND_SessionItems.SubjectType</c> y
/// <c>LND_CaseParticipations.SubjectType</c> (nvarchar(40)). <see cref="RiskPolicyVersion"/> liga la política SARC del consejo a un
/// punto de acta (T45, C52). (nuevo)
/// </summary>
public static class LendingDecisionSubjects
{
    public const string CreditApplication = nameof(CreditApplication);
    public const string CreditLimit = nameof(CreditLimit);
    public const string Novelty = nameof(Novelty);
    public const string Condonation = nameof(Condonation);
    public const string WriteOffBatch = nameof(WriteOffBatch);
    public const string PortfolioEvaluation = nameof(PortfolioEvaluation);
    public const string ImpairmentModelChange = nameof(ImpairmentModelChange);
    public const string ActivationDifferences = nameof(ActivationDifferences);
    public const string ValidationFailureException = nameof(ValidationFailureException);
    public const string RiskPolicyVersion = nameof(RiskPolicyVersion);

    // Feature 014 (T019; research C06; contracts/api.md §1.6): los sujetos de captaciones y aportes, al final. Los decide la matriz de
    // atribuciones o un órgano, como los del crédito; cada historia registra el suyo cuando lo usa.

    /// <summary>Un retiro de una cuenta de captación que supera la atribución del usuario (FR-051).</summary>
    public const string DepositWithdrawal = nameof(DepositWithdrawal);

    /// <summary>Una tasa negociada sobre la de la tabla (FR-127, R13).</summary>
    public const string NegotiatedRate = nameof(NegotiatedRate);

    /// <summary>El retiro del asociado (consejo o junta, R15).</summary>
    public const string MembershipWithdrawal = nameof(MembershipWithdrawal);

    /// <summary>La entrega de los depósitos de un titular fallecido (FR-082).</summary>
    public const string DeceasedDelivery = nameof(DeceasedDelivery);

    /// <summary>La condonación de un cargo o la exoneración de una cuenta (FR-128).</summary>
    public const string ChargeWaiver = nameof(ChargeWaiver);

    /// <summary>La redención anticipada de un CDAT (FR-071).</summary>
    public const string TermDepositRedemption = nameof(TermDepositRedemption);

    /// <summary>Una devolución de aportes sin retiro (FR-042, FR-044).</summary>
    public const string ContributionRefund = nameof(ContributionRefund);

    /// <summary>Una distribución de la asamblea: revalorización, excedentes o amortización (FR-045, FR-046).</summary>
    public const string AssemblyDistribution = nameof(AssemblyDistribution);

    /// <summary>El registro de un embargo (FR-055).</summary>
    public const string GarnishmentRegistration = nameof(GarnishmentRegistration);

    /// <summary>Los diez del crédito, en el orden de S4 y C52, y los nueve de captaciones (feature 014, C06).</summary>
    public static IReadOnlyList<string> Todos { get; } =
    [
        CreditApplication, CreditLimit, Novelty, Condonation, WriteOffBatch, PortfolioEvaluation, ImpairmentModelChange,
        ActivationDifferences, ValidationFailureException, RiskPolicyVersion,
        DepositWithdrawal, NegotiatedRate, MembershipWithdrawal, DeceasedDelivery, ChargeWaiver, TermDepositRedemption, ContributionRefund,
        AssemblyDistribution, GarnishmentRegistration,
    ];

    /// <summary>Feature 014 (T019): los sujetos de captaciones y aportes.</summary>
    public static IReadOnlyList<string> DeCaptaciones { get; } =
    [
        DepositWithdrawal, NegotiatedRate, MembershipWithdrawal, DeceasedDelivery, ChargeWaiver, TermDepositRedemption, ContributionRefund,
        AssemblyDistribution, GarnishmentRegistration,
    ];

    public static bool Existe(string? sujeto) => sujeto is not null && Todos.Contains(sujeto, StringComparer.Ordinal);
}
