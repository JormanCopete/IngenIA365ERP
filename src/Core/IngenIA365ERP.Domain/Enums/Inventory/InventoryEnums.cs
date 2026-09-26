namespace IngenIA365ERP.Domain.Enums.Inventory;

// Enumeraciones del módulo comercial (feature 012, T025; decisiones-transversales §2.5 y data-model §26).
//
// Se guardan como int: el número ES el dato en la base y en los mensajes ya emitidos, así que un
// valor nunca se renumera ni se reutiliza. Los nombres están en inglés; la pantalla traduce.
// Ninguna historia vuelve a crearlas ni les cambia valores: sólo agregan las que falten (y las
// registran en §2.5). Lo fija EnumeracionesDelComercioTests (Domain.Tests).

// ------------------------------------------------------------------------- catálogo y bodegas --

public enum ProductKind { Inventoriable = 1, Service = 2, Combo = 3, Kit = 4, Template = 5, Variant = 6 }

public enum ProductStatus { Active = 1, Inactive = 2, Blocked = 3 }

public enum WarehouseBehavior { Operational = 1, Transit = 2 }

public enum WarehouseActivationStatus { NotActivated = 0, Active = 1 }

/// <summary>
/// Para qué sirve una unidad alterna del producto (contracts/api.md §17.1; nuevo en §2.5, T198): viaja en el cuerpo de
/// <c>/products/{id}/units</c> y en la plantilla; en la base se guarda como las dos marcas <c>UsedForPurchase</c> y
/// <c>UsedForSale</c> de <c>INV_ProductUnits</c>.
/// </summary>
public enum ProductUnitUsage { Purchase = 1, Sale = 2, Both = 3 }

// --------------------------------------------------------------------------------- documentos --

/// <summary>Las 34 clases de documento. La clase decide efecto, fiscalidad, mensajes y cadena (ClasesDeDocumento).</summary>
public enum DocumentClass
{
    PurchaseRequest = 1,
    PurchaseOrder = 2,
    PurchaseReceipt = 3,
    SupplierInvoice = 4,
    SupplierNote = 5,
    SupportDocument = 6,
    SupportDocumentAdjustmentNote = 7,
    LandedCost = 8,
    SupplierReturn = 9,
    PositiveAdjustment = 10,
    NegativeAdjustment = 11,
    InternalConsumption = 12,
    WriteOff = 13,
    Assembly = 14,
    OpeningBalance = 15,
    TransferDispatch = 16,
    TransferReceipt = 17,
    LocationMove = 18,
    PhysicalCount = 19,
    CostAdjustment = 20,
    CashMovement = 21,
    CashCountDifference = 22,
    SalesQuote = 23,
    SalesOrder = 24,
    Shipment = 25,
    SalesInvoice = 26,
    SalesInvoiceFromShipments = 27,
    PosEquivalentDocument = 28,
    NonElectronicSalesReceipt = 29,
    NonElectronicSalesNote = 30,
    CreditNote = 31,
    PosAdjustmentNote = 32,
    DebitNote = 33,
    Voiding = 34,
}

/// <summary>Grupo de la clase: decide la ruta y la familia de permisos (T17).</summary>
public enum DocumentClassGroup
{
    Purchases = 1, Adjustments = 2, Transfers = 3, Counts = 4, Sales = 5, Cash = 6, OpeningBalance = 7, Costing = 8,
}

public enum DocumentStatus { Draft = 0, PendingApproval = 1, Confirmed = 2, Voided = 3, Discarded = 4 }

public enum PostingMode { Online = 1, Batch = 2, NotPosted = 3 }

public enum PostingGranularity { PerDocument = 1, Summarized = 2 }

public enum BatchScheduleKind { Daily = 1, CashSessionClose = 2, PeriodClose = 3 }

public enum PostingChain { None = 0, Purchases = 1, Sales = 2, Transfers = 3 }

/// <summary>
/// Vínculo entre documentos. Cerrado: pedido ← cotización es <see cref="FromOrder"/>, remisión ←
/// pedido es <see cref="DispatchOf"/>; no existe <c>FromQuote</c>.
/// </summary>
public enum DocumentLinkKind
{
    Voids = 1, FromOrder = 2, FromShipment = 3, NoteOf = 4, ReceiptOf = 5, InvoiceOfReceipt = 6,
    ReturnOf = 7, DispatchOf = 8, CountAdjustmentOf = 9, ReplacementOf = 10, LandedCostOf = 11,
}

