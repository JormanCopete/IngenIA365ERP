namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>CargaDeCaptacionesAplicada</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>CAC</c>; entrega K7): carga de migración de captaciones aplicada; informativo, con recibo. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record CargaDeCaptacionesAplicadaV1
{
    public const string Type = "CargaDeCaptacionesAplicada";


    public string Nature { get; init; } = string.Empty;

    public Guid LoadPublicId { get; init; }

    public DateOnly CutoffDate { get; init; }

    public IReadOnlyList<Guid> Parts { get; init; } = [];

    public IReadOnlyList<DepositLoadTotalV1> TotalsByLineAndConcept { get; init; } = [];
}

/// <summary>Un total de la carga por línea y concepto.</summary>
public sealed record DepositLoadTotalV1
{
    public string CreditLineCode { get; init; } = string.Empty;

    /// <summary>Valor de <c>MemberAccountConcept</c>.</summary>
    public string Concept { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public int AccountCount { get; init; }
}
