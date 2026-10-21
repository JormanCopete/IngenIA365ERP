namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CreditoAprobadoSinDesembolsar</c> v1 (§10.24; <c>CAD</c>; A3, C15): la cuenta contingente 9115 del CUIF. Nace en K1 con modo <c>NoPasa</c>
/// y comprobante <c>CN</c>; admite proceso. Si el dueño rechaza A3, sale del catálogo sin tocar nada más.
/// </summary>
public sealed record CreditoAprobadoSinDesembolsarV1
{
    public const string Type = "CreditoAprobadoSinDesembolsar";

    /// <summary>El solicitante, el titular del cupo o el deudor del crédito que la garantía respalda.</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>El crédito, cuando ya existe.</summary>
    public LoanRefV1? Loan { get; init; }

    /// <summary>La solicitud, el cupo o la garantía.</summary>
    public ContingentSubjectV1 Subject { get; init; } = new();

    public DateOnly ValueDate { get; init; }

    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Las del crédito cuando existe; si no, la matriz resuelve por <c>creditLineCode</c>.</summary>
    public LoanFactDimensionsV1? Dimensions { get; init; }

    public string CreditLineCode { get; init; } = string.Empty;

    /// <summary>El movimiento de §10.24.</summary>
    public string Movement { get; init; } = string.Empty;

    /// <summary>Una línea con la variación con signo (C22).</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El saldo contingente tras el hecho.</summary>
    public decimal BalanceAfter { get; init; }

    /// <summary>El hecho principal de la operación que lo movió.</summary>
    public OriginalFactRefV1? CauseFact { get; init; }
}

/// <summary>
/// La solicitud, el cupo o la garantía de un hecho contingente (§10.24, <c>subject</c>): <c>CreditApplication</c>,
/// <c>CreditLimit</c> o <c>Collateral</c>.
/// </summary>
public sealed record ContingentSubjectV1
{
    public string Kind { get; init; } = string.Empty;

    public Guid PublicId { get; init; }

    public string Number { get; init; } = string.Empty;
}
