namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>DevolucionGirada</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>DVG</c>; entrega K7): consumo de <c>ResultadoDelGiro</c> <c>Disbursed</c> de una orden de captaciones; informativo, con recibo. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record DevolucionGiradaV1
{
    public const string Type = "DevolucionGirada";


    public Guid OrderPublicId { get; init; }

    public string OrderKind { get; init; } = string.Empty;

    public string Outcome { get; init; } = string.Empty;

    public string TreasuryReference { get; init; } = string.Empty;

    public DateOnly OccurredOn { get; init; }
}
