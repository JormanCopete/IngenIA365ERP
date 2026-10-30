using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Receipts.Operations;

/// <summary>
/// <c>RCD_CommercialReceivableApplications</c> (H; feature 015, R1, T0397; FR-049; data-model §8.1): el detalle de una aplicación <c>CommercialReceivable</c> del recibo: la
/// factura (documento cruce), el saldo pendiente que Contabilidad informó al cobrar, el tercero y el valor. Lo vendido a crédito en Comercial que no pasa a Cartera; antes de
/// activar Cartera, este recibo es la referencia que Cartera incorpora. Una por aplicación (UK). (nuevo)
/// </summary>
public class CommercialReceivableApplication : AuditableEntity, IHechoInmutable
{
    public long ReceiptId { get; set; }

    public long ReceiptApplicationId { get; set; }

    public int PersonId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>El comprobante contable de la venta (el primero que cargó la factura), por su <c>PublicId</c>.</summary>
    public Guid? AccountingDocumentPublicId { get; set; }

    public decimal ConsultedBalance { get; set; }

    public decimal Amount { get; set; }
}
