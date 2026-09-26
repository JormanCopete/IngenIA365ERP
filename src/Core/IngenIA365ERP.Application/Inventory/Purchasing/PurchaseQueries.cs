using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>Los filtros de las listas de compras (api.md §14.2, §14.4). Todos opcionales y combinables. (nuevo)</summary>
public sealed record FiltrosDeCompras(
    Guid? SupplierPersonPublicId = null,
    DocumentStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? WarehousePublicId = null,
    string? PaymentForm = null,
    bool? RadianPending = null,
    string? Number = null);

/// <summary>
/// Lo que comparten las consultas de compras (feature 012, T348): la base de una clase con el alcance por bodega aplicado
/// (<see cref="FiltroDeAlcance"/>: una factura sin bodega es visible si alguna de sus recepciones lo es, data-model §5.2), los
/// filtros comunes y los eventos RADIAN. (nuevo)
/// </summary>
public static class ConsultasDeCompras
{
    /// <summary>Los eventos RADIAN de una factura, 030 primero, con quién los registró.</summary>
    public static async Task<IReadOnlyList<RadianEventDto>> EventosAsync(IApplicationDbContext db, int facturaId, CancellationToken ct)
    {
        var eventos = await db.SupplierInvoiceEvents.AsNoTracking().Where(e => e.DocumentId == facturaId).OrderBy(e => e.EventCode).ToListAsync(ct);
        var usuarios = eventos.Select(e => e.RegisteredByUserId).OfType<int>().Distinct().ToList();
        var nombres = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarios.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UsuarioDto(u.PublicId, u.Username), ct);
        return eventos.Select(e => new RadianEventDto(e.EventCode, e.Status, e.EventDate, e.Source, e.Cude,
            e.RegisteredByUserId is int u ? nombres.GetValueOrDefault(u) : null, e.RegisteredAt, e.Notes, e.ElectronicDocumentPublicId)).ToList();
    }

    /// <summary>Los documentos de <paramref name="clase"/> visibles en el alcance de quien pregunta, con los filtros comunes.</summary>
    public static async Task<IQueryable<InventoryDocument>?> BaseAsync(
        IApplicationDbContext db, IMaestrosDelDocumento maestros, AlcanceDeInventario alcance, DocumentClass clase, FiltrosDeCompras f, CancellationToken ct)
    {
        var consulta = db.InventoryDocuments.AsNoTracking().Where(d => d.Class == clase)
            .DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());
        if (f.Status is { } estado) consulta = consulta.Where(d => d.Status == estado);
        if (f.From is { } desde) consulta = consulta.Where(d => d.OperationDate >= desde);
        if (f.To is { } hasta) consulta = consulta.Where(d => d.OperationDate <= hasta);
        if (f.SupplierPersonPublicId is { } proveedor)
            consulta = consulta.Where(d => db.People.Any(p => p.Id == d.CounterpartyPersonId && p.PublicId == proveedor));
        if (f.WarehousePublicId is { } bodegaPedida)
        {
            var bodega = (await maestros.BodegasAsync([bodegaPedida], ct)).FirstOrDefault();
            if (bodega is null) return null;
            consulta = consulta.Where(d => d.WarehouseId == bodega.Id);
        }
        if (!string.IsNullOrWhiteSpace(f.Number))
        {
            var texto = f.Number.Trim().ToUpperInvariant();
            consulta = consulta.Where(d => db.SupplierInvoiceDetails.Any(s => s.DocumentId == d.Id && (s.SupplierPrefix + s.SupplierNumber).Contains(texto))
                                           || (d.Number != null && (d.Prefix + d.Number.ToString()).Contains(texto)));
        }
        return consulta;
    }

    public static async Task<(List<InventoryDocument> Documentos, long Total)> PaginaAsync(IQueryable<InventoryDocument> consulta, PageRequest pagina, CancellationToken ct)
    {
        var total = await consulta.LongCountAsync(ct);
        var documentos = await consulta
            .OrderByDescending(d => d.OperationDate).ThenByDescending(d => d.Number).ThenByDescending(d => d.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .ToListAsync(ct);
        return (documentos, total);
    }

    /// <summary>Por línea de recepción: recibido, facturado, devuelto, por facturar y devolvible (de los vínculos vigentes).</summary>
    public static async Task<IReadOnlyList<ReceiptLineBalanceDto>> SaldosDeRecepcionAsync(
        VinculosDeCompra vinculos, IReadOnlyList<InventoryDocumentLine> lineas, CancellationToken ct)
    {
        var consumo = await vinculos.ConsumoAsync(lineas.Select(l => l.Id).ToList(), excluir: 0, ct);
        return lineas.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).Select(l =>
        {
            var c = consumo.GetValueOrDefault(l.Id) ?? new ConsumoDeRecepcion(0m, 0m);
            return new ReceiptLineBalanceDto(l.PublicId, l.LineNumber, l.QuantityBase, c.Facturado, c.Devuelto,
                Math.Max(0m, l.QuantityBase - c.Facturado - c.Devuelto), Math.Max(0m, l.QuantityBase - c.Devuelto));
        }).ToList();
    }

    public static SupplierInvoiceInfoDto? Info(Domain.Entities.Inventory.Purchasing.SupplierInvoiceDetail? d) => d is null
        ? null
        : new SupplierInvoiceInfoDto(d.SupplierPrefix, d.SupplierNumber, d.Cufe, d.IssueDate, d.DueDate, d.PaymentForm, d.IsElectronic, d.IsDebitNote, d.IsReleased);
}

