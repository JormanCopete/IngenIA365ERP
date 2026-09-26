namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>VentaFacturada</c> v1 (contracts/mensajes.md §6.1): base, descuentos, impuestos, retenciones y pagos por
/// medio de una venta, <b>sin costo</b> (el costo viaja en <see cref="CostoDeVentaReconocidoV1"/> del mismo
/// evento). Lo emiten <c>SalesInvoice</c>, <c>SalesInvoiceFromShipments</c>, <c>PosEquivalentDocument</c> y
/// <c>NonElectronicSalesReceipt</c>. A Contabilidad, de negocio; <c>originEventKey</c> <c>Confirmation</c>.
/// </summary>
public sealed record VentaFacturadaV1
{
    public const string Type = "VentaFacturada";

    public string Operation { get; init; } = "Venta";

    public string? SalesChannelCode { get; init; }

    public string? PointOfSaleCode { get; init; }

    public string? CashRegisterCode { get; init; }

    /// <summary>Sesión de caja: la <c>BatchScopeKey</c> del disparador <c>CierreDeTurno</c>.</summary>
    public Guid? CashSessionPublicId { get; init; }

    /// <summary>Remisiones de una factura desde remisiones; vacío en las demás.</summary>
    public IReadOnlyList<DocumentRefV1> DerivedFrom { get; init; } = [];

    public IReadOnlyList<SalesAmountLineV1> Lines { get; init; } = [];

    /// <summary>Impuestos <c>Generated</c> y retenciones que practica el comprador (<c>WithholdingSuffered</c>).</summary>
    public IReadOnlyList<TaxLineV1> Taxes { get; init; } = [];

    /// <summary>Pagos por medio, todos <c>Received</c>.</summary>
    public IReadOnlyList<PaymentLineV1> Payments { get; init; } = [];

    public SalesTotalsV1 Totals { get; init; } = new();
}
