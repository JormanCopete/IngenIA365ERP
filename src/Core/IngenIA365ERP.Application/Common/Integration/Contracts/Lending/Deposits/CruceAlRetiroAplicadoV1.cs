namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>CruceAlRetiroAplicado</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>CRR</c>; entrega K7): liquidación del retiro del asociado: el débito de aportes y ahorros que paga los créditos (FR-077); <c>RA</c>. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record CruceAlRetiroAplicadoV1
{
    public const string Type = "CruceAlRetiroAplicado";

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

    public MembershipWithdrawalRefV1 Withdrawal { get; init; } = new();

    /// <summary>Un cruce por crédito pagado.</summary>
    public IReadOnlyList<WithdrawalOffsetV1> Offsets { get; init; } = [];
}

/// <summary>El retiro del asociado que se liquida (mensajes.md §4.1, <c>withdrawal</c>).</summary>
public sealed record MembershipWithdrawalRefV1
{
    public Guid PublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    /// <summary>Valor de <c>MembershipWithdrawalCause</c>.</summary>
    public string Cause { get; init; } = string.Empty;
}

/// <summary>Un crédito pagado con el cruce al retiro y su operación de pago (<c>OwnLoanPayoff</c>).</summary>
public sealed record WithdrawalOffsetV1
{
    public LoanRefV1 Loan { get; init; } = new();

    public Guid PaymentOperationPublicId { get; init; }

    public decimal Amount { get; init; }
}