// ------------------------------------------------------------------ kárdex, costo y períodos --

public enum KardexEntryKind { Entry = 1, Exit = 2, CostAdjustment = 3 }

public enum KardexReason
{
    Normal = 1, Retroactive = 2, PriceDifference = 3, LandedCost = 4, NegativeRegularization = 5,
    VoidDifference = 6, RoundingResidue = 7, MethodChange = 8,
}

public enum CostMethod { WeightedAverage = 1, Fifo = 2 }

public enum CostScope { Cooperative = 1, Warehouse = 2 }

public enum InventoryPeriodStatus { Open = 1, Closed = 2 }

// -------------------------------------------------------------------- conteos y traslados --

public enum CountKind { Total = 1, Cyclic = 2 }

public enum CountScope { All = 0, Category = 1, Location = 2, Selection = 3, AbcClass = 4 }

public enum TransferDiscrepancyKind { Shortage = 1, Surplus = 2 }

public enum TransferDiscrepancyResolution { ReturnToOrigin = 1, WriteOffFromTransit = 2, LateReceipt = 3, SurplusAdjustment = 4 }

// -------------------------------------------------------------------------------------- caja --

public enum CashSessionStatus { Open = 1, Closed = 2 }

public enum CashMovementKind
{
    WithdrawalToSafe = 1, WithdrawalToRegister = 2, WithdrawalForDeposit = 3, BaseIncome = 4, ReclassificationBetweenMeans = 5,
}

public enum CashMovementDestination { Safe = 1, Register = 2, Deposit = 3 }

public enum CashDifferenceTreatment { Surplus = 1, ShortageToCashier = 2, ShortageToExpense = 3 }

/// <summary>
/// Rol de cada tipo de documento en una caja. Las notas no electrónicas van en
/// <see cref="PosAdjustmentNote"/>; cada rol de venta tiene su rol de contingencia (FR-058, FR-067).
/// </summary>
public enum CashRegisterDocumentRole
{
    PosSale = 1, InvoiceOnRequest = 2, PosAdjustmentNote = 3, InvoiceCreditNote = 4, PosSaleContingency = 5, InvoiceContingency = 6,
}

/// <summary>Billete o moneda. Vive aquí y no en Core, como lo pone §2.5.</summary>
public enum CashDenominationKind { Bill = 1, Coin = 2 }

/// <summary>Estado del cierre del día de un punto de venta (data-model §26; nuevo en §2.5).</summary>
public enum DayCloseStatus { Closed = 1, Reopened = 2 }

// ------------------------------------------------------------------------ ventas y compras --

public enum ReservationStatus { Active = 1, Consumed = 2, Released = 3, Expired = 4 }

public enum PaymentDirection { Received = 1, Refunded = 2 }

/// <summary>
/// De dónde sale un pago a crédito que todavía no validó Cartera (data-model §26; nuevo en §2.5).
/// Es el mismo texto que viaja en <c>VentaACreditoRegistradaV1</c>.
/// </summary>
public enum CreditOrigin { ProvisionalCredit = 1, LendingNoResponse = 2, Validated = 3 }

public enum DiscountSource { Manual = 1, Promotion = 2 }

public enum VoucherRedemptionStatus { Active = 1, Released = 2 }

public enum PromotionKind { Percent = 1, Amount = 2, BuyNPayM = 3, QuantityPrice = 4, BundlePrice = 5 }

/// <summary>A qué alcanza una promoción: una sola columna destino llena por fila (data-model §26; nuevo en §2.5; I6).</summary>
public enum PromotionScopeKind { Product = 1, Category = 2, Segment = 3, Channel = 4 }

/// <summary>Eventos RADIAN que el comprador emite sobre la factura del proveedor: el valor es el código DIAN.</summary>
public enum SupplierInvoiceEventCode { Receipt030 = 30, GoodsReceived032 = 32 }

public enum SupplierInvoiceEventStatus { Pending = 0, RegisteredExternally = 1, Emitted = 2, Rejected = 3, NotApplicable = 4 }

