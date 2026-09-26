using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Models.Compras;

// Los DTO espejo de compras (feature 012, US9, T352; contracts/api.md §14). Los enums llegan como número (EnumPorNombreONumero)
// y se mandan por nombre o número; sólo PublicId (Principio VI). (nuevos)

/// <summary>Las clases de compras de I1 (<c>DocumentClass</c>).</summary>
public static class ClasesDeCompra
{
    public const int Recepcion = 3;
    public const int Factura = 4;
    public const int Nota = 5;
    public const int Devolucion = 9;
    public const int Anulacion = 34;
}

/// <summary>Los eventos RADIAN (<c>SupplierInvoiceEventCode</c>) y sus estados (<c>SupplierInvoiceEventStatus</c>).</summary>
public static class EventosRadian
{
    public const int Acuse030 = 30;
    public const int ReciboDelBien032 = 32;

    public static string Evento(int codigo) => codigo == Acuse030 ? "Acuse de recibo (030)" : codigo == ReciboDelBien032 ? "Recibo del bien (032)" : codigo.ToString();

    public static string Estado(int? estado) => estado switch
    {
        0 => "Pendiente",
        1 => "Registrado por fuera",
        2 => "Emitido",
        3 => "Rechazado",
        4 => "No aplica",
        _ => "",
    };
}

/// <summary>El documento del proveedor en una factura o nota (<c>supplier</c>, §14.4–§14.5).</summary>
public sealed record DocumentoDelProveedorRequest(string? Prefix, string Number, string? Cufe, DateOnly IssueDate, DateOnly? DueDate, string? PaymentForm, bool IsElectronic);

/// <summary>Una línea del borrador de compras (<c>SaveInventoryDraftLine</c> con los campos de compras).</summary>
public sealed record LineaDeCompraRequest(
    Guid? LinePublicId,
    Guid ProductPublicId,
    Guid UnitPublicId,
    decimal Quantity,
    decimal? UnitPrice = null,
    decimal? DiscountPercent = null,
    decimal? DiscountAmount = null,
    Guid? LocationPublicId = null,
    string? Notes = null,
    Guid? ReceiptLinePublicId = null,
    Guid? InvoiceLinePublicId = null,
    decimal? Amount = null,
    bool? AffectsCost = null);

/// <summary>El borrador de un documento de compras (§14.2, §14.4–§14.6): el del ciclo común más lo propio de compras.</summary>
public sealed record BorradorDeCompraRequest(
    Guid DocumentTypePublicId,
    DateOnly? OperationDate,
    Guid? WarehousePublicId,
    Guid? SupplierPersonPublicId,
    string? ExternalReference,
    string? Reason,
    string? Notes,
    byte[]? RowVersion,
    IReadOnlyList<LineaDeCompraRequest> Lines,
    string? OperationMunicipalityDaneCode = null,
    DocumentoDelProveedorRequest? Supplier = null,
    Guid? SupplierInvoicePublicId = null,
    string? NoteKind = null,
    Guid? CostCenterPublicId = null);

/// <summary>La factura de la compra directa: su tipo y el documento del proveedor (§14.3).</summary>
public sealed record FacturaDeCompraDirectaRequest(Guid DocumentTypePublicId, DocumentoDelProveedorRequest Supplier);

/// <summary>El cuerpo de <c>POST /purchases/direct</c> (§14.3).</summary>
public sealed record CompraDirectaRequest(BorradorDeCompraRequest Receipt, FacturaDeCompraDirectaRequest Invoice);

/// <summary>Un evento RADIAN de la factura (§14.8).</summary>
public sealed record EventoRadianDto(int EventCode, int Status, DateOnly? Date, string? Source, string? Cude, UsuarioDeInventarioDto? RegisteredBy,
    DateTime? RegisteredAt, string? Notes, Guid? ElectronicDocumentPublicId);

/// <summary>Registrar (o, con <see cref="Correct"/>, corregir) un evento emitido por fuera (§14.8).</summary>
public sealed record RegistrarEventoRadianRequest(int EventCode, DateOnly Date, string Source, string? Cude, string? Notes, bool Correct = false);

