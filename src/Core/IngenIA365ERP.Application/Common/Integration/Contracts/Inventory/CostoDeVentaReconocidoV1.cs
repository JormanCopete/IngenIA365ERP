namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>CostoDeVentaReconocido</c> v1 (contracts/mensajes.md §6.2): sólo cantidades y costo de lo que salió
/// (<c>movement = Exit</c>). Lo emiten <c>SalesInvoice</c> (directa o desde pedido), <c>PosEquivalentDocument</c>,
/// <c>NonElectronicSalesReceipt</c> y <c>Shipment</c>. Con <see cref="VentaFacturadaV1"/> del mismo evento forma
/// una unidad y un comprobante (T11).
/// </summary>
public sealed record CostoDeVentaReconocidoV1
{
    public const string Type = "CostoDeVentaReconocido";

    public string Operation { get; init; } = "CostoDeVenta";

    public string? PointOfSaleCode { get; init; }

    public Guid? CashSessionPublicId { get; init; }

    public IReadOnlyList<CostLineV1> Lines { get; init; } = [];
}
