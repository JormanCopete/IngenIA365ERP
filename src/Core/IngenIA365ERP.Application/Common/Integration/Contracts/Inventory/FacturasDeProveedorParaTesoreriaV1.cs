using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>FacturaDeProveedorPorPagar</c> v1 (<c>INV → Treasury</c>; feature 015, T2, fase 42, T0454; E-03; FR-124, FR-166): la factura de proveedor a crédito que Comercial
/// confirmó y queda por pagar: Tesorería la recibe como orden de pago del proveedor con su vencimiento y su neto (lo que se le debe después de retenciones). Origen la
/// factura, clave <c>Confirmation</c>, modo <c>Always</c> (no pasa a Contabilidad: la factura ya tiene su hecho). (nuevo)
/// </summary>
public sealed record FacturaDeProveedorPorPagarV1
{
    public const string Type = "FacturaDeProveedorPorPagar";

    public Guid InvoicePublicId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public string SupplierDocumentNumber { get; init; } = string.Empty;

    public Guid SupplierPersonPublicId { get; init; }

    public DateOnly IssueDate { get; init; }

    public DateOnly DueDate { get; init; }

    public decimal Total { get; init; }

    public decimal WithholdingTotal { get; init; }

    /// <summary>Lo que se le paga al proveedor: el total menos las retenciones.</summary>
    public decimal AmountDue { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();
}

/// <summary>
/// <c>AnulacionDeFacturaDeProveedor</c> v1 (<c>INV → Treasury</c>; E-03; FR-124): Comercial anuló el registro de la factura; Tesorería saca su orden de un lote no enviado
/// (o la anula si sigue en la bandeja). Origen la anulación, clave <c>Confirmation</c>. (nuevo)
/// </summary>
public sealed record AnulacionDeFacturaDeProveedorV1
{
    public const string Type = "AnulacionDeFacturaDeProveedor";

    public Guid InvoicePublicId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public string? Reason { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();
}

/// <summary>Los mensajes de Comercial a Tesorería (feature 015, T2, fase 42; E-03). Son de <c>INV</c>, a un solo destino y los origina la persona. (nuevo)</summary>
public static class CatalogoDeMensajesDeComercialParaTesoreriaV1
{
    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
    [
        new(typeof(FacturaDeProveedorPorPagarV1), FacturaDeProveedorPorPagarV1.Type, 1, ModulosDeOrigen.Inventario, TipoDeMensaje.Un(IntegrationDestinations.Treasury),
            IntegrationMessageKind.Business, FormaDeClaveDeEvento.Confirmacion, AdmiteProceso: false),
        new(typeof(AnulacionDeFacturaDeProveedorV1), AnulacionDeFacturaDeProveedorV1.Type, 1, ModulosDeOrigen.Inventario, TipoDeMensaje.Un(IntegrationDestinations.Treasury),
            IntegrationMessageKind.Business, FormaDeClaveDeEvento.Confirmacion, AdmiteProceso: false),
    ];
}
