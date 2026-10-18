namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>CdatConstituido</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>CDC</c>; entrega K9): fondeo del CDAT (consignación exacta o traslado); <c>TD</c>. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record CdatConstituidoV1
{
    public const string Type = "CdatConstituido";

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

    public TermDepositFundingV1 Funding { get; init; } = new();

    public NegotiatedRateRefV1? Negotiated { get; init; }
}

/// <summary>Cómo se fondeó un CDAT (mensajes.md §4.1, <c>funding</c>).</summary>
public sealed record TermDepositFundingV1
{
    /// <summary><c>Collector</c> o <c>TransferFromAccount</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public CollectorRefV1? Collector { get; init; }

    public MemberAccountRefV1? SourceAccount { get; init; }
}

/// <summary>La regla de tasa negociada y quién la aprobó (mensajes.md §4.1, <c>negotiated</c>).</summary>
public sealed record NegotiatedRateRefV1
{
    public Guid RulePublicId { get; init; }

    public Guid ApproverUserPublicId { get; init; }
}

/// <summary>A dónde va el dinero de un CDAT que se paga, redime o cancela (mensajes.md §4.1, <c>destination</c>).</summary>
public sealed record TermDepositDestinationV1
{
    /// <summary><c>Account</c> (abono en otra cuenta del asociado) o <c>DisbursementOrder</c> (giro por Tesorería).</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>La cuenta destino, si el abono es en cuenta.</summary>
    public MemberAccountRefV1? TargetAccount { get; init; }

    /// <summary>La orden de giro, si se gira.</summary>
    public Guid? OrderPublicId { get; init; }
}
