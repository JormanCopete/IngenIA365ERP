namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>CompraRecibida</c> v1 (contracts/mensajes.md §6.3): lo que entró por una recepción de compra, al costo de
/// entrada (<c>movement = Entry</c>). La recepción no conoce la factura: su contrapartida es la cuenta puente de
/// mercancía por facturar.
/// </summary>
public sealed record CompraRecibidaV1
{
    public const string Type = "CompraRecibida";

    public string Operation { get; init; } = "Compra";

    /// <summary>Remisión o guía del proveedor.</summary>
    public string? SupplierDeliveryReference { get; init; }

    public IReadOnlyList<CostLineV1> Lines { get; init; } = [];
}