// ------------------------------------------------------------------------------------------------ recepciones --

/// <summary>La lista de recepciones (§14.2) con lo que falta facturar y lo devuelto por documento. (nuevo)</summary>
public sealed record ListPurchaseReceiptsQuery(FiltrosDeCompras Filtros, PageRequest Pagina) : IRequest<Result<PagedResult<PurchaseReceiptSummaryDto>>>;

public sealed class ListPurchaseReceiptsQueryValidator : AbstractValidator<ListPurchaseReceiptsQuery>
{
    public ListPurchaseReceiptsQueryValidator()
    {
        RuleFor(x => x.Filtros).NotNull().Must(f => f.From is null || f.To is null || f.From <= f.To).WithMessage("La fecha inicial es posterior a la final.");
        RuleFor(x => x.Pagina).NotNull();
    }
}

public sealed class ListPurchaseReceiptsQueryHandler(
    IApplicationDbContext db, IMaestrosDelDocumento maestros, IAlcanceDeInventario alcanceDeLaPeticion, VistaDeDocumentos vista, VinculosDeCompra vinculos)
    : IRequestHandler<ListPurchaseReceiptsQuery, Result<PagedResult<PurchaseReceiptSummaryDto>>>
{
    public async Task<Result<PagedResult<PurchaseReceiptSummaryDto>>> Handle(ListPurchaseReceiptsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = await ConsultasDeCompras.BaseAsync(db, maestros, alcance, DocumentClass.PurchaseReceipt, request.Filtros, ct);
        if (consulta is null) return Result.Success(new PagedResult<PurchaseReceiptSummaryDto>([], request.Pagina.SafePage, request.Pagina.SafePageSize, 0));
        var (documentos, total) = await ConsultasDeCompras.PaginaAsync(consulta, request.Pagina, ct);

        var ids = documentos.Select(d => d.Id).ToList();
        var lineas = await db.InventoryDocumentLines.AsNoTracking().Where(l => ids.Contains(l.DocumentId) && !l.IsDeleted).ToListAsync(ct);
        var saldos = await ConsultasDeCompras.SaldosDeRecepcionAsync(vinculos, lineas, ct);
        var porLinea = saldos.ToDictionary(s => s.LinePublicId);
        var resumenes = await vista.ResumenesAsync(documentos, ct);
        return Result.Success(new PagedResult<PurchaseReceiptSummaryDto>(resumenes.Select((r, i) =>
        {
            var deEste = lineas.Where(l => l.DocumentId == documentos[i].Id).Select(l => porLinea[l.PublicId]).ToList();
            return new PurchaseReceiptSummaryDto(r, deEste.Sum(s => s.PendingToInvoice), deEste.Sum(s => s.Returned));
        }).ToList(), request.Pagina.SafePage, request.Pagina.SafePageSize, total));
    }
}

