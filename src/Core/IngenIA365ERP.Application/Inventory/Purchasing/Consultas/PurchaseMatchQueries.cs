using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Consultas;

/// <summary>La factura de una línea del cruce: su número en la cooperativa y el del proveedor. (nuevo)</summary>
public sealed record PurchaseMatchInvoiceDto(Guid PublicId, string? DisplayNumber, string? SupplierNumber);

/// <summary>
/// <c>PurchaseMatchLineDto</c> de contracts/api.md §14.9 (T797): una línea de la factura del proveedor cruzada contra lo ordenado y lo
/// recibido, en unidad base. Los precios y la diferencia de precio sólo salen con <c>Inventory.Costs.Read</c> (nulos sin él).
/// <see cref="Status"/> nulo = la línea no excede la tolerancia. <see cref="ExceedsTolerance"/> y <see cref="Tolerance"/> (los valores
/// de tolerancia que se usaron, <c>ToleranceJson</c>) son <b>(nuevos)</b>: la pantalla del cruce muestra la tolerancia usada. (nuevo)
/// </summary>
public sealed record PurchaseMatchLineDto(
    Guid PublicId,
    PurchaseMatchInvoiceDto SupplierInvoice,
    int LineNumber,
    ReferenciaDto Product,
    decimal Ordered,
    decimal Received,
    decimal Invoiced,
    decimal? OrderPrice,
    decimal? ReceiptPrice,
    decimal? InvoicePrice,
    decimal QuantityVariance,
    decimal? PriceVariance,
    IReadOnlyList<string> Reasons,
    PurchaseMatchStatus? Status,
    Guid? ApprovalRequestPublicId,
    bool ExceedsTolerance,
    string Tolerance);

/// <summary>Lo que comparten las consultas del cruce: de las filas vivas a sus DTO (T797). (nuevo)</summary>
public static class ConsultasDelCruce
{
    /// <summary>Las filas vivas de las facturas visibles en el alcance de quien pregunta (la factura por las bodegas de sus recepciones).</summary>
    public static IQueryable<PurchaseMatchLine> Visibles(IApplicationDbContext db, AlcanceDeInventario alcance)
    {
        var facturas = db.InventoryDocuments.AsNoTracking()
            .DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking())
            .Select(d => d.Id);
        return db.PurchaseMatchLines.AsNoTracking().Where(m => !m.IsDeleted && facturas.Contains(m.InvoiceDocumentId));
    }

    /// <summary>Las filas en DTO, con los precios sólo si <paramref name="conPrecios"/>, en el orden dado por la consulta.</summary>
    public static async Task<IReadOnlyList<PurchaseMatchLineDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<PurchaseMatchLine> filas, bool conPrecios, CancellationToken ct)
    {
        if (filas.Count == 0) return [];
        var facturas = filas.Select(f => f.InvoiceDocumentId).Distinct().ToList();
        var lineas = filas.Select(f => f.InvoiceLineId).Distinct().ToList();
        var documentos = await db.InventoryDocuments.AsNoTracking().Where(d => facturas.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Prefix, d.Number }).ToDictionaryAsync(d => d.Id, ct);
        var delProveedor = await db.SupplierInvoiceDetails.AsNoTracking().Where(s => facturas.Contains(s.DocumentId))
            .Select(s => new { s.DocumentId, Numero = s.SupplierPrefix + s.SupplierNumber }).ToDictionaryAsync(s => s.DocumentId, s => s.Numero, ct);
        var deLinea = await (from l in db.InventoryDocumentLines.AsNoTracking()
                             join p in db.Products.AsNoTracking() on l.ProductId equals p.Id
                             where lineas.Contains(l.Id)
                             select new { l.Id, l.LineNumber, Producto = new ReferenciaDto(p.PublicId, p.Code, p.Name) })
            .ToDictionaryAsync(x => x.Id, ct);

        return filas.Where(f => documentos.ContainsKey(f.InvoiceDocumentId) && deLinea.ContainsKey(f.InvoiceLineId)).Select(f =>
        {
            var d = documentos[f.InvoiceDocumentId];
            var l = deLinea[f.InvoiceLineId];
            return new PurchaseMatchLineDto(
                f.PublicId,
                new PurchaseMatchInvoiceDto(d.PublicId, VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number), delProveedor.GetValueOrDefault(d.Id)),
                l.LineNumber,
                l.Producto,
                f.OrderedQuantity,
                f.ReceivedNotInvoicedQuantity,
                f.InvoicedQuantity,
                conPrecios ? f.OrderedUnitPrice : null,
                conPrecios ? f.ReceivedUnitCost : null,
                conPrecios ? f.InvoicedUnitPrice : null,
                f.QuantityDifference,
                conPrecios ? f.PriceDifferenceAmount : null,
                string.IsNullOrEmpty(f.Reasons) ? [] : f.Reasons.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                f.Status,
                f.ApprovalRequestPublicId,
                f.ExceedsTolerance,
                f.ToleranceJson);
        }).ToList();
    }
}

