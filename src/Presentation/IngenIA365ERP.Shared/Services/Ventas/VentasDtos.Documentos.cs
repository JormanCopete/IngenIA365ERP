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
    bool PendingValidation,
    Guid? CounterpartyPersonPublicId = null)
{
    public string Numero => Number is { } n ? $"{Prefix}{n}" : "(borrador)";
}

public sealed record CompradorDto(Guid PersonPublicId, bool IsFinalConsumer, string Name, string? IdType, string? IdNumber, string? Address, string? Email);

public sealed record AprobacionDeVentaDto(Guid ApprovalRequestPublicId, string Status);

/// <summary>Un descuento de la línea. I6 (T900): con <c>source = Promotion</c>, la promoción que lo dio (FR-055).</summary>
public sealed record DescuentoDeLineaDeVentaDto(int Source, decimal? Percent, decimal Amount, bool FromDocumentDiscount, bool IsPriceOverride, AprobacionDeVentaDto? Approval,
    Guid? PromotionPublicId = null, string? PromotionName = null)
{
    public bool EsPromocion => Source == TextosDeVentas.DescuentoDePromocion;
}

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
    bool BelowCost,
    Guid? OriginLinePublicId = null,
    decimal? Pending = null);

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
    byte[]? RowVersion,
    DateOnly? ValidUntil = null,
    string? CorrectionConceptCode = null,
    IReadOnlyList<DocumentoReferidoDeInventarioDto>? Origins = null)
{
    public string Numero => Number is { } n ? $"{Prefix}{n}" : "(borrador)";

    /// <summary>El bloque <c>electronic</c> tipado (I4, T758): nulo si el documento no es electrónico.</summary>
    public ElectronicoDeLaVentaDto? Electronico => ElectronicoDeLaVentaDto.Desde(Electronic);
}

/// <summary>
/// El bloque <c>electronic</c> de la venta y del cobro (feature 012, I4, T758; api.md §18.2, §20.2): el documento electrónico, su estado
/// (<c>ElectronicDocumentStatus</c>), el código único y el QR, la contingencia, si ya se entrega y si quedó pendiente de entrega, y los
/// mensajes traducidos. (nuevo)
/// </summary>
public sealed record ElectronicoDeLaVentaDto(
    Guid ElectronicDocumentPublicId,
    int Kind,
    int Status,
    string? UniqueCode,
    string? QrContent,
    int? ContingencyType,
    long WaitedMs,
    bool Deliverable,
    bool PendingDelivery,
    IReadOnlyList<IngenIA365ERP.Shared.Services.FacturacionElectronica.MensajeDelCanalDto>? Messages)
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    /// <summary>Lee el bloque tal como llega en <c>electronic</c>; nulo si no viene o no se entiende.</summary>
    public static ElectronicoDeLaVentaDto? Desde(JsonElement? bloque)
    {
        if (bloque is not { ValueKind: JsonValueKind.Object } json) return null;
        try { return json.Deserialize<ElectronicoDeLaVentaDto>(Opciones); }
        catch (JsonException) { return null; }
    }
}

/// <summary><c>POST …/invoice-instead</c> (§18.3.1, I4): a nombre de quién va la factura, el motivo y lo que la persona vio a pagar. (nuevo)</summary>
public sealed record FacturaEnLugarRequest(Guid BuyerPersonPublicId, string Reason, decimal ExpectedAmountDue);

/// <summary>La nota de ajuste de anulación total y la factura que la reemplaza, hechas en una transacción. (nuevo)</summary>
public sealed record FacturaEnLugarDelDocumentoEquivalenteDto(Guid AdjustmentNotePublicId, string? AdjustmentNoteNumber, Guid InvoicePublicId,
    string? InvoiceNumber, IReadOnlyList<JsonElement>? Messages);

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
    string? Notes = null, Guid? LinePublicId = null, int? LineNumber = null, Guid? OriginLinePublicId = null);

/// <summary>
/// El borrador de una factura o comprobante de oficina (<c>SalesDraftInput</c>, §18.2), con sus pagos. I6 (§18.4): el mismo cuerpo sirve a
/// cotizaciones (<see cref="ValidUntil"/>), pedidos, remisiones y notas débito (<see cref="CorrectionConceptCode"/>, y la factura en
/// <see cref="OriginPublicIds"/>).
/// </summary>
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
    byte[]? RowVersion = null,
    DateOnly? ValidUntil = null,
    string? CorrectionConceptCode = null);

/// <summary><c>POST /quotes/{id}/to-order</c>: el tipo del pedido (sin él, el primero activo). (nuevo)</summary>
public sealed record ConvertirEnPedidoRequest(Guid? DocumentTypePublicId);

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

// ------------------------------------------------------------------------------------------ el crédito (US6) --
// Espejos del crédito provisional (feature 012, I3, T658; contracts/api.md §23.1, §23.2). (nuevos)

/// <summary>El cuerpo de <c>POST /sales/credit-evaluations</c>: una consulta, sin clave de operación.</summary>
public sealed record EvaluacionDeCreditoRequest(Guid PersonPublicId, Guid PaymentMeansPublicId, decimal Amount, DateOnly? OperationDate = null,
    Guid? PointOfSalePublicId = null, Guid? DocumentTypePublicId = null);

public sealed record MotivoDelCreditoDto(string Code, string Message);

public sealed record PersonaDelCreditoDto(Guid PublicId, string Name, bool IsAssociate, bool? AssociateActive, bool IsCustomer, bool Eligible,
    IReadOnlyList<MotivoDelCreditoDto> Reasons);

public sealed record NivelDelCreditoDto(int Order, string PermissionCode, decimal Threshold);

public sealed record AprobacionDelCreditoDto(IReadOnlyList<NivelDelCreditoDto> Levels, decimal? ApproverMaxAmount);

public sealed record CondicionesDelCreditoDto(short? MaxInstallments, short? TermDays, short? PeriodicityDays, string? SuggestedLineCode, short? MaxTermDays,
    short? DefaultInstallments);

/// <summary>La evaluación (§23.1). <c>Origin</c> es <c>CreditOrigin</c> como número; <c>Lending</c> sólo con Cartera habilitada (IC).</summary>
public sealed record EvaluacionDeCreditoDto(bool LendingEnabled, int Origin, PersonaDelCreditoDto Person, bool RequiresApproval,
    AprobacionDelCreditoDto? Approval, CondicionesDelCreditoDto CreditDefaults, JsonElement? Lending);

public sealed record AprobacionDelPagoDto(Guid ApprovalRequestPublicId, string Status, string? ApprovedByName, int? Level, DateTime? DecidedAt);

public sealed record PagoACreditoDto(Guid DocumentPaymentPublicId, string MeansCode, int MeansClass, decimal Amount, short? Installments, short? TermDays,
    short? PeriodicityDays, DateOnly? FirstDueDate, DateOnly? FinalDueDate, string? SuggestedLineCode, bool PendingValidation, int? CreditOrigin,
    AprobacionDelPagoDto? Approval);

public sealed record ValidacionDeCarteraDto(string Status, DateTime? EvaluatedAt, string? Reason);

public sealed record MensajeACarteraDto(Guid MessagePublicId, string Type, string DeliveryStatus, bool DestinationAvailable, ValidacionDeCarteraDto Validation);

/// <summary>La pestaña «Crédito» de una venta (§23.2): nunca trae saldos de Cartera.</summary>
public sealed record CreditoDeLaVentaDto(IReadOnlyList<PagoACreditoDto> Payments, string? AccountsReceivableRecordedBy,
    IReadOnlyList<MensajeACarteraDto> LendingMessages);
