namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CreditoAjustadoDesdeComercial</c> v1 (§10.3; <c>ACO</c>): el consumo de un <c>AjusteDeVentaACredito</c>. Origen
/// <c>CommercialAdjustment</c>, clave <c>Confirmation</c>, <c>related</c> = el desembolso en especie. Valores con signo.
/// </summary>
public sealed record CreditoAjustadoDesdeComercialV1
{
    public const string Type = "CreditoAjustadoDesdeComercial";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>Con signo: negativo baja el capital (y reversa el interés causado sobre lo devuelto), positivo lo sube.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary><c>CreditNote</c>, <c>Return</c>, <c>DebitNote</c>, <c>Voiding</c>, <c>VoidingByDianRejection</c>.</summary>
    public string AdjustmentClass { get; init; } = string.Empty;

    /// <summary>El <c>AjusteDeVentaACredito</c> consumido.</summary>
    public Guid AdjustmentMessageId { get; init; }

    /// <summary>La nota, la devolución o la anulación.</summary>
    public ExternalDocumentRefV1 AdjustmentDocument { get; init; } = new();

    /// <summary>La venta original.</summary>
    public ExternalDocumentRefV1 Invoice { get; init; } = new();

    /// <summary>El sello de la venta: <c>Contabilidad</c> o <c>Cartera</c> (FR-056).</summary>
    public string AccountsReceivableRecordedBy { get; init; } = string.Empty;

    /// <summary>Lo que supera lo adeudado (viaja en el acompañante <c>SaldoAFavorReconocido</c>).</summary>
    public decimal CreditBalanceRecognized { get; init; }

    /// <summary>En las anulaciones: el crédito queda sin efecto desde su origen.</summary>
    public bool VoidedFromOrigin { get; init; }

    /// <summary>El plan recalculado (FR-031).</summary>
    public LoanPlanSummaryV1? NewPlan { get; init; }

    /// <summary>Una nota débito evaluada en contra marca el crédito (FR-052).</summary>
    public bool ValidationFailed { get; init; }
}
