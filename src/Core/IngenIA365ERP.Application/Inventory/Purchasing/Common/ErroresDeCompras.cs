using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Purchasing;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>
/// Los códigos de error de compras (feature 012, US9; contracts/api.md §14.10; decisiones-transversales §2.17), con el mensaje
/// en español y el <c>data</c> que la persona necesita para corregir. Todos responden 422. (nuevo)
/// </summary>
public static class ErroresDeCompras
{
    // ----------------------------------------------------------------------------------- lo común (§14.1) --

    public static Error MunicipalityUnknown(string codigo) => new ErrorConDatos("Inventory.Purchase.MunicipalityUnknown",
        $"El municipio {codigo} no está en las ciudades (código DIVIPOLA). Revise el código o cárguelo en Maestros › Ciudades.",
        new { municipalityDaneCode = codigo });

    /// <summary>Una recepción con su número y el modo de paso que selló.</summary>
    public sealed record RecepcionConModo(Guid PublicId, string? DisplayNumber, string? Mode);

    public static Error MixedPostingDestinations(IReadOnlyList<RecepcionConModo> recepciones) => new ErrorConDatos(
        "Inventory.Purchase.MixedPostingDestinations",
        "Las recepciones van a Contabilidad por caminos distintos (en línea, por lotes o no pasa): regístrelas en documentos separados.",
        new { receipts = recepciones.Select(r => new { publicId = r.PublicId, displayNumber = r.DisplayNumber, mode = r.Mode }).ToList() });

    public static Error RateNotInForce(int lineNumber, string taxCode, DateOnly fecha) => new ErrorConDatos("Inventory.ProductTax.RateNotInForce",
        $"Línea {lineNumber}: el impuesto {taxCode} del producto no tiene tarifa vigente al {fecha:dd/MM/yyyy} para compras. Revise el catálogo tributario.",
        new { lineNumber, taxCode, date = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });

    /// <summary>Un rechazo del motor tributario (<c>Core.TaxRate.Ambiguous</c>…) con su mismo código.</summary>
    public static Error DelMotor(string codigo, string mensaje) => new(codigo, mensaje);

    // ------------------------------------------------------------------------------- factura (§14.4) --

    public static Error ReceiptFromOtherSupplier(int lineNumber, Guid receiptPublicId, string? displayNumber) => new ErrorConDatos(
        "Inventory.Purchase.ReceiptFromOtherSupplier",
        $"Línea {lineNumber}: la recepción {displayNumber} es de otro proveedor.",
        new { lineNumber, receiptPublicId, displayNumber });

    public static Error ReceiptNotConfirmed(int lineNumber, Guid receiptPublicId, string? displayNumber) => new ErrorConDatos(
        "Inventory.Purchase.ReceiptNotConfirmed",
        $"Línea {lineNumber}: la recepción {displayNumber ?? "(borrador)"} no está confirmada.",
        new { lineNumber, receiptPublicId, displayNumber });

    public static Error InvoiceExceedsReceived(int lineNumber, decimal received, decimal invoiced, decimal returned, decimal available) => new ErrorConDatos(
        "Inventory.Purchase.InvoiceExceedsReceived",
        $"Línea {lineNumber}: se factura más de lo recibido (recibido {received}, ya facturado {invoiced}, devuelto {returned}; queda {available}).",
        new { lineNumber, received, invoiced, returned, available });

    /// <summary>Una línea de mercancía sin recepción: sólo los servicios se facturan sin recibir. (nuevo)</summary>
    public static Error GoodsWithoutReceipt(int lineNumber, string productCode) => new ErrorConDatos("Inventory.Purchase.GoodsWithoutReceipt",
        $"Línea {lineNumber}: {productCode} es mercancía; se factura contra la línea de su recepción. Sin recepción sólo van servicios.",
        new { lineNumber, productCode });

    public static Error Duplicate(Guid documentPublicId, string? displayNumber, string supplierNumber) => new ErrorConDatos(
        "Inventory.SupplierInvoice.Duplicate",
        $"La factura {supplierNumber} de este proveedor ya está registrada en {displayNumber ?? "un borrador"}.",
        new { documentPublicId, displayNumber });

    public static Error CufeDuplicate(Guid documentPublicId, string? displayNumber) => new ErrorConDatos("Inventory.SupplierInvoice.CufeDuplicate",
        $"Ese CUFE ya está registrado en {displayNumber ?? "un borrador"}.",
        new { documentPublicId, displayNumber });

    public static Error CufeRequired() => new("Inventory.SupplierInvoice.CufeRequired",
        "Una factura electrónica lleva su CUFE (96 caracteres hexadecimales).");

    public static Error CufeInvalid() => new("Validation.Invalid",
        "El CUFE son 96 caracteres hexadecimales.");

