namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>CdatRedimido</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>CDD</c>; entrega K9): redención anticipada (FR-071); <c>TD</c>. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record CdatRedimidoV1
{
    public const string Type = "CdatRedimido";

    /// <summary>La cuenta y su número (el documento cruce <c>CTA</c>).</summary>
    public MemberAccountRefV1 Account { get; init; } = new();

    /// <summary>Titular (el tercero del sobre puede ser otro: el depositante, el pagador, el beneficiario).</summary>
    public Guid HolderPersonPublicId { get; init; }

    /// <summary>Fecha valor.</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones <b>vigentes</b> a la fecha contable.</summary>
    public DepositFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>Valores por concepto, con signo (+ = la Cartera le debe más al titular, R03).</summary>
    public IReadOnlyList<DepositConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (reversos, acompañantes); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary>Número visible de la operación.</summary>
    public string OperationNumber { get; init; } = string.Empty;

    /// <summary>Versión de reglas sellada (<c>LND-AAAA.n</c>).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    public TermDepositTermsV1 Terms { get; init; } = new();

    public TermDepositPenaltyV1 Penalty { get; init; } = new();

    public TermDepositDestinationV1 Destination { get; init; } = new();
}

/// <summary>La penalidad de una redención anticipada (mensajes.md §4.1, <c>penalty</c>).</summary>
public sealed record TermDepositPenaltyV1
{
    /// <summary>Valor de <c>EarlyRedemptionPenaltyMode</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public decimal? RepricedRate { get; init; }

    public decimal Amount { get; init; }
}