// ------------------------------------------------------------------------------------------------ facturas --

/// <summary>La lista de facturas del proveedor (§14.4) con el documento del proveedor y el estado de sus eventos. (nuevo)</summary>
public sealed record ListSupplierInvoicesQuery(FiltrosDeCompras Filtros, PageRequest Pagina, DocumentClass Clase = DocumentClass.SupplierInvoice)
    : IRequest<Result<PagedResult<SupplierInvoiceSummaryDto>>>;

public sealed class ListSupplierInvoicesQueryValidator : AbstractValidator<ListSupplierInvoicesQuery>
{
    public ListSupplierInvoicesQueryValidator()
    {
        RuleFor(x => x.Filtros).NotNull().Must(f => f.From is null || f.To is null || f.From <= f.To).WithMessage("La fecha inicial es posterior a la final.");
        RuleFor(x => x.Filtros.PaymentForm).Must(p => p is null or "Cash" or "Credit").WithMessage("La forma de pago es Cash o Credit.");
        RuleFor(x => x.Clase).Must(c => c is DocumentClass.SupplierInvoice or DocumentClass.SupplierNote);
        RuleFor(x => x.Pagina).NotNull();
    }
}

public sealed class ListSupplierInvoicesQueryHandler(
    IApplicationDbContext db, IMaestrosDelDocumento maestros, IAlcanceDeInventario alcanceDeLaPeticion, VistaDeDocumentos vista)
    : IRequestHandler<ListSupplierInvoicesQuery, Result<PagedResult<SupplierInvoiceSummaryDto>>>
{
    public async Task<Result<PagedResult<SupplierInvoiceSummaryDto>>> Handle(ListSupplierInvoicesQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = await ConsultasDeCompras.BaseAsync(db, maestros, alcance, request.Clase, f, ct);
        if (consulta is null) return Result.Success(new PagedResult<SupplierInvoiceSummaryDto>([], request.Pagina.SafePage, request.Pagina.SafePageSize, 0));
        if (f.PaymentForm is { } forma)
        {
            var credito = forma == "Credit";
            consulta = consulta.Where(d => db.SupplierInvoiceDetails.Any(s => s.DocumentId == d.Id && s.IsCredit == credito));
        }
        if (f.RadianPending is { } pendiente)
        {
            consulta = pendiente
                ? consulta.Where(d => db.SupplierInvoiceEvents.Any(e => e.DocumentId == d.Id && e.Status == SupplierInvoiceEventStatus.Pending))
                : consulta.Where(d => !db.SupplierInvoiceEvents.Any(e => e.DocumentId == d.Id && e.Status == SupplierInvoiceEventStatus.Pending));
        }
        var (documentos, total) = await ConsultasDeCompras.PaginaAsync(consulta, request.Pagina, ct);

        var ids = documentos.Select(d => d.Id).ToList();
        var detalles = await db.SupplierInvoiceDetails.AsNoTracking().Where(s => ids.Contains(s.DocumentId)).ToDictionaryAsync(s => s.DocumentId, ct);
        var eventos = await db.SupplierInvoiceEvents.AsNoTracking().Where(e => ids.Contains(e.DocumentId)).ToListAsync(ct);
        var resumenes = await vista.ResumenesAsync(documentos, ct);
        return Result.Success(new PagedResult<SupplierInvoiceSummaryDto>(resumenes.Select((r, i) =>
        {
            var id = documentos[i].Id;
            return new SupplierInvoiceSummaryDto(r, ConsultasDeCompras.Info(detalles.GetValueOrDefault(id)),
                eventos.FirstOrDefault(e => e.DocumentId == id && e.EventCode == SupplierInvoiceEventCode.Receipt030)?.Status,
                eventos.FirstOrDefault(e => e.DocumentId == id && e.EventCode == SupplierInvoiceEventCode.GoodsReceived032)?.Status);
        }).ToList(), request.Pagina.SafePage, request.Pagina.SafePageSize, total));
    }
}

// ------------------------------------------------------------------------------------------------- detalle --