/// <summary>El documento del proveedor de una factura o nota (<c>supplierInvoice</c> del detalle).</summary>
public sealed record InfoDelProveedorDto(string Prefix, string Number, string? Cufe, DateOnly IssueDate, DateOnly? DueDate, string PaymentForm,
    bool IsElectronic, bool IsDebitNote, bool IsReleased);

/// <summary>Por línea de una recepción: recibido, facturado, devuelto, por facturar y devolvible.</summary>
public sealed record SaldoDeLineaDeRecepcionDto(Guid LinePublicId, int LineNumber, decimal Received, decimal Invoiced, decimal Returned,
    decimal PendingToInvoice, decimal Returnable);

/// <summary>El detalle de un documento de compras (§14.2, §14.4).</summary>
public sealed record DocumentoDeCompraDto(
    DocumentoDeInventarioDto Document,
    InfoDelProveedorDto? SupplierInvoice,
    IReadOnlyList<EventoRadianDto> RadianEvents,
    IReadOnlyList<SaldoDeLineaDeRecepcionDto> LineBalances,
    string? OperationMunicipalityDaneCode,
    IReadOnlyList<AjusteDeCostoDeAnulacionDto>? CostAdjustments = null);

/// <summary>Una fila de la lista de recepciones: con lo que falta facturar y lo devuelto.</summary>
public sealed record ResumenDeRecepcionDto(ResumenDeDocumentoDto Document, decimal PendingToInvoice, decimal Returned);

/// <summary>Una fila de la lista de facturas o notas del proveedor, con su documento y el estado de los eventos.</summary>
public sealed record ResumenDeFacturaDeProveedorDto(ResumenDeDocumentoDto Document, InfoDelProveedorDto? Supplier, int? Receipt030, int? GoodsReceived032);

/// <summary>Un documento que creó la compra directa.</summary>
public sealed record DocumentoCreadoDto(Guid PublicId, string? DisplayNumber, int Status);

/// <summary>La factura que creó la compra directa, con el número del proveedor.</summary>
public sealed record FacturaCreadaDto(Guid PublicId, string? DisplayNumber, int Status, string SupplierNumber);

/// <summary><c>DirectPurchaseResultDto</c> (§14.3): <see cref="Status"/> 2 confirmada o 1 en aprobación.</summary>
public sealed record ResultadoDeCompraDirectaDto(
    int Status,
    DocumentoCreadoDto Receipt,
    FacturaCreadaDto SupplierInvoice,
    AprobacionPedidaDeInventarioDto? Approval,
    TotalesDeDocumentoDto Totals,
    IReadOnlyList<RenglonDeImpuestoDto> TaxLines,
    IReadOnlyList<EventoRadianDto> RadianEvents,
    IReadOnlyList<AvisoDeInventarioDto> Warnings);

/// <summary>Lo que el XML de la factura deja prellenar (§14.4, <c>POST /prefill</c>).</summary>
public sealed record PrellenadoDeFacturaDto(
    ProveedorDelXmlDto Supplier,
    string? Prefix,
    string Number,
    string? Cufe,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string PaymentForm,
    IReadOnlyList<LineaDelXmlDto> Lines,
    TotalesDelXmlDto Totals);

public sealed record ProveedorDelXmlDto(string DocumentNumber, string Name, Guid? PersonPublicId);

public sealed record LineaDelXmlDto(string? SupplierItemCode, string Description, decimal Quantity, string? UnitCode, decimal UnitPrice, decimal Discount,
    IReadOnlyList<ImpuestoDelXmlDto> Taxes, Guid? SuggestedProductPublicId);

public sealed record ImpuestoDelXmlDto(string Code, decimal? Rate, decimal Amount);

public sealed record TotalesDelXmlDto(decimal LineExtension, decimal TaxExclusive, decimal TaxInclusive, decimal Payable);

/// <summary>Los filtros de las listas de compras (§14.2, §14.4).</summary>
public sealed class FiltroDeCompras
{
    public Guid? SupplierPersonPublicId { get; set; }
    public int? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? WarehousePublicId { get; set; }
    public string? PaymentForm { get; set; }
    public bool? RadianPending { get; set; }
    public string? Number { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
