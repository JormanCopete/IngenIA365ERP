using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Documents;

// Los DTO del ciclo común (feature 012, T143; contracts/api.md §9.2–§9.5). Sólo PublicId hacia afuera (Principio VI):
// ningún Id interno cruza. Los enums salen como número (EnumPorNombreONumero).

// ------------------------------------------------------------------------------------------------ entrada --

/// <summary>El cuerpo de <c>POST /</c> y <c>PUT /{id}</c> del ciclo común (§9.3).</summary>
public sealed record SaveInventoryDraftRequest(
    Guid DocumentTypePublicId,
    DateOnly? OperationDate,
    Guid? WarehousePublicId,
    Guid? DestinationWarehousePublicId,
    Guid? CostCenterPublicId,
    Guid? CounterpartyPersonPublicId,
    string? ExternalReference,
    string? Reason,
    Guid? AdjustmentCausePublicId,
    string? Notes,
    string? Currency,
    decimal? ExchangeRate,
    byte[]? RowVersion,
    IReadOnlyList<SaveInventoryDraftLine> Lines,
    string? OperationMunicipalityDaneCode = null,
    Guid? SupplierPersonPublicId = null,
    SupplierDocumentRequest? Supplier = null,
    Guid? SupplierInvoicePublicId = null,
    string? NoteKind = null)
{
    /// <summary>La contraparte: la del ciclo común o, en compras, el proveedor (<c>supplierPersonPublicId</c>, api.md §14).</summary>
    public Guid? Contraparte => CounterpartyPersonPublicId ?? SupplierPersonPublicId;

    /// <summary>¿Trae algún campo que sólo admite el grupo de compras (§14.1)?</summary>
    public bool TraeCamposDeCompra =>
        OperationMunicipalityDaneCode is not null || SupplierPersonPublicId is not null || Supplier is not null
        || SupplierInvoicePublicId is not null || NoteKind is not null
        || Lines.Any(l => l.ReceiptLinePublicId is not null || l.InvoiceLinePublicId is not null || l.Amount is not null || l.AffectsCost is not null);
}

/// <summary>
/// El documento del proveedor en una factura o nota (<c>supplier</c>, api.md §14.4–§14.5): prefijo, número, CUFE o CUDE,
/// emisión, vencimiento, forma de pago (<c>Cash</c> o <c>Credit</c>) y si es electrónico. (nuevo)
/// </summary>
public sealed record SupplierDocumentRequest(
    string? Prefix,
    string Number,
    string? Cufe,
    DateOnly IssueDate,
    DateOnly? DueDate = null,
    string? PaymentForm = null,
    bool IsElectronic = false);

/// <summary>Una línea del borrador. <see cref="LinePublicId"/> en un <c>PUT</c> conserva la línea; sin él, es nueva.</summary>
public sealed record SaveInventoryDraftLine(
    Guid? LinePublicId,
    Guid ProductPublicId,
    Guid UnitPublicId,
    decimal Quantity,
    decimal? UnitPrice = null,
    decimal? DiscountPercent = null,
    decimal? DiscountAmount = null,
    decimal? UnitCost = null,
    Guid? LocationPublicId = null,
    Guid? ToLocationPublicId = null,
    Guid? SourceLinePublicId = null,
    string? LotCode = null,
    string? SerialNumber = null,
    DateOnly? ExpiryDate = null,
    string? Notes = null,
    Guid? ReceiptLinePublicId = null,
    Guid? InvoiceLinePublicId = null,
    decimal? Amount = null,
    bool? AffectsCost = null)
{
    /// <summary>La línea de origen: la de la recepción (factura, devolución), la de la factura (nota) o la genérica.</summary>
    public Guid? Origen => SourceLinePublicId ?? ReceiptLinePublicId ?? InvoiceLinePublicId;
}

// -------------------------------------------------------------------------------------------- referencias --

/// <summary>Una entidad nombrada por su código (tipo, bodega, sucursal, producto…). (nuevo)</summary>
public sealed record ReferenciaDto(Guid PublicId, string Code, string Name);

/// <summary>Una unidad de medida en una línea. (nuevo)</summary>
public sealed record UnidadDto(Guid PublicId, string Code);

/// <summary>Un usuario: nunca su Id interno (§2.5). (nuevo)</summary>
public sealed record UsuarioDto(Guid? UserPublicId, string Name);

/// <summary>La contraparte del documento. (nuevo)</summary>
public sealed record ContraparteDto(Guid PersonPublicId, string Name, string DocumentNumber);

/// <summary>Otro documento nombrado por su número visible (<c>voids</c>, <c>voidedBy</c>). (nuevo)</summary>
public sealed record DocumentoReferidoDto(Guid PublicId, string? DisplayNumber);

