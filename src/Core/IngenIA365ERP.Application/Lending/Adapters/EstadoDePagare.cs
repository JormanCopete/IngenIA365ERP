using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Adapters;

/// <summary>
/// El estado de un pagaré en el depósito (feature 013, US3, T0436; contracts/adaptadores-y-archivos.md §3.2, §3.8): su id externo, el estado
/// (<see cref="PromissoryNoteStatus"/>: <c>Created</c>, <c>PendingSignature</c>, <c>Signed</c>, <c>InCustody</c>, <c>Endorsed</c>, <c>Voided</c>),
/// la firma de cada firmante y desde cuándo lo custodia. Es lo que responden crear, consultar, endosar y anular. (nuevo)
/// </summary>
public sealed record EstadoDePagare(
    string IdExterno,
    PromissoryNoteStatus Estado,
    IReadOnlyList<FirmaDePagare> Firmas,
    DateTimeOffset? CustodiaDesde)
{
    /// <summary>Algún firmante rechazó la firma: el pagaré no se perfecciona (§3.7, dígito 1 del simulado).</summary>
    public bool FirmaRechazada => Firmas.Any(f => f.Estado == EstadoDeFirmaDePagare.Rechazada);

    /// <summary>Todos los firmantes firmaron.</summary>
    public bool Firmado => Firmas.Count > 0 && Firmas.All(f => f.Estado == EstadoDeFirmaDePagare.Firmada);
}