    public static Error DueDateInvalid(DateOnly issueDate) => new ErrorConDatos("Inventory.SupplierInvoice.DueDateInvalid",
        $"El vencimiento no puede ser anterior a la emisión ({issueDate:dd/MM/yyyy}); a crédito es obligatorio.",
        new { issueDate = issueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });

    public static Error IssueDateInFuture(DateOnly hoy) => new ErrorConDatos("Inventory.SupplierInvoice.IssueDateInvalid",
        $"La fecha de emisión no puede ser posterior a hoy ({hoy:dd/MM/yyyy}).",
        new { today = hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });

    public static Error FileUnreadable(string motivo) => new ErrorConDatos("Inventory.SupplierInvoice.FileUnreadable",
        "No se pudo leer el archivo: suba el XML de la factura electrónica (UBL 2.1), su AttachedDocument o el ZIP que lo trae.",
        new { reason = motivo });

    public static Error NotAnInvoice(string documento) => new ErrorConDatos("Inventory.SupplierInvoice.NotAnInvoice",
        $"El archivo es un {documento}, no una factura: registre las notas en Notas de proveedor.",
        new { document = documento });

    // ---------------------------------------------------------------------------------- notas (§14.5) --

    public static Error NoteExceedsInvoice(decimal remaining) => new ErrorConDatos("Inventory.SupplierNote.ExceedsInvoice",
        $"La nota crédito pasa lo que queda de la factura ({remaining:N2}).",
        new { remaining });

    /// <summary>La nota se registra contra una factura del mismo proveedor. (nuevo)</summary>
    public static Error NoteInvoiceFromOtherSupplier(Guid invoicePublicId, string? displayNumber) => new ErrorConDatos(
        "Inventory.SupplierNote.InvoiceFromOtherSupplier",
        $"La factura {displayNumber} es de otro proveedor.",
        new { invoicePublicId, displayNumber });

    /// <summary>La nota se registra contra una factura confirmada. (nuevo)</summary>
    public static Error NoteInvoiceNotConfirmed(Guid invoicePublicId, string? displayNumber) => new ErrorConDatos(
        "Inventory.SupplierNote.InvoiceNotConfirmed",
        $"La factura {displayNumber ?? "(borrador)"} no está confirmada.",
        new { invoicePublicId, displayNumber });

    /// <summary>Cada línea de la nota va contra una línea de su factura. (nuevo)</summary>
    public static Error NoteLineRequired(int lineNumber) => new ErrorConDatos("Inventory.SupplierNote.InvoiceLineRequired",
        $"Línea {lineNumber}: indique la línea de la factura que corrige la nota.",
        new { lineNumber });

    // ----------------------------------------------------------------------------- devolución (§14.6) --

    public static Error ReturnReceiptLineRequired(int lineNumber) => new ErrorConDatos("Inventory.Return.ReceiptLineRequired",
        $"Línea {lineNumber}: toda línea de la devolución va enlazada a la línea de su recepción.",
        new { lineNumber });

    public static Error ReturnExceedsReceived(int lineNumber, decimal received, decimal alreadyReturned) => new ErrorConDatos(
        "Inventory.Return.ExceedsReceived",
        $"Línea {lineNumber}: se devuelve más de lo recibido (recibido {received}, ya devuelto {alreadyReturned}).",
        new { lineNumber, received, alreadyReturned });

    // ------------------------------------------------------------------------------------ RADIAN (§14.8) --

    public static Error Radian(string codigo) => codigo switch
    {
        TransicionesDeEventoRadian.CodigoNoAplica => new Error(codigo, "Los eventos RADIAN sólo aplican a una factura a crédito pendiente de ellos."),
        TransicionesDeEventoRadian.CodigoFueraDeOrden => new Error(codigo, "El recibo del bien (032) exige primero el acuse de recibo (030)."),
        TransicionesDeEventoRadian.CodigoFechaInvalida => new Error(codigo, "La fecha del evento va entre la emisión de la factura y hoy."),
        TransicionesDeEventoRadian.CodigoYaRegistrado => new Error(codigo, "Ese evento ya está registrado o emitido; para cambiar un registro externo, corríjalo."),
        _ => new Error(codigo, "El evento RADIAN no se puede registrar."),
    };

    /// <summary>La fuente de un registro externo es el portal de la DIAN o el del proveedor.</summary>
    public static Error RadianSourceInvalid() => new("Validation.Invalid",
        "La fuente de un evento registrado por fuera es DianPortal o SupplierPortal.");

    // ------------------------------------------------------------------- solicitudes y órdenes (I5, §14.9) --

