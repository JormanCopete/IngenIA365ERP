using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Sales.CicloComercial;

/// <summary>
/// Los errores del ciclo comercial de I6 (feature 012, T877–T890; contracts/api.md §18.4, §13.4; data-model §14): cotización, pedido,
/// remisión, factura desde pedido o remisiones y nota débito. El mismo código en el borrador (como aviso) y en la confirmación. (nuevo)
/// </summary>
public static class ErroresDelCicloComercial
{
    public const string QuoteExpiredCode = "Inventory.Quote.Expired";
    public const string QuoteValidUntilInvalidCode = "Inventory.Quote.ValidUntilInvalid";
    public const string ShipmentAlreadyInvoicedCode = "Inventory.Shipment.AlreadyInvoiced";
    public const string ShipmentCustomerMismatchCode = "Inventory.Shipment.CustomerMismatch";
    public const string OrderExceedsPendingCode = "Inventory.Order.ExceedsPending";
    public const string OriginInvalidCode = "Inventory.Sales.OriginInvalid";
    public const string DebitNoteOriginInvalidCode = "Inventory.DebitNote.OriginInvalid";
    public const string ChainMismatchCode = "Inventory.PostingMode.ChainMismatch";

    /// <summary>La cotización venció: ya no se convierte en pedido (FR-052). (nuevo)</summary>
    public static Error QuoteExpired(DateOnly validUntil) => new ErrorConDatos(QuoteExpiredCode,
        $"La cotización venció el {validUntil:dd/MM/yyyy}: haga una nueva para convertirla en pedido.", new { validUntil });

    /// <summary>La vigencia de la cotización es anterior a su fecha (FR-052). (nuevo)</summary>
    public static Error QuoteValidUntilInvalid(DateOnly validUntil, DateOnly operationDate) => new ErrorConDatos(QuoteValidUntilInvalidCode,
        $"La cotización vence el {validUntil:dd/MM/yyyy}, antes de su fecha ({operationDate:dd/MM/yyyy}).", new { validUntil, operationDate });

    /// <summary>Lo que queda por facturar de una línea de remisión (<c>data.lines[]</c>).</summary>
    public sealed record PendienteDeRemision(Guid ShipmentLinePublicId, decimal Pending, decimal Requested);

    /// <summary>La factura pide más de lo que queda por facturar de alguna remisión (FR-052). (nuevo)</summary>
    public static Error ShipmentAlreadyInvoiced(IReadOnlyList<PendienteDeRemision> lineas) => new ErrorConDatos(ShipmentAlreadyInvoicedCode,
        "La factura pide más de lo que queda por facturar en las remisiones: ya se facturó en otra factura.", new { lines = lineas });

    /// <summary>Las remisiones de una factura son de clientes distintos. (nuevo)</summary>
    public static Error ShipmentCustomerMismatch() => new(ShipmentCustomerMismatchCode,
        "Una factura desde remisiones reúne sólo remisiones confirmadas del mismo cliente.");

    /// <summary>Lo que queda por despachar o facturar de una línea de pedido (<c>data.lines[]</c>).</summary>
    public sealed record PendienteDePedido(Guid OrderLinePublicId, decimal Pending, decimal Requested);

    /// <summary>La salida pide más de lo que queda del pedido (FR-052). (nuevo)</summary>
    public static Error OrderExceedsPending(IReadOnlyList<PendienteDePedido> lineas) => new ErrorConDatos(OrderExceedsPendingCode,
        "La salida pide más de lo que queda por despachar o facturar del pedido.", new { lines = lineas });

    /// <summary>El origen no corresponde a la clase del documento (cotización → pedido; pedido → remisión o factura; remisión → factura). (nuevo)</summary>
    public static Error OriginInvalid(DocumentClass clase, DocumentClass claseDelOrigen) => new ErrorConDatos(OriginInvalidCode,
        $"Un documento {clase} no nace de un {claseDelOrigen}, o el origen no está confirmado.",
        new { @class = clase.ToString(), originClass = claseDelOrigen.ToString() });

    /// <summary>La nota débito sólo va sobre una factura electrónica confirmada (§18.4). (nuevo)</summary>
    public static Error DebitNoteOriginInvalid() => new(DebitNoteOriginInvalidCode,
        "La nota débito se hace sobre una factura de venta confirmada (factura o factura desde remisiones).");

    /// <summary>
    /// Las remisiones de una factura tienen modos de paso distintos: un derivado no reúne orígenes con destinos distintos (FR-075, T9). (nuevo)
    /// </summary>
    public static Error ShipmentsChainMismatch(IReadOnlyList<string> remisiones) => new ErrorConDatos(ChainMismatchCode,
        $"Las remisiones {string.Join(", ", remisiones)} pasan a contabilidad de forma distinta: facture por separado las de cada forma.",
        new { chain = PostingChain.Sales.ToString(), shipments = remisiones });
}
