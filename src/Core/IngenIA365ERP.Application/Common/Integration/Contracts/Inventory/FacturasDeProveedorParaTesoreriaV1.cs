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

/// <summary>Las clases de nota del proveedor que ajustan lo que se le debe (<see cref="NotaDeProveedorPorPagarV1.Kind"/>). (nuevo)</summary>
public static class ClasesDeNotaPorPagar
{
    /// <summary>Disminuye lo que se le debe al proveedor (nota crédito del proveedor o nota de ajuste del documento soporte).</summary>
    public const string Credito = "CreditNote";

    /// <summary>Aumenta lo que se le debe al proveedor (nota débito del proveedor).</summary>
    public const string Debito = "DebitNote";
}

/// <summary>
/// <c>NotaDeProveedorPorPagar</c> v1 (<c>INV → Treasury</c>; revisión comercial del 2026-11-12): Comercial confirmó una nota del proveedor (o la nota de
/// ajuste de un documento soporte) sobre una factura que quedó por pagar en Tesorería. La nota <b>crédito</b> disminuye la orden de la factura; la
/// <b>débito</b> nace como una orden propia del proveedor, enlazada a la factura. Origen la nota, clave <c>Confirmation</c>. Valores positivos: la clase
/// dice el sentido. (nuevo)
/// </summary>
public sealed record NotaDeProveedorPorPagarV1
{
    public const string Type = "NotaDeProveedorPorPagar";

    public Guid NotePublicId { get; init; }

    public string NoteNumber { get; init; } = string.Empty;

    /// <summary><see cref="ClasesDeNotaPorPagar.Credito"/> o <see cref="ClasesDeNotaPorPagar.Debito"/>.</summary>
    public string Kind { get; init; } = ClasesDeNotaPorPagar.Credito;

    /// <summary>La factura (o el documento soporte) que corrige: la que dejó su orden en Tesorería.</summary>
    public Guid InvoicePublicId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    /// <summary>El número de la nota del proveedor (o el propio, en la nota de ajuste del soporte).</summary>
    public string SupplierDocumentNumber { get; init; } = string.Empty;

    public Guid SupplierPersonPublicId { get; init; }

    public DateOnly IssueDate { get; init; }

    /// <summary>La fecha de pago de una nota débito (la de la factura si la nota no trae).</summary>
    public DateOnly DueDate { get; init; }

    public decimal Total { get; init; }

    public decimal WithholdingTotal { get; init; }

    /// <summary>Lo que cambia la deuda con el proveedor: el total de la nota menos sus retenciones.</summary>
    public decimal AmountDue { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();
}

/// <summary>
/// <c>AnulacionDeNotaDeProveedor</c> v1 (<c>INV → Treasury</c>; revisión comercial del 2026-11-12): Comercial anuló una nota que había ajustado lo que se le
/// debe al proveedor; Tesorería revierte exactamente lo que la nota hizo (si la aplicó). Origen la anulación, clave <c>Confirmation</c>. (nuevo)
/// </summary>
public sealed record AnulacionDeNotaDeProveedorV1
{
    public const string Type = "AnulacionDeNotaDeProveedor";

    public Guid NotePublicId { get; init; }

    public string NoteNumber { get; init; } = string.Empty;

    public string Kind { get; init; } = ClasesDeNotaPorPagar.Credito;

    public Guid InvoicePublicId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public decimal AmountDue { get; init; }

    public string? Reason { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();
}

/// <summary>
/// Los mensajes de Comercial a Tesorería (feature 015, T2, fase 42; E-03; revisión comercial del 2026-11-12: las notas). Son de <c>INV</c>, a un solo
/// destino y los origina la persona. (nuevo)
/// </summary>
public static class CatalogoDeMensajesDeComercialParaTesoreriaV1
{
    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
    [
        new(typeof(FacturaDeProveedorPorPagarV1), FacturaDeProveedorPorPagarV1.Type, 1, ModulosDeOrigen.Inventario, TipoDeMensaje.Un(IntegrationDestinations.Treasury),
            IntegrationMessageKind.Business, FormaDeClaveDeEvento.Confirmacion, AdmiteProceso: false),
        new(typeof(AnulacionDeFacturaDeProveedorV1), AnulacionDeFacturaDeProveedorV1.Type, 1, ModulosDeOrigen.Inventario, TipoDeMensaje.Un(IntegrationDestinations.Treasury),
            IntegrationMessageKind.Business, FormaDeClaveDeEvento.Confirmacion, AdmiteProceso: false),
        new(typeof(NotaDeProveedorPorPagarV1), NotaDeProveedorPorPagarV1.Type, 1, ModulosDeOrigen.Inventario, TipoDeMensaje.Un(IntegrationDestinations.Treasury),
            IntegrationMessageKind.Business, FormaDeClaveDeEvento.Confirmacion, AdmiteProceso: false),
        new(typeof(AnulacionDeNotaDeProveedorV1), AnulacionDeNotaDeProveedorV1.Type, 1, ModulosDeOrigen.Inventario, TipoDeMensaje.Un(IntegrationDestinations.Treasury),
            IntegrationMessageKind.Business, FormaDeClaveDeEvento.Confirmacion, AdmiteProceso: false),
    ];
}