    /// <summary>Una recepción contra la orden de otro proveedor (T789). (nuevo)</summary>
    public static Error OrderFromOtherSupplier(int lineNumber, Guid orderPublicId, string? displayNumber) => new ErrorConDatos(
        "Inventory.Purchase.OrderFromOtherSupplier",
        $"Línea {lineNumber}: la orden {displayNumber} es de otro proveedor.",
        new { lineNumber, orderPublicId, displayNumber });

    /// <summary>
    /// La orden no admite recepciones ni cerrar su saldo: no está confirmada, está anulada o su saldo ya se cerró (T789, T792; data-model
    /// §9.8). (nuevo)
    /// </summary>
    public static Error OrderNotOpen(Guid orderPublicId, string? displayNumber, DocumentStatus status, DateTime? balanceClosedAt, int? lineNumber = null) =>
        new ErrorConDatos("Inventory.PurchaseOrder.NotOpen",
            balanceClosedAt is not null
                ? $"{(lineNumber is { } n ? $"Línea {n}: l" : "L")}a orden {displayNumber} ya tiene el saldo cerrado: no admite más recepciones."
                : $"{(lineNumber is { } m ? $"Línea {m}: l" : "L")}a orden {displayNumber ?? "(sin número)"} no está abierta (está {InventoryErrors.Estado(status)}).",
            new { lineNumber, orderPublicId, displayNumber, status = status.ToString(), balanceClosedAt });

    /// <summary>Lo recibido contra una línea de orden pasa lo ordenado más la tolerancia de cantidad vigente (FR-049; T789). (nuevo)</summary>
    public static Error OverReceiptBeyondTolerance(int lineNumber, decimal ordered, decimal received, decimal tolerance) => new ErrorConDatos(
        CruceDeCompra.CodigoRecibidoDeMas,
        $"Línea {lineNumber}: se recibe más de lo ordenado fuera de la tolerancia (ordenado {ordered}, recibido con ésta {received}, tolerancia {tolerance}).",
        new { lineNumber, ordered, received, tolerance });

    /// <summary>Enviar al proveedor una orden que no está confirmada (T791). (nuevo)</summary>
    public static Error OrderNotConfirmed(DocumentStatus status) => new ErrorConDatos("Inventory.PurchaseOrder.NotConfirmed",
        $"Sólo se envía al proveedor una orden confirmada; ésta está {InventoryErrors.Estado(status)}.",
        new { status = status.ToString() });

    /// <summary>El proveedor no tiene correo en el maestro de personas y la petición no trae uno (T791). (nuevo)</summary>
    public static Error SupplierEmailMissing(Guid supplierPersonPublicId) => new ErrorConDatos("Inventory.PurchaseOrder.SupplierEmailMissing",
        "El proveedor no tiene correo registrado: escríbalo en el envío o regístrelo en Personas.",
        new { supplierPersonPublicId });

    /// <summary>Una orden desde una solicitud que no está confirmada (T787). (nuevo)</summary>
    public static Error RequestNotConfirmed(int lineNumber, Guid requestPublicId, string? displayNumber, DocumentStatus status) => new ErrorConDatos(
        "Inventory.PurchaseRequest.NotConfirmed",
        $"Línea {lineNumber}: la solicitud {displayNumber ?? "(sin número)"} no está aprobada (está {InventoryErrors.Estado(status)}).",
        new { lineNumber, requestPublicId, displayNumber, status = status.ToString() });

    /// <summary>La línea de origen no es de la clase que corresponde (recepción ← orden, orden ← solicitud). (nuevo)</summary>
    public static Error OrigenDeOtraClase(int lineNumber, string esperado) => new ErrorConDatos("Validation.Invalid",
        $"Línea {lineNumber}: la línea de origen no es de {esperado}.",
        new { lineNumber });

    /// <summary>Una línea de la orden sin el precio pactado con el proveedor (T787). (nuevo)</summary>
    public static Error OrderPriceRequired(int lineNumber) => new ErrorConDatos("Validation.Invalid",
        $"Línea {lineNumber}: la orden lleva el precio pactado con el proveedor.",
        new { lineNumber, field = "lines.unitPrice" });

    /// <summary>En la orden las condiciones de pago se guardan en sus notas (data-model §9.8): van en uno de los dos campos. (nuevo)</summary>
    public static Error PaymentTermsAndNotes() => new ErrorConDatos("Validation.Invalid",
        "En la orden las condiciones de pago son sus notas: envíelas en paymentTerms o en notes, no en los dos.",
        new { field = "paymentTerms" });

    /// <summary>Los campos que la clase no admite (§9.3, §14.1).</summary>
    public static Error CampoNoAdmitido(string campo, DocumentClass clase) => new ErrorConDatos("Validation.Invalid",
        $"El campo {campo} no aplica a un documento de clase {clase}.",
        new { field = campo, @class = clase.ToString() });
}
