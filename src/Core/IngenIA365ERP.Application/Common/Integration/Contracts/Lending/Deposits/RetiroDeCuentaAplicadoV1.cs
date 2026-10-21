namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>RetiroDeCuentaAplicado</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>RCT</c>; entrega K8): retiro (contrato, por giro, cancelación o débito automático del ciclo diario); <c>MA</c>. Admite proceso.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record RetiroDeCuentaAplicadoV1
{
    public const string Type = "RetiroDeCuentaAplicado";

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

    /// <summary>El recaudador, si el retiro llegó por el contrato.</summary>
    public CollectorRefV1? Collector { get; init; }

    /// <summary>La orden de giro, si el retiro es por giro.</summary>
    public Guid? DisbursementOrderPublicId { get; init; }

    /// <summary>El débito automático: el crédito que paga y su operación de pago.</summary>
    public InternalWithdrawalV1? Internal { get; init; }

    /// <summary>Valor de <c>ClaseDeMedioDeRecaudo</c>.</summary>
    public string MeansClass { get; init; } = string.Empty;

    /// <summary>El retiro cierra la cuenta.</summary>
    public bool ClosesAccount { get; init; }
}

/// <summary>El débito automático de un retiro (mensajes.md §4.1, <c>internal</c>): el crédito que paga y su operación de pago.</summary>
public sealed record InternalWithdrawalV1
{
    /// <summary><c>DirectDebit</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public LoanRefV1 Loan { get; init; } = new();

    public Guid PaymentOperationPublicId { get; init; }
}
