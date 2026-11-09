using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>VentaFacturada</c> v2 (feature 022, T047; FR-047, Q16; precedente <see cref="VentaACreditoRegistradaV2"/>): la v1
/// (<see cref="VentaFacturadaV1"/>) con los mismos campos en su orden, salvo que cada pago es un <see cref="PaymentLineV2"/>, que agrega al
/// final la cuenta bancaria propia a la que entró una transferencia o consignación. Comercial emite sólo v2 en las ventas desde la 022;
/// Contabilidad acepta v1 y v2 (y las anulaciones de ventas v1). Las notas siguen en v1. Mismo <c>Type</c> que la v1: la versión la dice el
/// catálogo. (nuevo)
/// </summary>
public sealed record VentaFacturadaV2
{
    public const string Type = VentaFacturadaV1.Type;

    public string Operation { get; init; } = "Venta";

    public string? SalesChannelCode { get; init; }

    public string? PointOfSaleCode { get; init; }

    public string? CashRegisterCode { get; init; }

    /// <summary>Sesión de caja: la <c>BatchScopeKey</c> del disparador <c>CierreDeTurno</c>.</summary>
    public Guid? CashSessionPublicId { get; init; }

    /// <summary>Remisiones de una factura desde remisiones; vacío en las demás.</summary>
    public IReadOnlyList<DocumentRefV1> DerivedFrom { get; init; } = [];

    public IReadOnlyList<SalesAmountLineV1> Lines { get; init; } = [];

    /// <summary>Impuestos <c>Generated</c> y retenciones que practica el comprador (<c>WithholdingSuffered</c>).</summary>
    public IReadOnlyList<TaxLineV1> Taxes { get; init; } = [];

    /// <summary>Pagos por medio, todos <c>Received</c>, con la cuenta propia de las transferencias y consignaciones.</summary>
    public IReadOnlyList<PaymentLineV2> Payments { get; init; } = [];

    public SalesTotalsV1 Totals { get; init; } = new();

    /// <summary>Los pagos en la forma v1 (sin la cuenta propia), para el código que sólo mira lo común.</summary>
    public IReadOnlyList<PaymentLineV1> PagosV1() => Payments.Select(p => p.ComoV1()).ToList();
}

/// <summary>
/// Un renglón por pago (feature 022, T047): los campos de <see cref="PaymentLineV1"/> en su orden y, al final,
/// <see cref="OwnBankAccountPublicId"/>: la cuenta bancaria propia de Tesorería a la que entró una transferencia o consignación (nula en los
/// demás medios y sin cuentas habilitadas en el punto). Con ella, Contabilidad lleva el pago a la cuenta contable de su vínculo
/// (<c>ACC_OwnBankAccountLinks</c>); sin ella, por la regla del medio. Nunca el número de la tarjeta. (nuevo)
/// </summary>
public sealed record PaymentLineV2
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

    /// <summary>La cuenta bancaria propia (Tesorería) a la que entró una transferencia o consignación; nula si no aplica.</summary>
    public Guid? OwnBankAccountPublicId { get; init; }

    /// <summary>El mismo pago en la forma v1.</summary>
    public PaymentLineV1 ComoV1() => new()
    {
        PaymentPublicId = PaymentPublicId,
        LineNumber = LineNumber,
        PaymentMeansCode = PaymentMeansCode,
        PaymentMeansClass = PaymentMeansClass,
        Direction = Direction,
        Amount = Amount,
        Reference = Reference,
        ThirdPartyPersonPublicId = ThirdPartyPersonPublicId,
        CardNetworkCode = CardNetworkCode,
        CardAcquirerCode = CardAcquirerCode,
        CardTerminalCode = CardTerminalCode,
        BatchNumber = BatchNumber,
        PendingValidation = PendingValidation,
    };
}
