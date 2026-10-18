namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>OperacionDeCuentaReversada</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>RVC</c>; entrega K7): reverso de cualquier operación de cuenta, espejo exacto con las dimensiones vigentes a la fecha contable (la 013 §9); hereda el modo y el comprobante del original. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record OperacionDeCuentaReversadaV1
{
    public const string Type = "OperacionDeCuentaReversada";

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

    public string Reason { get; init; } = string.Empty;

    /// <summary><c>Collector</c>, <c>Lending</c>, <c>TreasuryResult</c> o <c>LoanPaymentReversal</c>.</summary>
    public string InitiatedBy { get; init; } = string.Empty;

    /// <summary>Un bloque por cada hecho de la unidad original.</summary>
    public IReadOnlyList<DepositFactMirrorV1> Mirrors { get; init; } = [];
}

/// <summary>El espejo de un hecho de la unidad original (mensajes.md §4.1, <c>mirrors</c>).</summary>
public sealed record DepositFactMirrorV1
{
    /// <summary>El <c>Type</c> del hecho original.</summary>
    public string Type { get; init; } = string.Empty;

    public IReadOnlyList<DepositConceptAmountV1> Amounts { get; init; } = [];
}
