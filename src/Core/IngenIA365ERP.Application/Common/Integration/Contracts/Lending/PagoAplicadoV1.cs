namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>PagoAplicado</c> v1 (§10.7; <c>PAP</c>): el pago aplicado por el contrato de obligaciones. Origen <c>PaymentApplication</c>,
/// clave <c>Confirmation</c>, tercero el pagador. Principal de su unidad (§7.2). Nunca lo origina el proceso (T11, C14).
/// <c>Amounts</c> lleva sólo lo aplicado a la deuda; el resto va en sus acompañantes.
/// </summary>
public sealed record PagoAplicadoV1
{
    public const string Type = "PagoAplicado";

    public LoanRefV1 Loan { get; init; } = new();

    public Guid DebtorPersonPublicId { get; init; }

    public DateOnly ValueDate { get; init; }

    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Recaudador, clave, clase de medio, pagador y recibo.</summary>
    public CollectorRefV1 Collector { get; init; } = new();

    /// <summary>Valor de <c>PaymentModality</c> (FR-071).</summary>
    public string Modality { get; init; } = string.Empty;

    public decimal AmountReceived { get; init; }

    /// <summary>El excedente que vuelve al recaudador (D5); no se contabiliza en Cartera.</summary>
    public decimal Surplus { get; init; }

    /// <summary>Con <c>collectorClass</c> = la clase del recaudador (T53).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>Redistribución de pagos posteriores por una fecha valor anterior (FR-081).</summary>
    public IReadOnlyList<ReallocationV1> Reallocations { get; init; } = [];

    /// <summary>Valor de <c>PrepaymentChoice</c> del abono extraordinario.</summary>
    public string? PrepaymentChoice { get; init; }

    public LoanPlanSummaryV1? NewPlan { get; init; }

    public bool PaidOff { get; init; }

    /// <summary>Firmeza de la cancelación (FR-082, D23).</summary>
    public DateOnly? FinalityUntil { get; init; }

    /// <summary>El de la forma interactiva (T32).</summary>
    public string? SimulationId { get; init; }

    /// <summary>K2: la línea «descontado pendiente de giro» que cubre.</summary>
    public Guid? DeductionFileLinePublicId { get; init; }

    /// <summary>La reclamación al seguro que el pago de la aseguradora cierra (T102).</summary>
    public Guid? InsuranceClaimPublicId { get; init; }

    public string RulesVersion { get; init; } = string.Empty;
}
