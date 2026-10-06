namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>SaldoAFavorResuelto</c> v1 (§10.13; <c>SFS</c>): origen <c>CreditBalanceResolution</c>, clave <c>Confirmation</c>.
/// </summary>
public sealed record SaldoAFavorResueltoV1
{
    public const string Type = "SaldoAFavorResuelto";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary><c>CreditBalance</c> o <c>Advance</c> que baja.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary><c>CreditBalance</c> o <c>Advance</c>.</summary>
    public string BalanceKind { get; init; } = string.Empty;

    /// <summary><c>RefundThroughCollector</c>, <c>ApplyToOtherObligation</c> o <c>ApplyAsExtraordinaryPrincipal</c>.</summary>
    public string Resolution { get; init; } = string.Empty;

    public CollectorRefV1 Collector { get; init; } = new();

    /// <summary>El <c>PagoAplicado</c> de la aplicación.</summary>
    public OriginalFactRefV1? TargetPayment { get; init; }

    /// <summary>La obligación destino.</summary>
    public LoanRefV1? TargetLoan { get; init; }
}
