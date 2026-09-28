using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>SupportDocument</c> (feature 012, I4, T740; data-model §9.5; api.md §14.7; FR-051, FR-063): el documento soporte de
/// una compra a quien no está obligado a facturar, sobre el modelo de la factura del proveedor (<see cref="EfectoFacturaDeProveedor"/>) —mismas
/// reglas de impuestos y retenciones, mismo cruce con sus recepciones (<c>InvoiceOfReceipt</c>) o sin recepción si es un servicio, misma
/// diferencia de precio— con tres diferencias:
/// <list type="bullet">
/// <item>la contraparte no está obligada a facturar (<c>COR_People.IsObligatedToInvoice = false</c>;
/// <c>Inventory.SupportDocument.SupplierObligated</c> si lo está: le corresponde una factura del proveedor);</item>
/// <item>no hay documento del proveedor: lo numera la cooperativa con su resolución (<c>NumeradorFiscal</c>) y el CUDS lo da la emisión, así que
/// <c>FacturaProveedorRegistrada</c> lleva <c>kind = SupportDocument</c> con el número propio;</item>
/// <item>no nacen eventos RADIAN (los emite la cooperativa sobre las facturas que recibe, no sobre las que emite).</item>
/// </list>
/// Un documento soporte validado no se anula: se corrige con su nota de ajuste (<see cref="EfectoDeNotaDeAjusteDeDocumentoSoporte"/>). (nuevo)
/// </summary>
public sealed class EfectoDeDocumentoSoporte(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    CalculoTributarioDeCompra calculo,
    VinculosDeCompra vinculos,
    DiferenciasDePrecioDeCompra diferencias,
    IApplicationDbContext db) : EfectoFacturaDeProveedor(registro, emision, maestros, calculo, vinculos, diferencias, db)
{
    public const string SupplierObligatedCode = "Inventory.SupportDocument.SupplierObligated";

    public override DocumentClass Clase => DocumentClass.SupportDocument;

    protected override bool RegistraEventosRadian => false;

    protected override string TipoDelMensaje => "SupportDocument";

    protected override Task<Error?> ReglasDelDocumentoAsync(InventoryDocument documento, CancellationToken ct) =>
        DocumentoSoporte.ContraparteNoObligadaAsync(BaseDeDatos, documento, ct);

    protected override Task<SupplierInvoiceDetail?> DetalleDelMensajeAsync(InventoryDocument documento, CancellationToken ct) =>
        Task.FromResult<SupplierInvoiceDetail?>(DocumentoSoporte.DetalleDelMensaje(documento));
}

/// <summary>
/// La estrategia de <c>SupportDocumentAdjustmentNote</c> (feature 012, I4, T740; api.md §14.7): la nota de ajuste del documento soporte, sobre
/// el modelo de la nota del proveedor (<see cref="EfectoNotaDeProveedor"/>): contra un documento soporte confirmado de la misma contraparte
/// (<c>NoteOf</c>, líneas enlazadas), impuestos con su foto en proporción, sin pasar de lo que queda y con la diferencia de precio sobre lo
/// recibido; emite <c>FacturaProveedorRegistrada</c> con signo negativo (<c>kind = SupportDocumentAdjustmentNote</c>) y, si cambia el precio de
/// lo recibido, <c>AjusteDeCostoReconocido</c>. La numera su consecutivo (<c>INV_DocumentSequences</c>), sale por el canal vigente y lleva la
/// referencia al CUDS del documento soporte (el canónico). (nuevo)
/// </summary>
public sealed class EfectoDeNotaDeAjusteDeDocumentoSoporte(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    CalculoTributarioDeCompra calculo,
    VinculosDeCompra vinculos,
    DiferenciasDePrecioDeCompra diferencias,
    IApplicationDbContext db) : EfectoNotaDeProveedor(registro, emision, maestros, calculo, vinculos, diferencias, db)
{
    public override DocumentClass Clase => DocumentClass.SupportDocumentAdjustmentNote;

    protected override DocumentClass ClaseCorregida => DocumentClass.SupportDocument;

    protected override async Task<Result<SupplierInvoiceDetail>> DetalleDeLaNotaAsync(InventoryDocument documento, CancellationToken ct)
    {
        var contraparte = await DocumentoSoporte.ContraparteNoObligadaAsync(BaseDeDatos, documento, ct);
        return contraparte is not null ? Result.Failure<SupplierInvoiceDetail>(contraparte) : Result.Success(DocumentoSoporte.DetalleDelMensaje(documento));
    }

    protected override Task<SupplierInvoiceDetail?> DetalleDelMensajeAsync(InventoryDocument documento, CancellationToken ct) =>
        Task.FromResult<SupplierInvoiceDetail?>(DocumentoSoporte.DetalleDelMensaje(documento));

    protected override string TipoDelMensaje(SupplierInvoiceDetail detalle) => "SupportDocumentAdjustmentNote";
}

/// <summary>Lo que comparten el documento soporte y su nota de ajuste (T740). (nuevo)</summary>
public static class DocumentoSoporte
{
    /// <summary>La contraparte existe y no está obligada a facturar; si lo está → <c>Inventory.SupportDocument.SupplierObligated</c>.</summary>
    public static async Task<Error?> ContraparteNoObligadaAsync(IApplicationDbContext db, InventoryDocument documento, CancellationToken ct)
    {
        if (documento.CounterpartyPersonId is not int persona) return InventoryErrors.FieldRequired(ReglasDelDocumento.CampoContraparte);
        var obligada = await db.People.AsNoTracking().Where(p => p.Id == persona).Select(p => (bool?)p.IsObligatedToInvoice).FirstOrDefaultAsync(ct);
        if (obligada is null) return ErroresDelDocumento.PersonaInexistente();
        return obligada.Value
            ? new Error(EfectoDeDocumentoSoporte.SupplierObligatedCode,
                "El proveedor está obligado a facturar: la compra se soporta con su factura electrónica, no con un documento soporte.")
            : null;
    }

    /// <summary>
    /// El «documento del proveedor» del mensaje de un documento soporte: su propio prefijo y número (antes de numerar, el provisional de la
    /// validación previa), la fecha de la operación y, sin CUDS todavía (lo da la emisión), <c>uniqueCode</c> nulo; electrónico siempre.
    /// </summary>
    public static SupplierInvoiceDetail DetalleDelMensaje(InventoryDocument documento) => new()
    {
        DocumentClass = documento.Class,
        SupplierPersonId = documento.CounterpartyPersonId ?? 0,
        SupplierPrefix = documento.Prefix ?? string.Empty,
        SupplierNumber = documento.Number?.ToString(CultureInfo.InvariantCulture) ?? ConfirmacionDeDocumento.NumeroProvisionalDeLaValidacion,
        IssueDate = documento.OperationDate,
        DueDate = documento.DueDate,
        IsCredit = documento.DueDate is { } vence && vence > documento.OperationDate,
        IsElectronic = true,
    };
}
