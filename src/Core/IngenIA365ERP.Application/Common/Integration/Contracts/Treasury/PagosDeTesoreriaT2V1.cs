using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

// Feature 015, T2 (fase 42; FR-124 a FR-127, FR-166, FR-167, FR-220; contracts/contabilidad.md §4; mensajes.md §5): los pagos de T2 —a proveedores de Comercial y de
// cuentas por pagar contables—, el cobro tardío de un cheque que ya había pasado a cuenta por pagar y el aviso del pago a Comercial. (nuevo)

/// <summary>
/// <c>PagoDeProveedorGirado</c> v1 (<c>PPG</c>; FR-124): el giro de una orden nacida de una factura de proveedor de Comercial: <c>CuentasPorPagarProveedores</c> (el
/// proveedor como tercero, la factura como cruce) → <c>Banco</c>, con la fecha del débito. En línea, comprobante sugerido <c>TP</c>. (nuevo)
/// </summary>
public sealed record PagoDeProveedorGiradoV1
{
    public const string Type = "PagoDeProveedorGirado";

    public Guid PaymentOrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public Guid InvoicePublicId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public Guid SupplierPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly DebitDate { get; init; }

    public string? TreasuryReference { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>PagoDeCuentaPorPagarGirado</c> v1 (<c>PCG</c>; FR-125, FR-127): el giro de una orden convertida desde una cuenta por pagar contable (pago total o parcial):
/// <c>CuentaPorPagarContable</c> (el tercero y el documento cruce de la cuenta por pagar) → <c>Banco</c>. Tesorería no registra facturas ni causa gastos: paga lo causado.
/// En línea, comprobante sugerido <c>TP</c>. (nuevo)
/// </summary>
public sealed record PagoDeCuentaPorPagarGiradoV1
{
    public const string Type = "PagoDeCuentaPorPagarGirado";

    public Guid PaymentOrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public Guid PayableDocumentPublicId { get; init; }

    public string CrossDocument { get; init; } = string.Empty;

    public Guid ThirdPartyPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly DebitDate { get; init; }

    public string? TreasuryReference { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>ChequeNoCobradoPagado</c> v1 (<c>CTP</c>; FR-220; pedido del dueño para T2, decisión pendiente de confirmar): el cheque que el cierre del mes pasó a cuenta por pagar
/// y el banco paga después. El paso a cuenta por pagar devolvió el dinero al banco en libros (<c>ChequeNoCobradoACuentaPorPagar</c>); el cobro tardío lo saca otra vez:
/// <c>CuentaPorPagarChequesNoCobrados</c> (el beneficiario) → <c>Banco</c>, con la fecha del cobro. En línea, comprobante sugerido <c>TB</c>. (nuevo)
/// </summary>
public sealed record ChequeNoCobradoPagadoV1
{
    public const string Type = "ChequeNoCobradoPagado";

    public Guid IssuedCheckPublicId { get; init; }

    public long CheckNumber { get; init; }

    public Guid BankAccountPublicId { get; init; }

    public Guid? BeneficiaryPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly CashedOn { get; init; }

    public DateOnly MovedToPayableOn { get; init; }

    public Guid? StatementLinePublicId { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>PagoDeFacturaDeProveedor</c> v1 (<c>TES → Inventory</c>; E-03; FR-124, FR-166): Tesorería le informa a Comercial que pagó (todo o parte de) una factura de proveedor
/// que le llegó por <c>FacturaDeProveedorPorPagar</c>. Origen la orden, clave <c>Confirmation:{orden:N}</c>, modo <c>Always</c>. (nuevo)
/// </summary>
public sealed record PagoDeFacturaDeProveedorV1
{
    public const string Type = "PagoDeFacturaDeProveedor";

    public Guid InvoicePublicId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public Guid PaymentOrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateOnly PaidOn { get; init; }

    public string? TreasuryReference { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();
}
