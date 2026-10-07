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

    /// <summary>Los diez, en el orden de S4 y C52.</summary>
    public static IReadOnlyList<string> Todos { get; } =
    [
        CreditApplication, CreditLimit, Novelty, Condonation, WriteOffBatch, PortfolioEvaluation, ImpairmentModelChange,
        ActivationDifferences, ValidationFailureException, RiskPolicyVersion,
    ];

    public static bool Existe(string? sujeto) => sujeto is not null && Todos.Contains(sujeto, StringComparer.Ordinal);
}
