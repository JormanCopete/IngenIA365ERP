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

    // Feature 014, US8 (T143; research E106): a dónde iba la contrapartida del original, para que Contabilidad arme el espejo sin leer tablas de la
    // Cartera. Opcionales al final (un lector anterior los ignora); nulos cuando el original no los tenía.

    /// <summary>La clase del recaudador del original (consignación, aporte, retiro por el contrato; <c>OwnLoanPayoff</c> en el débito automático y el cruce).</summary>
    public string? CollectorClass { get; init; }

    /// <summary>El <c>OrderKind</c> de la orden de giro del original (retiro por giro, devolución; mensajes.md §5.3).</summary>
    public string? OrderKind { get; init; }

    /// <summary>El motivo de la contrapartida del original: el <c>ChargeKind</c> de un cargo o de su condonación.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>US11 (T189; research E163): el tercero de la contrapartida del original (el juzgado de una consignación de embargo); opcional al final.</summary>
    public Guid? CounterpartPersonPublicId { get; init; }

    /// <summary>
    /// Respuesta 89 B (E260): el reverso entero de una decisión de asamblea con retención devuelve también lo retenido (valores positivos, los que el original
    /// retuvo) del retención por pagar al fondo. Vacío en los demás reversos.
    /// </summary>
    public IReadOnlyList<AssemblyWithholdingV1> Withholdings { get; init; } = [];
}

/// <summary>El espejo de un hecho de la unidad original (mensajes.md §4.1, <c>mirrors</c>).</summary>
public sealed record DepositFactMirrorV1
{
    /// <summary>El <c>Type</c> del hecho original.</summary>
    public string Type { get; init; } = string.Empty;

    public IReadOnlyList<DepositConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>
    /// Feature 014, US8 (T143; research E106): la cuenta del espejo cuando no es la del reverso —el destino de un traslado—, con su titular y sus
    /// dimensiones vigentes. Nulos = la del reverso.
    /// </summary>
    public MemberAccountRefV1? Account { get; init; }

    public Guid? HolderPersonPublicId { get; init; }

    public DepositFactDimensionsV1? Dimensions { get; init; }
}
