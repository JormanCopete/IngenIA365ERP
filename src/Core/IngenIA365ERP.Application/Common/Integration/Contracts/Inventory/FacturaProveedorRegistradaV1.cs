namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>FacturaProveedorRegistrada</c> v1 (contracts/mensajes.md §6.4): base, descuentos, IVA descontable y al
/// costo y retenciones de una factura o nota del proveedor, un documento soporte o su nota de ajuste, <b>sin
/// costo</b>. Una nota que reduce viaja con importes negativos. La diferencia de precio con la recepción viaja
/// en <see cref="AjusteDeCostoReconocidoV1"/>.
/// </summary>
public sealed record FacturaProveedorRegistradaV1
{
    public const string Type = "FacturaProveedorRegistrada";

    public string Operation { get; init; } = "FacturaProveedor";

    public SupplierDocumentV1 SupplierDocument { get; init; } = new();

    /// <summary>Recepciones contra las que se registra; vacío si no hay recepción (sólo servicios).</summary>
    public IReadOnlyList<DocumentRefV1> DerivedFrom { get; init; } = [];

    public IReadOnlyList<SupplierInvoiceLineV1> Lines { get; init; } = [];

    /// <summary><c>Deductible</c>, <c>AddedToCost</c> (informativa) y <c>WithholdingApplied</c>.</summary>
    public IReadOnlyList<TaxLineV1> Taxes { get; init; } = [];

    public SupplierInvoiceTotalsV1 Totals { get; init; } = new();
}

/// <summary>
/// <c>supplierDocument</c> (§6.4). <see cref="Kind"/> es <c>Invoice</c> | <c>CreditNote</c> | <c>DebitNote</c> |
/// <c>SupportDocument</c> | <c>SupportDocumentAdjustmentNote</c>; <see cref="PaymentForm"/>, <c>Cash</c> | <c>Credit</c>:
/// texto con valores cerrados.
/// </summary>
public sealed record SupplierDocumentV1
{
    public string Kind { get; init; } = string.Empty;

    public string? Prefix { get; init; }

    public string Number { get; init; } = string.Empty;

    /// <summary>CUFE del proveedor o CUDS propio.</summary>
    public string? UniqueCode { get; init; }

    public DateOnly IssueDate { get; init; }

    public DateOnly? DueDate { get; init; }

    public string PaymentForm { get; init; } = string.Empty;

    public bool IsElectronic { get; init; }
}

/// <summary>Una línea por grupo, bodega y tipo (<see cref="LineKind"/>: <c>Goods</c> | <c>Service</c>) (§6.4).</summary>
public sealed record SupplierInvoiceLineV1
{
    public string AccountingGroupCode { get; init; } = string.Empty;

    public string? WarehouseCode { get; init; }

    public string LineKind { get; init; } = string.Empty;

    public decimal GrossAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal NetAmount { get; init; }

    /// <summary>El IVA o INC que fue al costo de esas líneas (FR-044).</summary>
    public decimal TaxAddedToCost { get; init; }

    public IReadOnlyList<int> DocumentLines { get; init; } = [];
}

/// <summary><c>totals</c> de la factura del proveedor (§6.4); <see cref="TaxTotal"/> suma descontables y al costo.</summary>
public sealed record SupplierInvoiceTotalsV1
{
    public decimal Subtotal { get; init; }

    public decimal DiscountTotal { get; init; }

    public decimal TaxTotal { get; init; }

    public decimal WithholdingTotal { get; init; }

    public decimal Total { get; init; }

    public decimal AmountPayable { get; init; }
}