/// <summary>
/// El detalle de un documento de compras (§14.2, §14.4): el genérico más el documento del proveedor y sus eventos RADIAN, los
/// saldos por línea de una recepción y, en un borrador, la vista previa de impuestos. <paramref name="Clase"/> nula = cualquiera
/// del grupo. (nuevo)
/// </summary>
public sealed record GetPurchaseDocumentQuery(Guid DocumentPublicId, DocumentClass? Clase = null) : IRequest<Result<PurchaseDocumentDto>>;

public sealed class GetPurchaseDocumentQueryHandler(
    IApplicationDbContext db, VistaDeDocumentos vista, VinculosDeCompra vinculos, CalculoTributarioDeCompra calculo)
    : IRequestHandler<GetPurchaseDocumentQuery, Result<PurchaseDocumentDto>>
{
    public async Task<Result<PurchaseDocumentDto>> Handle(GetPurchaseDocumentQuery request, CancellationToken ct)
    {
        // El alcance lo aplica VistaDeDocumentos.BuscarAsync con IAlcanceDeInventario (el mismo 404 fuera de él).
        var documento = await vista.BuscarAsync(request.DocumentPublicId, DocumentClassGroup.Purchases, seguir: false, ct);
        if (documento is null || (request.Clase is { } clase && documento.Class != clase && documento.Class != DocumentClass.Voiding))
            return Result.Failure<PurchaseDocumentDto>(InventoryErrors.DocumentNotFound());

        var detalle = await vista.DetalleAsync(documento, [], ct);
        if (documento.Status == DocumentStatus.Draft && documento.Class is DocumentClass.PurchaseReceipt or DocumentClass.SupplierInvoice
            && documento.CounterpartyPersonId is not null && documento.DocumentType is { } tipo)
        {
            var previa = await calculo.CalcularAsync(documento, tipo, ct);
            if (previa.IsSuccess)
            {
                detalle = detalle with
                {
                    TaxLines = previa.Value.Renglones.Select(r => new DocumentTaxLineDto(r.Linea, r.Kind, r.TaxRateCode, r.Rate, r.AmountPerUnit,
                        r.Base, r.Amount, r.Treatment, r.MunicipalityDaneCode, System.Text.Json.JsonSerializer.Serialize(r.Explicacion))).ToList(),
                };
            }
        }

        var proveedor = await db.SupplierInvoiceDetails.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == documento.Id, ct);
        var eventos = documento.Class == DocumentClass.SupplierInvoice ? await ConsultasDeCompras.EventosAsync(db, documento.Id, ct) : [];
        var saldos = documento.Class == DocumentClass.PurchaseReceipt
            ? await ConsultasDeCompras.SaldosDeRecepcionAsync(vinculos, documento.Lines.ToList(), ct)
            : [];
        return Result.Success(new PurchaseDocumentDto(detalle, ConsultasDeCompras.Info(proveedor), eventos, saldos, documento.OperationMunicipalityDaneCode,
            await AjustesDeCostoAsync(documento, ct)));
    }

    /// <summary>
    /// Las diferencias de costo que dejó el documento (la de precio de una factura o nota, la de una devolución o una anulación)
    /// por producto: Σ de sus líneas <c>CostAdjustment</c>. Es un valor: sólo con <c>Inventory.Costs.Read</c>.
    /// </summary>
    private async Task<IReadOnlyList<AjusteDeCostoDeAnulacionDto>?> AjustesDeCostoAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (!await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct)) return null;
        var ajustes = await db.KardexEntries.AsNoTracking()
            .Where(k => k.DocumentId == documento.Id && k.Kind == KardexEntryKind.CostAdjustment)
            .GroupBy(k => k.ProductId)
            .Select(g => new { ProductId = g.Key, Diferencia = g.Sum(k => k.TotalCost) })
            .ToListAsync(ct);
        if (ajustes.Count == 0) return [];
        var ids = ajustes.Select(a => a.ProductId).ToList();
        var productos = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new ReferenciaDto(p.PublicId, p.Code, p.Name), ct);
        return ajustes.Where(a => productos.ContainsKey(a.ProductId))
            .Select(a => new AjusteDeCostoDeAnulacionDto(productos[a.ProductId], a.Diferencia)).ToList();
    }
}
