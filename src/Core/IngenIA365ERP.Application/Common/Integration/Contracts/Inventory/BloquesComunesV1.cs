using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

// Bloques comunes de los mensajes (feature 012, T8, T075; contracts/mensajes.md §5), también en v1. El orden
// de las propiedades es el del contrato: el JSON sale en ese orden (OpcionesDeMensajes). Un cambio
// incompatible de un bloque es un bloque V2 y, con él, una V2 de cada mensaje que lo usa (§12).
//
// Signo (§3): todo importe y toda cantidad llevan el suyo. Positivo es el efecto natural del mensaje;
// negativo, el contrario. Los pagos llevan amount positivo y el sentido en direction.

/// <summary>Referencia a un documento de Inventario: el relacionado, un origen de derivado, el afectado.</summary>
public sealed record DocumentRefV1
{
    public Guid PublicId { get; init; }

    public DocumentClass DocumentClass { get; init; }

    public string Number { get; init; } = string.Empty;
}

/// <summary>Un usuario como <b>dato</b> (cajero, aprobador, quien confirmó), nunca como actor.</summary>
public sealed record UserRefV1
{
    public Guid? CentralUserId { get; init; }

    public string Name { get; init; } = string.Empty;
}

/// <summary>Importes de venta o nota por grupo contable y bodega, sin impuestos (§5).</summary>
public sealed record SalesAmountLineV1
{
    /// <summary><c>INV_AccountingGroups.Code</c>: dimensión exigida del rol <c>Ingreso</c>.</summary>
    public string AccountingGroupCode { get; init; } = string.Empty;

    /// <summary>Bodega de la que salió; nula en servicios.</summary>
    public string? WarehouseCode { get; init; }

    /// <summary>Cantidad × precio, antes de descuentos (18,2).</summary>
    public decimal GrossAmount { get; init; }

    /// <summary>Descuentos de línea, promociones y la prorrata del descuento por total (18,2).</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary><c>GrossAmount − DiscountAmount</c>: la base gravable (18,2).</summary>
    public decimal NetAmount { get; init; }

    /// <summary>Números de línea del documento que suman aquí (FR-074).</summary>
    public IReadOnlyList<int> DocumentLines { get; init; } = [];
}

/// <summary>Un renglón por impuesto y tarifa (y por concepto y municipio en retenciones), foto del motor tributario.</summary>
public sealed record TaxLineV1
{
    /// <summary><c>COR_TaxDefinitions.Code</c>.</summary>
    public string TaxCode { get; init; } = string.Empty;

    public TaxKind TaxKind { get; init; }

    /// <summary><c>COR_TaxRates</c>: dimensión exigida de los roles <c>Impuesto</c> y <c>Retencion</c>.</summary>
    public string TaxRateCode { get; init; } = string.Empty;

    /// <summary>Tarifa como <b>fracción</b> (9,6): <c>0.19</c>, <c>0.00966</c>. Nula en los de valor por unidad.</summary>
    public decimal? Rate { get; init; }

    /// <summary>Valor por unidad (impuesto de bolsas y similares) (18,2).</summary>
    public decimal? AmountPerUnit { get; init; }

    /// <summary>Unidades gravables en los de valor por unidad (18,4).</summary>
    public decimal? TaxableUnits { get; init; }

    /// <summary>Decide el lado del asiento (contracts/contabilidad.md §3.5).</summary>
    public TaxTreatment Treatment { get; init; }

    /// <summary>Base (en ReteIVA, el IVA sobre el que se calcula) (18,2).</summary>
    public decimal TaxableBase { get; init; }

    public decimal Amount { get; init; }

    /// <summary><c>COR_WithholdingConcepts.Code</c>.</summary>
    public string? WithholdingConceptCode { get; init; }

    /// <summary>Municipio de ICA y ReteICA (DIVIPOLA, 5).</summary>
    public string? MunicipalityDaneCode { get; init; }

    public IReadOnlyList<int> DocumentLines { get; init; } = [];
}

/// <summary>Un renglón por pago del documento (<c>INV_DocumentPayments</c>); nunca el número de la tarjeta.</summary>
public sealed record PaymentLineV1
{
    public Guid PaymentPublicId { get; init; }

    public int LineNumber { get; init; }

    /// <summary><c>COR_PaymentMeans.Code</c>: dimensión exigida del rol <c>MedioDePago</c>.</summary>
    public string PaymentMeansCode { get; init; } = string.Empty;

    public PaymentMeansClass PaymentMeansClass { get; init; }

    public PaymentDirection Direction { get; init; }

    /// <summary>Lo aplicado, positivo; en efectivo, sin las vueltas (18,2).</summary>
    public decimal Amount { get; init; }

    public string? Reference { get; init; }

