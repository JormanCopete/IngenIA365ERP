namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>TrasladoRecibido</c> v1 (contracts/mensajes.md §6.7): de tránsito (<c>Transit</c>) a destino
/// (<c>Operational</c>), sólo lo recibido. Es un derivado de su despacho: no sella modo propio.
/// </summary>
public sealed record TrasladoRecibidoV1
{
    public const string Type = "TrasladoRecibido";

    public string Operation { get; init; } = "RecepcionTraslado";

    /// <summary>El despacho (uno).</summary>
    public IReadOnlyList<DocumentRefV1> DerivedFrom { get; init; } = [];

    public IReadOnlyList<TransferCostLineV1> Lines { get; init; } = [];
}
