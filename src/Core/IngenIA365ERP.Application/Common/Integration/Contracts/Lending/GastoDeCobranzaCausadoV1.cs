namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>GastoDeCobranzaCausado</c> v1 (§10.19; <c>GCO</c>; K4, declarado en K1): origen <c>LoanCharge</c>, clave <c>Confirmation</c>.
/// Sólo después del envío del mensaje <c>CollectionFeeNotice</c> (T77).
/// </summary>
public sealed record GastoDeCobranzaCausadoV1
{
    public const string Type = "GastoDeCobranzaCausado";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary><c>CollectionFee</c>.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary>El mensaje al deudor que informó el gasto.</summary>
    public Guid NoticeDebtorMessagePublicId { get; init; }

    /// <summary>Etapa de cobranza.</summary>
    public string StageCode { get; init; } = string.Empty;

    /// <summary>Casa de cobranza o abogado, si es una cuenta por pagar a un tercero.</summary>
    public Guid? PayeePersonPublicId { get; init; }
}
