using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

// Los DTO de compras (feature 012, US9; contracts/api.md §14). Sólo PublicId hacia afuera (Principio VI). (nuevos)

/// <summary>Un evento RADIAN de la factura (§14.8). <see cref="Status"/> y <see cref="EventCode"/> salen como número.</summary>
public sealed record RadianEventDto(
    SupplierInvoiceEventCode EventCode,
    SupplierInvoiceEventStatus Status,
    DateOnly? Date,
    string? Source,
    string? Cude,
    UsuarioDto? RegisteredBy,
    DateTime? RegisteredAt,
    string? Notes,
    Guid? ElectronicDocumentPublicId);

/// <summary><c>supplierInvoice</c> del detalle de una factura o nota del proveedor (§14.4).</summary>
public sealed record SupplierInvoiceInfoDto(
    string Prefix,
    string Number,
    string? Cufe,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string PaymentForm,
    bool IsElectronic,
    bool IsDebitNote,
    bool IsReleased);

/// <summary>Por línea de una recepción: lo que falta por facturar y lo devuelto (se calculan de los vínculos, §14.2).</summary>
public sealed record ReceiptLineBalanceDto(Guid LinePublicId, int LineNumber, decimal Received, decimal Invoiced, decimal Returned, decimal PendingToInvoice, decimal Returnable);

/// <summary>
/// El detalle de un documento de compras: el genérico (<see cref="InventoryDocumentDto"/>) más lo propio de la clase —el
/// documento del proveedor y sus eventos RADIAN, o los saldos por línea de una recepción—.
/// </summary>
public sealed record PurchaseDocumentDto(
    InventoryDocumentDto Document,
    SupplierInvoiceInfoDto? SupplierInvoice,
    IReadOnlyList<RadianEventDto> RadianEvents,
    IReadOnlyList<ReceiptLineBalanceDto> LineBalances,
    string? OperationMunicipalityDaneCode,
    IReadOnlyList<AjusteDeCostoDeAnulacionDto>? CostAdjustments = null);

/// <summary>Una fila de la lista de facturas del proveedor (§14.4): el resumen genérico más el documento y los eventos.</summary>
public sealed record SupplierInvoiceSummaryDto(
    DocumentSummaryDto Document,
    SupplierInvoiceInfoDto? Supplier,
    SupplierInvoiceEventStatus? Receipt030,
    SupplierInvoiceEventStatus? GoodsReceived032);

/// <summary>Una fila de la lista de recepciones (§14.2): el resumen genérico más lo que falta facturar y lo devuelto.</summary>
public sealed record PurchaseReceiptSummaryDto(DocumentSummaryDto Document, decimal PendingToInvoice, decimal Returned);

/// <summary>Un documento que creó la compra directa. (nuevo)</summary>
public sealed record DocumentoCreadoDto(Guid PublicId, string? DisplayNumber, DocumentStatus Status);

/// <summary>La factura que creó la compra directa, con el número del proveedor. (nuevo)</summary>
public sealed record FacturaCreadaDto(Guid PublicId, string? DisplayNumber, DocumentStatus Status, string SupplierNumber);

/// <summary><c>DirectPurchaseResultDto</c> (§14.3): <see cref="Status"/> es <c>Confirmed</c> o <c>PendingApproval</c>.</summary>
public sealed record DirectPurchaseResultDto(
    DocumentStatus Status,
    DocumentoCreadoDto Receipt,
    FacturaCreadaDto SupplierInvoice,
    AprobacionPedidaDto? Approval,
    DocumentTotalsDto Totals,
    IReadOnlyList<DocumentTaxLineDto> TaxLines,
    IReadOnlyList<RadianEventDto> RadianEvents,
    IReadOnlyList<MensajeEmitidoDto>? Messages,
    IReadOnlyList<AvisoDto> Warnings);

/// <summary>La factura de la compra directa: su tipo y el documento del proveedor (§14.3).</summary>
public sealed record DirectPurchaseInvoiceRequest(Guid DocumentTypePublicId, SupplierDocumentRequest Supplier);

/// <summary>El cuerpo de registrar un evento RADIAN hecho por fuera (§14.8); <see cref="Correct"/> corrige uno ya registrado. (nuevo)</summary>
public sealed record RegistrarEventoRadianRequest(SupplierInvoiceEventCode EventCode, DateOnly Date, string Source, string? Cude = null, string? Notes = null, bool Correct = false);

/// <summary>Lo que el XML de la factura del proveedor deja prellenar (§14.4, <c>POST /prefill</c>). (nuevo)</summary>
public sealed record SupplierInvoicePrefillDto(
    PrefillSupplierDto Supplier,
    string? Prefix,
    string Number,
    string? Cufe,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string PaymentForm,
    IReadOnlyList<PrefillLineDto> Lines,
    PrefillTotalsDto Totals);

/// <summary>El proveedor del XML y, si ya existe, su persona.</summary>
public sealed record PrefillSupplierDto(string DocumentNumber, string Name, Guid? PersonPublicId);

/// <summary>Una línea del XML con el producto sugerido (por código de barras o código del proveedor).</summary>
public sealed record PrefillLineDto(
    string? SupplierItemCode,
    string Description,
    decimal Quantity,
    string? UnitCode,
    decimal UnitPrice,
    decimal Discount,
    IReadOnlyList<PrefillTaxDto> Taxes,
    Guid? SuggestedProductPublicId);

/// <summary>Un impuesto de una línea del XML.</summary>
public sealed record PrefillTaxDto(string Code, decimal? Rate, decimal Amount);

/// <summary>Los totales del XML.</summary>
public sealed record PrefillTotalsDto(decimal LineExtension, decimal TaxExclusive, decimal TaxInclusive, decimal Payable);
