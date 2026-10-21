namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>BonificacionReconocida</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>BON</c>; entrega K9): vencimiento cumplido del contractual (FR-075); <c>CC</c>. Admite proceso.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record BonificacionReconocidaV1
{
    public const string Type = "BonificacionReconocida";

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

    public ContractualBonusV1 Contractual { get; init; } = new();
}

/// <summary>El cumplimiento de un ahorro contractual (mensajes.md §4.1, <c>contractual</c>).</summary>
public sealed record ContractualBonusV1
{
    public int Installments { get; init; }

    public int Paid { get; init; }

    public decimal BonusRateEa { get; init; }

    public decimal BaseRateEa { get; init; }
}
