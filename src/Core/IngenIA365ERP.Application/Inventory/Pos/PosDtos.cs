using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Pos;

// Los contratos de la venta en el POS (feature 012, I3, T603–T606; contracts/api.md §20.2). Todos (nuevo).

/// <summary>Un descuento pedido: <c>percent</c> como fracción (0,05 = 5 %, api.md §2.6) o <c>amount</c> en pesos, en la base de la lista. (nuevo)</summary>
public sealed record PosDiscountInput(decimal? Percent = null, decimal? Amount = null);

/// <summary>Una referencia corta: id público, código y nombre. (nuevo)</summary>
public sealed record PosRefDto(Guid PublicId, string Code, string Name);

/// <summary>Lo que devuelve el lector (<c>LookupPosProductQuery</c>). (nuevo)</summary>
public sealed record PosLookupDto(
    PosRefDto Product,
    ProductStatus Status,
    PosRefDto Unit,
    decimal Factor,
    decimal Price,
    PosRefDto? PriceList,
    bool IncludesTaxes,
    decimal? Available);

/// <summary>El tipo de la venta y su rol en la caja. (nuevo)</summary>
public sealed record PosDocumentTypeDto(Guid PublicId, string Code, CashRegisterDocumentRole Role);

/// <summary>El comprador: el consumidor final genérico o una persona identificada. (nuevo)</summary>
public sealed record PosCustomerDto(Guid? PersonPublicId, string Name, bool IsFinalConsumer, string? Segment);

/// <summary>El vendedor (FR-057). (nuevo)</summary>
public sealed record PosSalespersonDto(Guid SalespersonPublicId, string Name);

/// <summary>Un descuento de la línea con su aprobación si la exige. (nuevo)</summary>
public sealed record PosLineDiscountDto(
    byte Sequence,
    DiscountSource Source,
    bool FromDocumentDiscount,
    bool IsPriceOverride,
    decimal? Percent,
    decimal Amount,
    bool RequiresApproval,
    PosLineDiscountApprovalDto? Approval);

/// <summary>El estado de la aprobación de un descuento. (nuevo)</summary>
public sealed record PosLineDiscountApprovalDto(Guid? ApprovalRequestPublicId, string Status);

/// <summary>Un impuesto de la línea. (nuevo)</summary>
public sealed record PosLineTaxDto(string TaxRateCode, TaxKind Kind, decimal? Rate, decimal Base, decimal Amount);

/// <summary><c>PosLineDto</c> (§20.2). <see cref="BelowCost"/> no expone el costo. (nuevo)</summary>
public sealed record PosLineDto(
    Guid LinePublicId,
    int LineNumber,
    PosRefDto Product,
    PosRefDto Unit,
    decimal Factor,
    decimal Quantity,
    decimal QuantityBase,
    decimal RoundingQuantity,
    decimal ListPrice,
    decimal UnitPrice,
    bool IncludesTaxes,
    PosRefDto? PriceList,
    IReadOnlyList<PosLineDiscountDto> Discounts,
    IReadOnlyList<PosLineTaxDto> Taxes,
    decimal Total,
    decimal? Available,
    bool BelowCost);

/// <summary>Los totales de la venta (T26). (nuevo)</summary>
public sealed record PosTotalsDto(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal WithholdingTotal, decimal Total, decimal AmountDue);

/// <summary>El descuento por total vigente (la suma de su prorrata). (nuevo)</summary>
public sealed record PosDocumentDiscountDto(decimal? Percent, decimal Amount, bool RequiresApproval);

/// <summary>Un medio que se ofrece en esta venta (<c>DisponibilidadDeMedio</c>). (nuevo)</summary>
public sealed record PosPaymentMeansDto(
    Guid PaymentMeansPublicId,
    string Code,
    string Name,
    PaymentMeansClass Class,
    string? QuickKey,
    bool RequiresReference,
    PaymentReferenceKind? ReferenceKind,
    bool AllowsChange,
    bool AllowsPartial,
    CashCountMethod CountMethod,
    IReadOnlyList<PosCardTerminalDto> CardTerminals,
    Guid? DefaultCardTerminalPublicId,
    PosCreditDefaultsDto? CreditDefaults);

/// <summary>Un datáfono del adquirente del medio. (nuevo)</summary>
public sealed record PosCardTerminalDto(Guid PublicId, string Code);

/// <summary>Los valores propuestos de un medio de crédito. (nuevo)</summary>
public sealed record PosCreditDefaultsDto(short? TermDays, short? Installments, short? PeriodicityDays, string? SuggestedLineCode);

/// <summary>Una venta suspendida: rótulo, cuándo y quién. (nuevo)</summary>
public sealed record PosSuspendedDto(string? Label, DateTime? SuspendedAt, string? SuspendedByName);

/// <summary>Una aprobación pendiente de la venta. (nuevo)</summary>
public sealed record PosPendingApprovalDto(Guid ApprovalRequestPublicId, string Subject, string SourceType, int? LineNumber, string Status);

/// <summary><c>PosDraftDto</c> (§20.2). <see cref="DraftPublicId"/> es el <c>documentPublicId</c> de §18.1. (nuevo)</summary>
public sealed record PosDraftDto(
    Guid DraftPublicId,
    DocumentStatus Status,
    DocumentClass Class,
    PosDocumentTypeDto DocumentType,
    Guid? CashSessionPublicId,
    PosRefDto PointOfSale,
    PosRefDto CashRegister,
    PosRefDto Warehouse,
    DateOnly OperationDate,
    PosCustomerDto Customer,
    PosSalespersonDto? Salesperson,
    IReadOnlyList<PosLineDto> Lines,
    PosLineDto? LastLine,
    PosDocumentDiscountDto? DocumentDiscount,
    PosTotalsDto Totals,
    IReadOnlyList<PosPaymentMeansDto> AvailablePaymentMeans,
    PosSuspendedDto? Suspended,
    IReadOnlyList<PosPendingApprovalDto> PendingApprovals,
    IReadOnlyList<AvisoDto> Warnings,
    string? Notes,
    byte[]? RowVersion);

/// <summary><c>PosDraftSummaryDto</c> (§20.2): las ventas en curso o suspendidas del punto. (nuevo)</summary>
public sealed record PosDraftSummaryDto(
    Guid DraftPublicId,
    Guid? CashSessionPublicId,
    string? CashierName,
    int Lines,
    decimal Total,
    PosSuspendedDto? Suspended);