/// <summary>La solicitud de aprobación pendiente de un documento, en la lista. (nuevo)</summary>
public sealed record AprobacionPendienteDto(Guid RequestPublicId, int CurrentLevel);

/// <summary>Un aviso que no detiene nada (§2.8). (nuevo)</summary>
public sealed record AvisoDto(string Code, string Message, object? Data);

// ------------------------------------------------------------------------------------------------- lista --

/// <summary><c>DocumentSummaryDto</c> (§9.2). <see cref="CostValue"/> sólo con <c>Inventory.Costs.Read</c>.</summary>
public sealed record DocumentSummaryDto(
    Guid PublicId,
    DocumentClass Class,
    DocumentClassGroup Group,
    ReferenciaDto DocumentType,
    string Prefix,
    long? Number,
    string? DisplayNumber,
    DocumentStatus Status,
    DateOnly OperationDate,
    ReferenciaDto? Warehouse,
    ReferenciaDto? DestinationWarehouse,
    ContraparteDto? Counterparty,
    decimal? Total,
    decimal? CostValue,
    UsuarioDto CreatedBy,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    UsuarioDto? ConfirmedBy,
    PostingMode? PostingMode,
    DocumentoReferidoDto? Voids,
    DocumentoReferidoDto? VoidedBy,
    AprobacionPendienteDto? PendingApproval);

// ----------------------------------------------------------------------------------------------- detalle --

/// <summary>Un vínculo de una línea con la línea de otro documento. (nuevo)</summary>
public sealed record VinculoDeLineaDto(DocumentLinkKind Kind, Guid DocumentPublicId, string? DisplayNumber, Guid LinePublicId, decimal QuantityBase);

/// <summary>Una línea del documento (§9.2). <see cref="UnitCost"/>/<see cref="TotalCost"/> sólo con <c>Inventory.Costs.Read</c>. (nuevo)</summary>
public sealed record DocumentLineDto(
    Guid LinePublicId,
    int LineNumber,
    ReferenciaDto Product,
    UnidadDto Unit,
    decimal Quantity,
    decimal Factor,
    decimal QuantityBase,
    decimal RoundingQuantity,
    decimal? UnitPrice,
    decimal? DiscountAmount,
    decimal? LineSubtotal,
    decimal? UnitCost,
    decimal? TotalCost,
    ReferenciaDto? Location,
    ReferenciaDto? ToLocation,
    string? Lot,
    string? Serial,
    DateOnly? ExpiryDate,
    IReadOnlyList<VinculoDeLineaDto> Links,
    string? Notes);

/// <summary>Un vínculo del documento con otro (§9.2). (nuevo)</summary>
public sealed record DocumentLinkDto(DocumentLinkKind Kind, Guid DocumentPublicId, DocumentClass Class, string? DisplayNumber, DocumentStatus Status);

/// <summary>Un renglón de la foto tributaria (<c>INV_DocumentTaxLines</c>). (nuevo)</summary>
public sealed record DocumentTaxLineDto(
    int? LineNumber,
    TaxKind Kind,
    string RateCode,
    decimal? Rate,
    decimal? AmountPerUnit,
    decimal Base,
    decimal Amount,
    TaxTreatment Treatment,
    string? MunicipalityDaneCode,
    string ExplanationJson);

/// <summary>Los totales del documento (T26). <see cref="CostTotal"/> sólo con <c>Inventory.Costs.Read</c>. (nuevo)</summary>
public sealed record DocumentTotalsDto(
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal WithholdingTotal,
    decimal Total,
    decimal AmountDue,
    decimal? CostTotal);

/// <summary>La copia fiscal vigente de la contraparte, sólo en confirmados (<c>INV_DocumentPartySnapshots</c>). (nuevo)</summary>
public sealed record DocumentPartyDto(
    int Version,
    string LegalName,
    string DianIdTypeCode,
    string TaxId,
    string? CheckDigit,
    string? Address,
    string? MunicipalityDaneCode,
    string? DianResponsibilities,
    bool IsVatResponsible,
    bool IsLargeContributor,
    bool IsSelfWithholder);

/// <summary>Un nivel de la aprobación del documento. (nuevo)</summary>
public sealed record NivelDeAprobacionDelDocumentoDto(int Order, decimal Threshold, string PermissionCode, string Status);

/// <summary>Una decisión sobre la aprobación del documento. (nuevo)</summary>
public sealed record DecisionDeAprobacionDelDocumentoDto(int Level, string Decision, string DecidedBy, DateTime DecidedAt, string? Reason);

/// <summary><c>approval</c> del detalle (§15.2). (nuevo)</summary>
public sealed record DocumentApprovalDto(
    Guid RequestPublicId,
    string Status,
    int CurrentLevel,
    IReadOnlyList<NivelDeAprobacionDelDocumentoDto> Levels,
    IReadOnlyList<DecisionDeAprobacionDelDocumentoDto> Decisions);