// ------------------------------------------------------------------------------------------------------ lista --

/// <summary>
/// <c>GET /purchases/matches?status=&amp;supplierPersonPublicId=&amp;page=&amp;pageSize=</c> (api.md §14.9, T797): las líneas del cruce de
/// las facturas visibles en el alcance, las más recientes primero. (nuevo)
/// </summary>
public sealed record ListPurchaseMatchesQuery(PurchaseMatchStatus? Status, Guid? SupplierPersonPublicId, PageRequest Pagina)
    : IRequest<Result<PagedResult<PurchaseMatchLineDto>>>;

public sealed class ListPurchaseMatchesQueryValidator : AbstractValidator<ListPurchaseMatchesQuery>
{
    public ListPurchaseMatchesQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Pagina).NotNull();
    }
}

public sealed class ListPurchaseMatchesQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, VistaDeDocumentos vista)
    : IRequestHandler<ListPurchaseMatchesQuery, Result<PagedResult<PurchaseMatchLineDto>>>
{
    public async Task<Result<PagedResult<PurchaseMatchLineDto>>> Handle(ListPurchaseMatchesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = ConsultasDelCruce.Visibles(db, alcance);
        if (request.Status is { } estado) consulta = consulta.Where(m => m.Status == estado);
        if (request.SupplierPersonPublicId is { } proveedor)
        {
            var deProveedor = db.InventoryDocuments.AsNoTracking()
                .Where(d => db.People.Any(p => p.Id == d.CounterpartyPersonId && p.PublicId == proveedor)).Select(d => d.Id);
            consulta = consulta.Where(m => deProveedor.Contains(m.InvoiceDocumentId));
        }

        var pagina = request.Pagina;
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(m => m.InvoiceDocumentId).ThenBy(m => m.InvoiceLineId)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var dtos = await ConsultasDelCruce.DtosAsync(db, filas, await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct), ct);
        return Result.Success(new PagedResult<PurchaseMatchLineDto>(dtos, pagina.SafePage, pagina.SafePageSize, total));
    }
}

// ------------------------------------------------------------------------------------------------ por factura --

/// <summary>
/// <c>GET /purchases/supplier-invoices/{id}/match</c> (api.md §14.9, T797): el cruce de esa factura, línea por línea (vacío si no se
/// cruzó contra una orden). Fuera del alcance o si no es una factura del proveedor, el 404 del documento. (nuevo)
/// </summary>
public sealed record GetSupplierInvoiceMatchQuery(Guid InvoicePublicId) : IRequest<Result<IReadOnlyList<PurchaseMatchLineDto>>>;

public sealed class GetSupplierInvoiceMatchQueryHandler(IApplicationDbContext db, VistaDeDocumentos vista)
    : IRequestHandler<GetSupplierInvoiceMatchQuery, Result<IReadOnlyList<PurchaseMatchLineDto>>>
{
    public async Task<Result<IReadOnlyList<PurchaseMatchLineDto>>> Handle(GetSupplierInvoiceMatchQuery request, CancellationToken ct)
    {
        var factura = await vista.BuscarAsync(request.InvoicePublicId, DocumentClassGroup.Purchases, seguir: false, ct);
        if (factura is null || factura.Class != DocumentClass.SupplierInvoice)
            return Result.Failure<IReadOnlyList<PurchaseMatchLineDto>>(InventoryErrors.DocumentNotFound());
        return Result.Success(await DeFacturaAsync(db, vista, factura.Id, ct));
    }

    /// <summary>Las filas vivas de una factura ya encontrada (también para el <c>match?</c> de su detalle).</summary>
    public static async Task<IReadOnlyList<PurchaseMatchLineDto>> DeFacturaAsync(IApplicationDbContext db, VistaDeDocumentos vista, int facturaId, CancellationToken ct)
    {
        var filas = await db.PurchaseMatchLines.AsNoTracking().Where(m => m.InvoiceDocumentId == facturaId && !m.IsDeleted)
            .OrderBy(m => m.InvoiceLineId).ToListAsync(ct);
        return await ConsultasDelCruce.DtosAsync(db, filas, await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct), ct);
    }
}
