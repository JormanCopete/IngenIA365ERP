namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>SaldoAFavorReconocido</c> v1 (§10.12; <c>SFR</c>): acompañante del pago sin preferencia o del ajuste de Comercial (clave
/// <c>Confirmation</c>), o hecho por crédito de la corrida <c>NationalValueCorrection</c> (clave <c>Confirmation:{crédito:N}</c>).
/// Admite proceso. Es un pasivo de Cartera con el deudor (D5).
/// </summary>
public sealed record SaldoAFavorReconocidoV1
{
    public const string Type = "SaldoAFavorReconocido";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary><c>CreditBalance</c>.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary><c>NoPrepaymentPreference</c>, <c>CommercialAdjustment</c> o <c>NationalValueCorrection</c>.</summary>
    public string Cause { get; init; } = string.Empty;

    public OriginalFactRefV1? CauseFact { get; init; }
}
