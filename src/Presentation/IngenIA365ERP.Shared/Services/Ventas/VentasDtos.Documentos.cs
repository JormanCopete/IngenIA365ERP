using System.Text.Json;
using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Ventas;

// Espejos de las ventas de oficina (feature 012, I3, T633; contracts/api.md §18.1–§18.3): la consulta de documentos de venta, su
// detalle, los borradores de factura y de nota. (nuevos)

public sealed record PagoResumidoDto(string MeansCode, int MeansClass, decimal Amount);

public sealed record ResumenDeVentaDto(
    Guid DocumentPublicId,
    int Class,
    string DocumentTypeCode,
    string Prefix,
    long? Number,
    int Status,
    DateOnly OperationDate,
    string? PointOfSaleCode,
    string? CashRegisterCode,
    string CounterpartyName,
    string? SalespersonName,
    decimal Total,
    decimal AmountDue,
    IReadOnlyList<PagoResumidoDto> Payments,
    int? PostingMode,
    string? ElectronicStatus,
    bool PendingDelivery,
    bool PendingValidation)
{
    public string Numero => Number is { } n ? $"{Prefix}{n}" : "(borrador)";
}

public sealed record CompradorDto(Guid PersonPublicId, bool IsFinalConsumer, string Name, string? IdType, string? IdNumber, string? Address, string? Email);

public sealed record AprobacionDeVentaDto(Guid ApprovalRequestPublicId, string Status);

public sealed record DescuentoDeLineaDeVentaDto(int Source, decimal? Percent, decimal Amount, bool FromDocumentDiscount, bool IsPriceOverride, AprobacionDeVentaDto? Approval);

public sealed record ImpuestoDeVentaDto(string TaxRateCode, int Kind, decimal? Rate, decimal? AmountPerUnit, decimal Base, decimal Amount, int Treatment);

public sealed record LineaDeVentaDto(
    Guid LinePublicId,
    int LineNumber,
    ReferenciaDeInventarioDto Product,
    UnidadDeLineaDto Unit,
    decimal Factor,
    decimal Quantity,
    decimal QuantityBase,
    decimal RoundingQuantity,
    decimal ListPrice,
    decimal UnitPrice,
    bool IncludesTaxes,
    ReferenciaDeInventarioDto? PriceList,
    IReadOnlyList<DescuentoDeLineaDeVentaDto> Discounts,
    IReadOnlyList<ImpuestoDeVentaDto> Taxes,
    decimal Subtotal,
    decimal Total,
    bool BelowCost);

public sealed record TotalesDeVentaDto(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal WithholdingTotal, decimal Total, decimal AmountDue);

public sealed record PagoDelDocumentoDto(
    Guid DocumentPaymentPublicId,
    int LineNumber,
    int Direction,
    decimal Amount,
    decimal? Tendered,
    decimal Change,
    string MeansCode,
    int MeansClass,
    string? NetworkCode,
    string? AcquirerCode,
    Guid? BankPublicId,
    string? Reference,
    string? AuthorizationCode,
    string? Last4,
    string? BatchNumber,
    Guid? CashSessionPublicId,
    bool PendingValidation,
    int? CreditOrigin,
    int? VoucherRedemptionStatus);

public sealed record MensajeDeVentaDto(Guid MessagePublicId, string Type, string Destination, string DeliveryStatus, long? BatchNumber);

public sealed record VinculosDeVentaDto(
    DocumentoReferidoDeInventarioDto? Origin,
    DocumentoReferidoDeInventarioDto? Voids,
    DocumentoReferidoDeInventarioDto? VoidedBy,
    IReadOnlyList<DocumentoReferidoDeInventarioDto> Notes,
    DocumentoReferidoDeInventarioDto? ReplacementOf,
    DocumentoReferidoDeInventarioDto? ReplacedBy);

public sealed record HallazgoDeVentaDto(string Code, string Message, int? LineNumber);

