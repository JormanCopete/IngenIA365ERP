namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CreditoModificado</c> v1 (§10.20; <c>MOD</c>; K3, declarado en K1): origen <c>TermsChange</c> (la novedad aprobada), clave
/// <c>Confirmation</c>. Un cambio de dimensión que provoque va aparte en <c>DimensionContableCambiada</c>.
/// </summary>
public sealed record CreditoModificadoV1
{
    public const string Type = "CreditoModificado";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>Lo que la novedad mueve: intereses capitalizados, cargos refinanciados.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary>La combinación anterior (C22).</summary>
    public LoanFactDimensionsV1 PreviousDimensions { get; init; } = new();

    /// <summary><c>LND_LoanNovelties</c>.</summary>
    public Guid NoveltyPublicId { get; init; }

    /// <summary>El punto del acta que la aprobó (T64).</summary>
    public Guid? SessionItemPublicId { get; init; }

    public LoanPlanSummaryV1 PreviousPlan { get; init; } = new();

    public LoanPlanSummaryV1 NewPlan { get; init; } = new();

    public int RestructuringCount { get; init; }

    /// <summary>Sólo en <c>CreditoNovado</c>: el crédito nuevo.</summary>
    public LoanRefV1? NewLoan { get; init; }
}
