namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>NotaCreditoEmitida</c> v1 (contracts/mensajes.md §6.11): lo que se acredita, sus impuestos con la foto del
/// original y los reintegros por medio (<c>Refunded</c>). Con devolución, el mismo evento emite
/// <see cref="DevolucionRegistradaV1"/>.
/// </summary>
public sealed record NotaCreditoEmitidaV1
{
    public const string Type = "NotaCreditoEmitida";

    public string Operation { get; init; } = "NotaCredito";

    /// <summary>Marca de anulación total (FR-066).</summary>
    public bool IsTotalVoid { get; init; }

    public bool WithReturn { get; init; }

    public string? PointOfSaleCode { get; init; }

    public Guid? CashSessionPublicId { get; init; }

    public IReadOnlyList<SalesAmountLineV1> Lines { get; init; } = [];

    public IReadOnlyList<TaxLineV1> Taxes { get; init; } = [];

    public IReadOnlyList<PaymentLineV1> Payments { get; init; } = [];

    public SalesTotalsV1 Totals { get; init; } = new();
}
