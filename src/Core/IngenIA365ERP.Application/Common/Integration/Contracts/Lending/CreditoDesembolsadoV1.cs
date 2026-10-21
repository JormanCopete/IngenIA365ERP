namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CreditoDesembolsado</c> v1 (contracts/mensajes.md §10.1; <c>CAR → Accounting</c>, código corto <c>DES</c>): cada tramo
/// desembolsado, también en especie desde una venta. Origen <c>LoanDisbursement</c>, clave <c>Confirmation</c>; lo origina la persona
/// (o, en especie, el consumo de la venta con el cajero como usuario de origen). Sin cuentas contables (FR-126).
/// </summary>
public sealed record CreditoDesembolsadoV1
{
    public const string Type = "CreditoDesembolsado";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>El capital del tramo con <c>reasonCode</c> = la forma (en especie, quién registró la cuenta por cobrar) y los cargos financiados.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    public TrancheV1 Tranche { get; init; } = new();

    /// <summary><c>Transfer</c>, <c>Check</c>, <c>ThirdPartyPayment</c> o <c>InKind</c>.</summary>
    public string DisbursementForm { get; init; } = string.Empty;

    /// <summary>El beneficiario sellado en la orden; nulo en especie.</summary>
    public Guid? PayeePersonPublicId { get; init; }

    /// <summary>Valor del tramo.</summary>
    public decimal GrossAmount { get; init; }

    /// <summary>Créditos recogidos (FR-046), cada uno con su propio <c>PagoAplicado</c>.</summary>
    public IReadOnlyList<PayoffDeductionV1> PayoffDeductions { get; init; } = [];

    /// <summary><c>GrossAmount − Σ PayoffDeductions.Amount</c>; cero en especie.</summary>
    public decimal NetToDisburse { get; init; }

    /// <summary>La orden de giro; nula en especie.</summary>
    public Guid? DisbursementOrderPublicId { get; init; }

    /// <summary>Sólo en especie (FR-050, FR-056).</summary>
    public InKindDisbursementV1? InKind { get; init; }

    /// <summary>El plan sellado (o regenerado en cada tramo, D12).</summary>
    public LoanPlanSummaryV1 Plan { get; init; } = new();
}
