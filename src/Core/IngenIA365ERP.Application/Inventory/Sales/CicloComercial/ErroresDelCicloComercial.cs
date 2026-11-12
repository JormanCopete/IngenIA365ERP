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

    /// <summary>2026-11-12: la nota débito en papel sólo va sobre un comprobante de venta no electrónico confirmado. (nuevo)</summary>
    public static Error NonElectronicDebitNoteOriginInvalid() => new(DebitNoteOriginInvalidCode,
        "La nota débito no electrónica se hace sobre un comprobante de venta no electrónico confirmado.");

    // ------------------------------------------------------------------------------- la cotización como base (2026-11-12) --

    public const string QuoteCustomerRequiredCode = "Inventory.Quote.CustomerRequired";
    public const string QuoteCustomerMismatchCode = "Inventory.Quote.CustomerMismatch";
    public const string QuoteSaleClassInvalidCode = "Inventory.Quote.SaleClassInvalid";
    public const string QuoteCustomerEmailMissingCode = "Inventory.Quote.CustomerEmailMissing";
    public const string QuoteNotConfirmedCode = "Inventory.Quote.NotConfirmed";

    /// <summary>La cotización se imprime para un cliente: confirmarla exige uno identificado (no el consumidor final). (nuevo)</summary>
    public static Error QuoteCustomerRequired() => new(QuoteCustomerRequiredCode,
        "La cotización es para un cliente: elíjalo (o créelo) antes de confirmarla.");

    /// <summary>La venta ya es de otro cliente identificado: la cotización no se carga en ella. (nuevo)</summary>
    public static Error QuoteCustomerMismatch(string clienteDeLaVenta) => new ErrorConDatos(QuoteCustomerMismatchCode,
        $"La venta es de {clienteDeLaVenta}, otro cliente: cargue la cotización en una venta nueva o del mismo cliente.", new { customer = clienteDeLaVenta });

    /// <summary>La cotización se carga en una factura o un comprobante de venta en borrador (no en una factura desde remisiones). (nuevo)</summary>
    public static Error QuoteSaleClassInvalid(DocumentClass clase) => new ErrorConDatos(QuoteSaleClassInvalidCode,
        "La cotización se carga en una factura de venta, un comprobante o una venta del punto de venta en borrador.", new { @class = clase.ToString() });

    /// <summary>Sólo una cotización confirmada se imprime para el cliente, se envía o se carga en una venta. (nuevo)</summary>
    public static Error QuoteNotConfirmed(DocumentStatus estado) => new ErrorConDatos(QuoteNotConfirmedCode,
        "Sólo una cotización confirmada se envía al cliente o se carga en una venta.", new { status = estado.ToString() });

    /// <summary>Enviar la cotización sin correo: el cliente no tiene uno y no se indicó otro. (nuevo)</summary>
    public static Error QuoteCustomerEmailMissing(Guid persona) => new ErrorConDatos(QuoteCustomerEmailMissingCode,
        "El cliente no tiene correo registrado: escríbalo para enviarle la cotización.", new { personPublicId = persona });

    /// <summary>
    /// Las remisiones de una factura tienen modos de paso distintos: un derivado no reúne orígenes con destinos distintos (FR-075, T9). (nuevo)
    /// </summary>
    public static Error ShipmentsChainMismatch(IReadOnlyList<string> remisiones) => new ErrorConDatos(ChainMismatchCode,
        $"Las remisiones {string.Join(", ", remisiones)} pasan a contabilidad de forma distinta: facture por separado las de cada forma.",
        new { chain = PostingChain.Sales.ToString(), shipments = remisiones });
}
