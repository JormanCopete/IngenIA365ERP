using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Lo que una devolución a proveedor pide corregir en lo facturado (revisión comercial del 2026-11-12;
/// <c>GET /api/inventory/purchases/returns/{id}/credit-proposal</c>, <c>Inventory.Purchases.View</c>). Devolver mercancía que el proveedor ya facturó
/// no cambia su factura: hace falta su <b>nota crédito</b> (clase <c>SupplierNote</c>, la emite el proveedor con su número y su CUFE, así que la
/// cooperativa la registra) o, si lo recibido lo cubrió un <b>documento soporte</b> (clase 6), la <b>nota de ajuste del documento soporte</b> (clase 7),
/// que la cooperativa emite y numera —el documento soporte no se anula, se corrige con ella—. Por eso la devolución no crea la nota: propone sus
/// líneas (las de la factura o del soporte que cubrieron lo devuelto, con la cantidad ya facturada que se devuelve y su valor al precio neto facturado)
/// y la pantalla abre el formulario de la nota prellenado; la nota lleva en su referencia externa la de la devolución
/// (<see cref="Referencia"/>), y así la propuesta sabe si ya se registró. (nuevo)
/// </summary>
public sealed record GetReturnCreditProposalQuery(Guid ReturnPublicId) : IRequest<Result<ReturnCreditProposalDto>>;

/// <summary>Una línea propuesta para la nota: la de la factura (o del soporte), la cantidad en su unidad y su valor. (nuevo)</summary>
public sealed record ReturnCreditLineDto(Guid InvoiceLinePublicId, int InvoiceLineNumber, string ProductCode, string ProductName, decimal Quantity,
    decimal UnitNetPrice, decimal Amount);

/// <summary>
/// Lo que pide una factura (o un documento soporte): su clase, número y documento del proveedor, las líneas y el valor de la nota, y las notas ya
/// registradas con la referencia de la devolución (<see cref="Covered"/> = alguna confirmada). (nuevo)
/// </summary>
public sealed record ReturnCreditByInvoiceDto(Guid InvoicePublicId, DocumentClass Class, string? DisplayNumber, string? SupplierDocument,
    IReadOnlyList<ReturnCreditLineDto> Lines, decimal Amount, IReadOnlyList<DocumentoDeLaNotaDto> Notes, bool Covered);

/// <summary>Una nota que lleva la referencia de la devolución. (nuevo)</summary>
public sealed record DocumentoDeLaNotaDto(Guid PublicId, DocumentClass Class, string? DisplayNumber, DocumentStatus Status);

/// <summary>
/// La propuesta de una devolución: si exige la nota crédito del proveedor, si exige la nota de ajuste de un documento soporte (obligatoria por la regla
/// de la clase: el soporte se corrige con su nota), la referencia que debe llevar la nota y lo que pide cada factura. (nuevo)
/// </summary>
public sealed record ReturnCreditProposalDto(Guid ReturnPublicId, string? ReturnNumber, string Reference, bool SupplierCreditNoteRequired,
    bool SupportDocumentAdjustmentNoteRequired, IReadOnlyList<ReturnCreditByInvoiceDto> Invoices);

public sealed class GetReturnCreditProposalQueryHandler(IApplicationDbContext db, VistaDeDocumentos vista)
    : IRequestHandler<GetReturnCreditProposalQuery, Result<ReturnCreditProposalDto>>
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    /// <summary>La referencia externa que lleva la nota hecha por la devolución (60 caracteres como máximo, los de la columna).</summary>
    public static string Referencia(string? numeroDeLaDevolucion)
    {
        var texto = $"Devolución {numeroDeLaDevolucion}".Trim();
        return texto.Length <= 60 ? texto : texto[..60];
    }

    public async Task<Result<ReturnCreditProposalDto>> Handle(GetReturnCreditProposalQuery request, CancellationToken ct)
    {
        // El alcance lo aplica VistaDeDocumentos.BuscarAsync (el mismo 404 fuera de él).
        var devolucion = await vista.BuscarAsync(request.ReturnPublicId, DocumentClassGroup.Purchases, seguir: false, ct);
        if (devolucion is null || devolucion.Class != DocumentClass.SupplierReturn) return Result.Failure<ReturnCreditProposalDto>(InventoryErrors.DocumentNotFound());
        var numero = devolucion.Number is null ? null : VistaDeDocumentos.NumeroVisible(devolucion.Prefix, devolucion.Number);
        var referencia = Referencia(numero);
        if (devolucion.Status != DocumentStatus.Confirmed)
            return Result.Success(new ReturnCreditProposalDto(devolucion.PublicId, numero, referencia, false, false, []));

        // Lo devuelto por línea de recepción (esta devolución).
        var propias = await (from x in db.DocumentLineLinks.AsNoTracking()
                             join l in db.DocumentLinks.AsNoTracking() on x.DocumentLinkId equals l.Id
                             where l.TargetDocumentId == devolucion.Id && l.Kind == DocumentLinkKind.ReturnOf && !x.IsDeleted && !l.IsDeleted
                             select new { x.SourceLineId, x.QuantityBase }).ToListAsync(ct);
        var devueltoAqui = propias.GroupBy(p => p.SourceLineId).ToDictionary(g => g.Key, g => g.Sum(p => p.QuantityBase));
        if (devueltoAqui.Count == 0) return Result.Success(new ReturnCreditProposalDto(devolucion.PublicId, numero, referencia, false, false, []));
        var lineasDeRecepcion = devueltoAqui.Keys.ToList();
        var recibido = await db.InventoryDocumentLines.AsNoTracking().Where(l => lineasDeRecepcion.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.QuantityBase, ct);

        // Lo facturado (factura del proveedor o documento soporte vigentes) y lo devuelto por las otras devoluciones vigentes.
        var consumos = await (from x in db.DocumentLineLinks.AsNoTracking()
                              join l in db.DocumentLinks.AsNoTracking() on x.DocumentLinkId equals l.Id
                              join d in db.InventoryDocuments.AsNoTracking() on l.TargetDocumentId equals d.Id
                              where lineasDeRecepcion.Contains(x.SourceLineId) && !x.IsDeleted && !l.IsDeleted && Vigentes.Contains(d.Status)
                                    && (l.Kind == DocumentLinkKind.InvoiceOfReceipt || (l.Kind == DocumentLinkKind.ReturnOf && d.Id != devolucion.Id))
                              select new { x.SourceLineId, x.TargetLineId, x.QuantityBase, l.Kind, DocumentoId = d.Id, d.Class })
            .ToListAsync(ct);

        // Por línea de recepción: de lo que devuelve esta devolución, cuánto ya estaba facturado (las otras devoluciones consumen primero lo no
        // facturado), repartido entre las líneas de factura que la cubrieron, en su orden.
        var porLineaDeFactura = new Dictionary<int, decimal>();
        foreach (var (lineaDeRecepcion, aqui) in devueltoAqui)
        {
            var facturas = consumos.Where(c => c.SourceLineId == lineaDeRecepcion && c.Kind == DocumentLinkKind.InvoiceOfReceipt
                                               && c.Class is DocumentClass.SupplierInvoice or DocumentClass.SupportDocument)
                .OrderBy(c => c.DocumentoId).ThenBy(c => c.TargetLineId).ToList();
            var facturado = facturas.Sum(c => c.QuantityBase);
            var otras = consumos.Where(c => c.SourceLineId == lineaDeRecepcion && c.Kind == DocumentLinkKind.ReturnOf).Sum(c => c.QuantityBase);
            var sinFacturar = recibido.GetValueOrDefault(lineaDeRecepcion) - facturado;
            var aAcreditar = Math.Max(0m, otras + aqui - sinFacturar) - Math.Max(0m, otras - sinFacturar);
            foreach (var f in facturas)
            {
                if (aAcreditar <= 0m) break;
                var parte = Math.Min(aAcreditar, f.QuantityBase);
                porLineaDeFactura[f.TargetLineId] = porLineaDeFactura.GetValueOrDefault(f.TargetLineId) + parte;
                aAcreditar -= parte;
            }
        }
        if (porLineaDeFactura.Count == 0) return Result.Success(new ReturnCreditProposalDto(devolucion.PublicId, numero, referencia, false, false, []));

        var idsDeLinea = porLineaDeFactura.Keys.ToList();
        var lineasDeFactura = await (from l in db.InventoryDocumentLines.AsNoTracking()
                                     join p in db.Products.AsNoTracking() on l.ProductId equals p.Id
                                     where idsDeLinea.Contains(l.Id)
                                     select new { l.Id, l.PublicId, l.DocumentId, l.LineNumber, l.Quantity, l.Factor, l.NetAmount, p.Code, p.Name }).ToListAsync(ct);
        var idsDeFactura = lineasDeFactura.Select(l => l.DocumentId).Distinct().ToList();
        var facturasDoc = await db.InventoryDocuments.AsNoTracking().Where(d => idsDeFactura.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Class, d.Prefix, d.Number }).ToDictionaryAsync(d => d.Id, ct);
        var proveedor = await db.SupplierInvoiceDetails.AsNoTracking().Where(s => idsDeFactura.Contains(s.DocumentId))
            .Select(s => new { s.DocumentId, s.SupplierPrefix, s.SupplierNumber }).ToListAsync(ct);
        var notas = await (from l in db.DocumentLinks.AsNoTracking()
                           join d in db.InventoryDocuments.AsNoTracking() on l.TargetDocumentId equals d.Id
                           where idsDeFactura.Contains(l.SourceDocumentId) && l.Kind == DocumentLinkKind.NoteOf && !l.IsDeleted
                                 && d.ExternalReference == referencia && d.Status != DocumentStatus.Discarded
                           select new { l.SourceDocumentId, d.PublicId, d.Class, d.Prefix, d.Number, d.Status }).ToListAsync(ct);

        var porFactura = lineasDeFactura.GroupBy(l => l.DocumentId).OrderBy(g => g.Key).Select(g =>
        {
            var f = facturasDoc[g.Key];
            var lineas = g.OrderBy(l => l.LineNumber).Select(l =>
            {
                var cantidad = l.Factor == 0m ? porLineaDeFactura[l.Id] : Math.Round(porLineaDeFactura[l.Id] / l.Factor, 4, MidpointRounding.AwayFromZero);
                var precioNeto = l.Quantity == 0m ? 0m : Math.Round(l.NetAmount / l.Quantity, 6, MidpointRounding.AwayFromZero);
                return new ReturnCreditLineDto(l.PublicId, l.LineNumber, l.Code, l.Name, cantidad, precioNeto,
                    Math.Round(cantidad * precioNeto, 2, MidpointRounding.AwayFromZero));
            }).ToList();
            var s = proveedor.FirstOrDefault(p => p.DocumentId == g.Key);
            var deLaFactura = notas.Where(n => n.SourceDocumentId == g.Key)
                .Select(n => new DocumentoDeLaNotaDto(n.PublicId, n.Class, n.Number is null ? null : VistaDeDocumentos.NumeroVisible(n.Prefix, n.Number), n.Status))
                .ToList();
            return new ReturnCreditByInvoiceDto(f.PublicId, f.Class, f.Number is null ? null : VistaDeDocumentos.NumeroVisible(f.Prefix, f.Number),
                s is null ? null : $"{s.SupplierPrefix}{s.SupplierNumber}", lineas, lineas.Sum(x => x.Amount), deLaFactura,
                deLaFactura.Any(n => n.Status == DocumentStatus.Confirmed));
        }).ToList();

        return Result.Success(new ReturnCreditProposalDto(devolucion.PublicId, numero, referencia,
            porFactura.Any(f => f.Class == DocumentClass.SupplierInvoice),
            porFactura.Any(f => f.Class == DocumentClass.SupportDocument),
            porFactura));
    }
}
