using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// La generación del documento soporte por operación (feature 012, I4, T741; api.md §14.7; contracts/dian.md §14.2; FR-051): con
/// <c>DocumentoSoporte.Generacion = PorOperacion</c> (el defecto), la confirmación de una recepción de un vendedor <b>no obligado a facturar</b>
/// propone su documento soporte: un borrador de <c>SupportDocument</c> con las mismas líneas y precios, enlazado a la recepción
/// (<c>InvoiceOfReceipt</c>), que la persona revisa y confirma (numera con la resolución y se emite). Lo devuelve en <c>warnings[]</c> como
/// <c>Inventory.SupportDocument.Proposed</c> con su <c>PublicId</c> y su ruta. Nunca bloquea ni confirma nada por su cuenta.
/// <para>
/// No propone si ya hay un documento que factura la recepción (una factura o un soporte vivo), si la cooperativa no tiene un tipo de documento
/// soporte activo, con <c>Semanal</c> (lo genera la tarea de la semana, T742) ni dentro de una compra directa (que arma su propio documento,
/// factura o soporte, en la misma operación). (nuevo)
/// </para>
/// </summary>
public sealed class PropuestaDeDocumentoSoporte(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    IServiceProvider servicios,
    ContextoDeCompraDirecta? compraDirecta = null) : IAvisoAlConfirmar
{
    public const string ProposedCode = "Inventory.SupportDocument.Proposed";
    public const string GeneracionSemanal = "Semanal";

    public async Task<IReadOnlyList<AvisoDto>> AvisarAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (documento.Class != DocumentClass.PurchaseReceipt || documento.Status != DocumentStatus.Confirmed) return [];
        if (compraDirecta?.EnCurso == true || documento.CounterpartyPersonId is not int proveedor) return [];
        var obligado = await db.People.AsNoTracking().Where(p => p.Id == proveedor).Select(p => (bool?)p.IsObligatedToInvoice).FirstOrDefaultAsync(ct);
        if (obligado is not false) return [];
        if (await GeneracionAsync(documento.OperationDate, ct) == GeneracionSemanal) return [];

        var yaFacturada = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == documento.Id && l.Kind == DocumentLinkKind.InvoiceOfReceipt && !l.IsDeleted)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d.Status)
            .AnyAsync(s => s != DocumentStatus.Discarded, ct);
        if (yaFacturada) return [];

        var tipo = await db.InventoryDocumentTypes.AsNoTracking()
            .Where(t => t.Class == DocumentClass.SupportDocument && t.IsActive && !t.IsContingency).OrderBy(t => t.Code).FirstOrDefaultAsync(ct);
        if (tipo is null) return [];

        var propuesto = await ProponerAsync(documento, tipo.PublicId, ct);
        if (propuesto.IsFailure)
            return [new AvisoDto(ProposedCode, $"No se pudo proponer el documento soporte de la recepción: {propuesto.Error.Message}", null)];
        return
        [
            new AvisoDto(ProposedCode, "El proveedor no está obligado a facturar: se preparó el documento soporte de esta compra para revisarlo y confirmarlo.",
                new { supportDocumentPublicId = propuesto.Value, route = $"{Sales.ErroresDeVentas.RutaDeDocumentosSoporte}/{propuesto.Value}" }),
        ];
    }

    /// <summary>El borrador del documento soporte de <paramref name="recepcion"/>: las mismas líneas y precios, enlazado a ella.</summary>
    public async Task<Result<Guid>> ProponerAsync(InventoryDocument recepcion, Guid tipoPublicId, CancellationToken ct)
    {
        var lineas = await db.InventoryDocumentLines.AsNoTracking()
            .Where(l => l.DocumentId == recepcion.Id && !l.IsDeleted).OrderBy(l => l.LineNumber)
            .Select(l => new { l.PublicId, l.Quantity, l.UnitPrice, l.DiscountAmount, l.GrossAmount, Product = l.ProductId, Unit = l.UnitId })
            .ToListAsync(ct);
        var productos = await db.Products.AsNoTracking().Where(p => lineas.Select(l => l.Product).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => lineas.Select(l => l.Unit).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.PublicId, ct);
        var proveedor = await db.People.AsNoTracking().Where(p => p.Id == recepcion.CounterpartyPersonId).Select(p => p.PublicId).FirstAsync(ct);

        var pedido = new SaveInventoryDraftRequest(tipoPublicId, recepcion.OperationDate, null, null, null, proveedor, null, null, null,
            recepcion.Notes, null, null, null,
            lineas.Select(l => new SaveInventoryDraftLine(null, productos[l.Product], unidades[l.Unit], l.Quantity, l.UnitPrice,
                DiscountAmount: l.DiscountAmount > 0m ? l.DiscountAmount : null, ReceiptLinePublicId: l.PublicId)).ToList(),
            OperationMunicipalityDaneCode: recepcion.OperationMunicipalityDaneCode);
        var guardar = servicios.GetRequiredService<SaveInventoryDraftCommandHandler>();
        var r = await guardar.Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Purchases, pedido), ct);
        return r.IsFailure ? Result.Failure<Guid>(r.Error) : Result.Success(r.Value.PublicId);
    }

    private async Task<string> GeneracionAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerComoAsync<string>(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.DocumentoSoporteGeneracion, fecha, ct: ct);
        return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value) ? leido.Value! : string.Empty;
    }
}
