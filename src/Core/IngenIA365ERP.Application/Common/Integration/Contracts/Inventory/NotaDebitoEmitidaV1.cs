namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>NotaDebitoEmitida</c> v1 (contracts/mensajes.md §6.12, I6): mismo contenido que <see cref="VentaFacturadaV1"/>
/// sin <c>derivedFrom</c>: lo que se carga, sus impuestos y cómo se cobra (<c>Received</c>).
/// </summary>
public sealed record NotaDebitoEmitidaV1
{
    public const string Type = "NotaDebitoEmitida";

    public string Operation { get; init; } = "NotaDebito";

    public string? SalesChannelCode { get; init; }

    public string? PointOfSaleCode { get; init; }

    public string? CashRegisterCode { get; init; }

    public Guid? CashSessionPublicId { get; init; }

    public IReadOnlyList<SalesAmountLineV1> Lines { get; init; } = [];

    public IReadOnlyList<TaxLineV1> Taxes { get; init; } = [];

    public IReadOnlyList<PaymentLineV1> Payments { get; init; } = [];

    public SalesTotalsV1 Totals { get; init; } = new();
}
