namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>PagoReversado</c> v1 (§10.8; <c>PRV</c>): origen <c>PaymentReversal</c>, clave <c>Confirmation</c>, <c>related</c> = el pago;
/// hereda el modo del pago (C08). Lo origina el recaudador o un usuario de Cartera; los valores restituyen el saldo con las
/// dimensiones vigentes (T55).
/// </summary>
public sealed record PagoReversadoV1
{
    public const string Type = "PagoReversado";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>Positivos: lo que el reverso restituye por concepto y cuota.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary><c>Collector</c> o <c>Lending</c>.</summary>
    public string InitiatedBy { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    /// <summary>El del pago reversado.</summary>
    public CollectorRefV1 Collector { get; init; } = new();

    public decimal ReversedAmount { get; init; }

    public IReadOnlyList<ReallocationV1> Reallocations { get; init; } = [];

    /// <summary>El pago reversado había cancelado el crédito.</summary>
    public bool Reopened { get; init; }
}