/// <summary>Un mensaje de integración del documento, sólo con <c>Inventory.Messages.View</c>. (nuevo)</summary>
public sealed record DocumentMessageDto(Guid MessagePublicId, string Type, string Destination, DeliveryStatus DeliveryStatus, DeliveryMode Mode);

/// <summary>Un adjunto del documento. (nuevo)</summary>
public sealed record DocumentAttachmentDto(Guid AttachmentPublicId, string FileName, string ContentType, long SizeBytes, string? UploadedBy, DateTime UploadedAt, bool CanDelete);

/// <summary>
/// <c>InventoryDocumentDto</c> (§9.2): la cabecera, las líneas y los satélites. <see cref="AllowedActions"/> lo calcula
/// el servidor con el estado, los permisos y el alcance; la pantalla no lo deduce.
/// </summary>
public sealed record InventoryDocumentDto(
    Guid PublicId,
    DocumentClass Class,
    DocumentClassGroup Group,
    ReferenciaDto DocumentType,
    string Prefix,
    long? Number,
    string? DisplayNumber,
    DocumentStatus Status,
    DateOnly OperationDate,
    UsuarioDto CreatedBy,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    UsuarioDto? ConfirmedBy,
    ReferenciaDto? Warehouse,
    ReferenciaDto? DestinationWarehouse,
    ReferenciaDto? TransitWarehouse,
    ReferenciaDto Branch,
    ReferenciaDto? CostCenter,
    ContraparteDto? Counterparty,
    ReferenciaDto? Salesperson,
    string? ExternalReference,
    string? Reason,
    ReferenciaDto? AdjustmentCause,
    string? Notes,
    string Currency,
    decimal ExchangeRate,
    PostingMode? PostingMode,
    DocumentoReferidoDto? Voids,
    DocumentoReferidoDto? VoidedBy,
    byte[] RowVersion,
    IReadOnlyList<DocumentLineDto> Lines,
    IReadOnlyList<DocumentLinkDto> Links,
    IReadOnlyList<DocumentTaxLineDto> TaxLines,
    DocumentTotalsDto Totals,
    DocumentPartyDto? Party,
    DocumentApprovalDto? Approval,
    IReadOnlyList<DocumentMessageDto>? Messages,
    IReadOnlyList<DocumentAttachmentDto> Attachments,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<AvisoDto> Warnings);

// --------------------------------------------------------------------------------------------- resultados --

/// <summary>Un nivel de la aprobación que pidió la confirmación. (nuevo)</summary>
public sealed record NivelPedidoDto(int Order, decimal Threshold, string PermissionCode, string Status);

/// <summary><c>approval</c> de la confirmación: <c>reason</c> es <c>Policy</c> o <c>AmountLimit</c> (§9.3). (nuevo)</summary>
public sealed record AprobacionPedidaDto(Guid RequestPublicId, string Reason, IReadOnlyList<NivelPedidoDto> Levels);

/// <summary><c>prevalidation</c> de la confirmación; <c>NotApplicable</c> antes de I2. (nuevo)</summary>
public sealed record ValidacionPreviaDto(PrevalidationOutcome Outcome, IReadOnlyList<AvisoDto> Warnings);

/// <summary>Un mensaje emitido al confirmar, sólo con <c>Inventory.Messages.View</c>. (nuevo)</summary>
public sealed record MensajeEmitidoDto(Guid MessagePublicId, string Type, string Destination, DeliveryStatus DeliveryStatus);

/// <summary><c>ConfirmationResultDto</c> (§9.3): <see cref="Status"/> es <c>Confirmed</c> o <c>PendingApproval</c>.</summary>
public sealed record ConfirmationResultDto(
    Guid PublicId,
    DocumentStatus Status,
    long? Number,
    string? DisplayNumber,
    DateOnly OperationDate,
    DateTime? ConfirmedAt,
    PostingMode? PostingMode,
    AprobacionPedidaDto? Approval,
    ValidacionPreviaDto? Prevalidation,
    IReadOnlyList<MensajeEmitidoDto>? Messages,
    IReadOnlyList<AvisoDto> Warnings);

/// <summary>Una diferencia de costo que dejó la anulación. (nuevo)</summary>
public sealed record AjusteDeCostoDeAnulacionDto(ReferenciaDto Product, decimal Difference);

/// <summary><c>VoidResultDto</c> (§9.5).</summary>
public sealed record VoidResultDto(
    Guid VoidingDocumentPublicId,
    string? DisplayNumber,
    DocumentStatus Status,
    DateOnly OperationDate,
    IReadOnlyList<AjusteDeCostoDeAnulacionDto>? CostAdjustments,
    IReadOnlyList<MensajeEmitidoDto>? Messages);
