namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>TrasladoDespachado</c> v1 (contracts/mensajes.md §6.6): de la bodega de origen (<c>Operational</c>) a la de
/// tránsito de su sucursal (<c>Transit</c>).
/// </summary>
public sealed record TrasladoDespachadoV1
{
    public const string Type = "TrasladoDespachado";

    public string Operation { get; init; } = "DespachoTraslado";

    public string TransitWarehouseCode { get; init; } = string.Empty;

    /// <summary>Bodega de destino (informativa).</summary>
    public string DestinationWarehouseCode { get; init; } = string.Empty;

    public IReadOnlyList<TransferCostLineV1> Lines { get; init; } = [];
}
