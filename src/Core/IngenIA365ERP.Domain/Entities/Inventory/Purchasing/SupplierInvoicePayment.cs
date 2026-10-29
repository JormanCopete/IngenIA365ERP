using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Purchasing;

/// <summary>
/// <c>INV_SupplierInvoicePayments</c> (feature 015, T2, fase 42, T0454; E-03 a la 012; FR-124, FR-166): el pago de una factura de proveedor que Tesorería le informó a
/// Comercial (<c>PagoDeFacturaDeProveedor</c> v1). Comercial sabe así cuánto de la factura se pagó y cuándo, sin leer tablas de Tesorería; la orden va por su
/// <c>PublicId</c>. El mensaje es único: repetirlo no duplica el pago. (nuevo; nace en el par <c>TesoreriaAmpliada</c>, como la columna de la remesa de T1)
/// </summary>
public class SupplierInvoicePayment : AuditableEntity, IHechoInmutable
{
    public int DocumentId { get; set; }

    public Guid MessagePublicId { get; set; }

    public Guid PaymentOrderPublicId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly PaidOn { get; set; }

    public string? TreasuryReference { get; set; }

    public DateTime ReceivedAt { get; set; }
}
