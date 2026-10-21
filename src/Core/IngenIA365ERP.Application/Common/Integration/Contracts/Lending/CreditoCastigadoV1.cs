namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CreditoCastigado</c> v1 (§10.22; <c>CAS</c>; K3, declarado en K1): origen <c>WriteOff</c> (ítem del lote), clave
/// <c>Confirmation</c>. Aparte nace <c>CausacionSuspendida</c> (<c>cause = WriteOff</c>).
/// </summary>
public sealed record CreditoCastigadoV1
{
    public const string Type = "CreditoCastigado";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>Saldos castigados por concepto, con <c>reasonCode</c> = causal.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    public Guid WriteOffBatchPublicId { get; init; }

    /// <summary>El punto del acta del consejo.</summary>
    public Guid SessionItemPublicId { get; init; }

    public IReadOnlyList<ConceptAmountV1> ImpairmentApplied { get; init; } = [];

    public decimal ImpairmentShortfall { get; init; }

    /// <summary>El suspenso en orden que pasa a castigada en orden (C81).</summary>
    public IReadOnlyList<ConceptAmountV1> SuspenseWrittenOff { get; init; } = [];
}