    /// <summary>Adquirente en tarjetas, banco en consignaciones y transferencias, cliente en créditos.</summary>
    public Guid? ThirdPartyPersonPublicId { get; init; }

    public string? CardNetworkCode { get; init; }

    public string? CardAcquirerCode { get; init; }

    public string? CardTerminalCode { get; init; }

    public string? BatchNumber { get; init; }

    /// <summary><c>true</c> sólo en un pago de crédito provisional.</summary>
    public bool PendingValidation { get; init; }
}

/// <summary>Cantidades y costo por grupo contable y bodega, sumados desde el kardex.</summary>
public sealed record CostLineV1
{
    public string AccountingGroupCode { get; init; } = string.Empty;

    public string WarehouseCode { get; init; } = string.Empty;

    /// <summary><c>Transit</c> resuelve el rol <c>Transito</c> en lugar de <c>Inventario</c>.</summary>
    public WarehouseBehavior WarehouseBehavior { get; init; }

    /// <summary>Sucursal de la bodega, cuando difiere de la del sobre.</summary>
    public Guid? BranchPublicId { get; init; }

    /// <summary><c>Entry</c> o <c>Exit</c> en esa bodega.</summary>
    public KardexEntryKind Movement { get; init; }

    /// <summary>Cantidad en unidad base (18,4).</summary>
    public decimal QuantityBase { get; init; }

    /// <summary>Valor al costo (18,2).</summary>
    public decimal Cost { get; init; }

    public IReadOnlyList<int> DocumentLines { get; init; } = [];
}

/// <summary>Un traslado mueve valor de una bodega a otra («de → a»).</summary>
public sealed record TransferCostLineV1
{
    public string AccountingGroupCode { get; init; } = string.Empty;

    public string FromWarehouseCode { get; init; } = string.Empty;

    public WarehouseBehavior FromWarehouseBehavior { get; init; }

    public Guid FromBranchPublicId { get; init; }

    public string ToWarehouseCode { get; init; } = string.Empty;

    public WarehouseBehavior ToWarehouseBehavior { get; init; }

    public Guid ToBranchPublicId { get; init; }

    public decimal QuantityBase { get; init; }

    /// <summary>Valor al costo de la línea de despacho (T18).</summary>
    public decimal Cost { get; init; }

    public IReadOnlyList<int> DocumentLines { get; init; } = [];
}

/// <summary>Diferencia de costo por grupo y bodega, partida en existencia y vendido. Positivo aumenta el valor.</summary>
public sealed record CostDifferenceLineV1
{
    public string AccountingGroupCode { get; init; } = string.Empty;

    public string WarehouseCode { get; init; } = string.Empty;

    public WarehouseBehavior WarehouseBehavior { get; init; }

    /// <summary>Diferencia sobre lo que sigue en existencia (18,2).</summary>
    public decimal InventoryAmount { get; init; }

    /// <summary>Diferencia sobre lo ya vendido o consumido: va al costo (18,2).</summary>
    public decimal SoldAmount { get; init; }
}

// ------------------------------------------------ objetos que comparten varios mensajes (nuevos, §2.16) --

/// <summary><c>totals</c> de <c>VentaFacturada</c>, <c>NotaCreditoEmitida</c> y <c>NotaDebitoEmitida</c> (§6.1).</summary>
public sealed record SalesTotalsV1
{
    public decimal Subtotal { get; init; }

    public decimal DiscountTotal { get; init; }

    public decimal TaxTotal { get; init; }

    public decimal WithholdingTotal { get; init; }

    public decimal Total { get; init; }

    public decimal AmountDue { get; init; }
}

/// <summary><c>approval</c> de <c>DiferenciaDeArqueoAprobada</c> y <c>VentaACreditoRegistrada</c> (§6.15, §8.1).</summary>
public sealed record ApprovalRefV1
{
    public Guid ApprovalRequestPublicId { get; init; }

    public UserRefV1 ApprovedBy { get; init; } = new();

    public int Level { get; init; }

    public ApprovalMethod Method { get; init; }

    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset DecidedAt { get; init; }
}

/// <summary>
/// <c>terms</c> del crédito (§8.1). <see cref="TermUnit"/> (<c>Days</c> | <c>Months</c>) y <see cref="Periodicity"/>
/// (<c>Weekly</c> | <c>Biweekly</c> | <c>Monthly</c> | <c>SinglePayment</c>) son texto con valores cerrados, no enums
/// de §2.5 (§15.12).
/// </summary>
public sealed record CreditTermsV1
{
    public string TermUnit { get; init; } = string.Empty;

    public int Term { get; init; }

    public int Installments { get; init; }

    public string Periodicity { get; init; } = string.Empty;

    public DateOnly FirstDueDate { get; init; }

    public DateOnly FinalDueDate { get; init; }
}