public sealed record DocumentoDeVentaDto(
    Guid DocumentPublicId,
    int Class,
    ReferenciaDeInventarioDto DocumentType,
    string Prefix,
    long? Number,
    int Status,
    DateOnly OperationDate,
    DateTime? ConfirmedAt,
    ReferenciaDeInventarioDto? Warehouse,
    ReferenciaDeInventarioDto Branch,
    ReferenciaDeInventarioDto? PointOfSale,
    ReferenciaDeInventarioDto? CashRegister,
    Guid? CashSessionPublicId,
    ReferenciaDeInventarioDto? SalesChannel,
    int? PostingMode,
    CompradorDto? Counterparty,
    ReferenciaDeInventarioDto? Salesperson,
    IReadOnlyList<LineaDeVentaDto> Lines,
    IReadOnlyList<ImpuestoDeVentaDto> Withholdings,
    TotalesDeVentaDto Totals,
    IReadOnlyList<PagoDelDocumentoDto> Payments,
    IReadOnlyList<MensajeDeVentaDto> Messages,
    JsonElement? Electronic,
    VinculosDeVentaDto Links,
    UsuarioDeInventarioDto CreatedBy,
    UsuarioDeInventarioDto? ConfirmedBy,
    IReadOnlyList<HallazgoDeVentaDto> Issues,
    byte[]? RowVersion)
{
    public string Numero => Number is { } n ? $"{Prefix}{n}" : "(borrador)";
}

/// <summary>Los filtros de <c>GET /sales/documents</c> (§18.1); <see cref="Class"/> y <see cref="Status"/> son números del dominio.</summary>
public sealed class FiltroDeVentas
{
    public int? Class { get; set; }
    public Guid? DocumentType { get; set; }
    public int? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? PointOfSale { get; set; }
    public Guid? CashRegister { get; set; }
    public Guid? CashSession { get; set; }
    public Guid? Person { get; set; }
    public Guid? Salesperson { get; set; }
    public long? Number { get; set; }
    public bool? PendingDelivery { get; set; }
    public bool? PendingValidation { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed record LineaDeVentaRequest(Guid ProductPublicId, Guid UnitPublicId, decimal Quantity, decimal? UnitPrice = null, DescuentoRequest? Discount = null,
    string? Notes = null, Guid? LinePublicId = null, int? LineNumber = null);

/// <summary>El borrador de una factura o comprobante de oficina (<c>SalesDraftInput</c>, §18.2), con sus pagos.</summary>
public sealed record BorradorDeVentaRequest(
    Guid DocumentTypePublicId,
    Guid WarehousePublicId,
    IReadOnlyList<LineaDeVentaRequest> Lines,
    IReadOnlyList<PagoRequest> Payments,
    DateOnly? OperationDate = null,
    Guid? CounterpartyPersonPublicId = null,
    Guid? SalespersonPublicId = null,
    Guid? CostCenterPublicId = null,
    string? ExternalReference = null,
    DateOnly? DueDate = null,
    string? Notes = null,
    DescuentoRequest? DocumentDiscount = null,
    IReadOnlyList<Guid>? OriginPublicIds = null,
    byte[]? RowVersion = null);

public sealed record LineaDeNotaRequest(Guid OriginLinePublicId, decimal? Quantity = null, decimal? Amount = null);

/// <summary>El borrador de una nota de venta (<c>CreditNoteDraftInput</c>, §18.3): origen, motivo, devolución y reintegros.</summary>
public sealed record BorradorDeNotaRequest(
    Guid OriginDocumentPublicId,
    string Reason,
    bool TotalVoid,
    bool WithReturn,
    IReadOnlyList<LineaDeNotaRequest> Lines,
    IReadOnlyList<PagoRequest> Refunds,
    Guid? DocumentTypePublicId = null,
    DateOnly? OperationDate = null,
    string? CorrectionConceptCode = null,
    Guid? ReturnWarehousePublicId = null,
    byte[]? RowVersion = null);

public sealed record ConfirmarVentaRequest(decimal? ExpectedAmountDue, byte[]? RowVersion);

public sealed record AnularVentaRequest(string Reason, DateOnly? OperationDate, Guid? CashSessionPublicId);
