using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Transfers;

/// <summary>
/// Lo que comparten las consultas de traslados (feature 012, US10, T374; contracts/api.md §11): el estado derivado de cada despacho,
/// lo recibido y lo que sigue en tránsito por línea, y las diferencias con su documento y su solicitud de aprobación. Los valores
/// sólo con <c>Inventory.Costs.Read</c>. (nuevo)
/// </summary>
public sealed class VistaDeTraslados(IApplicationDbContext db, VistaDeDocumentos vista)
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    /// <summary>Un vínculo de línea desde un despacho: su línea origen, cuánto, de qué clase y estado es el destino y con qué relación.</summary>
    public sealed record Consumo(int DespachoId, int LineaDeDespachoId, decimal QuantityBase, DocumentClass Clase, DocumentStatus Estado, DocumentLinkKind Kind, int DocumentoId);

    /// <summary>Lo que salió del tránsito de cada despacho: recepciones (y las de sus diferencias), devoluciones al origen y bajas.</summary>
    public async Task<IReadOnlyList<Consumo>> ConsumosAsync(IReadOnlyCollection<int> despachos, CancellationToken ct) =>
        (await db.DocumentLinks.AsNoTracking()
                .Where(l => despachos.Contains(l.SourceDocumentId) && l.Kind != DocumentLinkKind.Voids)
                .Join(db.DocumentLineLinks.AsNoTracking(), l => l.Id, x => x.DocumentLinkId, (l, x) => new { l.SourceDocumentId, l.Kind, l.TargetDocumentId, x.SourceLineId, x.QuantityBase })
                .Join(db.InventoryDocuments.AsNoTracking(), y => y.TargetDocumentId, d => d.Id, (y, d) => new { y, d.Class, d.Status })
                .ToListAsync(ct))
            .Select(z => new Consumo(z.y.SourceDocumentId, z.y.SourceLineId, z.y.QuantityBase, z.Class, z.Status, z.y.Kind, z.y.TargetDocumentId))
            .ToList();

    /// <summary>La primera recepción vigente de cada despacho (la de <c>ReceiveTransferCommand</c>).</summary>
    public static IReadOnlyDictionary<int, int> Recepciones(IReadOnlyList<Consumo> consumos) =>
        consumos.Where(c => c.Clase == DocumentClass.TransferReceipt && c.Kind == DocumentLinkKind.ReceiptOf && Vigentes.Contains(c.Estado))
            .GroupBy(c => c.DespachoId).ToDictionary(g => g.Key, g => g.Min(c => c.DocumentoId));

    /// <summary>El estado derivado (§11): borrador, en aprobación, en tránsito, recibido (con o sin diferencias pendientes), anulado.</summary>
    public static string EstadoDe(InventoryDocument despacho, bool recibido, int diferenciasPendientes) => despacho.Status switch
    {
        DocumentStatus.Draft => EstadosDeTraslado.Borrador,
        DocumentStatus.PendingApproval => EstadosDeTraslado.EnAprobacion,
        DocumentStatus.Voided => EstadosDeTraslado.Anulado,
        _ when !recibido => EstadosDeTraslado.EnTransito,
        _ when diferenciasPendientes > 0 => EstadosDeTraslado.RecibidoConDiferencias,
        _ => EstadosDeTraslado.Recibido,
    };

    /// <summary>Las diferencias como las ve la pantalla, en el orden dado.</summary>
    public async Task<IReadOnlyList<TransferDiscrepancyDto>> DiferenciasAsync(IReadOnlyList<TransferDiscrepancy> diferencias, CancellationToken ct)
    {
        if (diferencias.Count == 0) return [];
        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);
        var despachoIds = diferencias.Select(d => d.DispatchDocumentId).Distinct().ToList();
        var despachos = await db.InventoryDocuments.AsNoTracking().Where(d => despachoIds.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Prefix, d.Number, d.DestinationWarehouseId }).ToDictionaryAsync(d => d.Id, ct);
        var productoIds = diferencias.Select(d => d.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new ReferenciaDto(p.PublicId, p.Code, p.Name), ct);
        var bodegaIds = despachos.Values.Select(d => d.DestinationWarehouseId).OfType<int>().Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => bodegaIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => new ReferenciaDto(w.PublicId, w.Code, w.Name), ct);
        var documentoIds = diferencias.Select(d => d.ResolutionDocumentId).OfType<int>().Distinct().ToList();
        var documentos = await db.InventoryDocuments.AsNoTracking().Where(d => documentoIds.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Class, d.Prefix, d.Number, d.Status }).ToDictionaryAsync(d => d.Id, ct);
        var publicas = diferencias.Select(d => d.PublicId).ToList();
        var solicitudes = (await db.ApprovalRequests.AsNoTracking()
                .Where(r => r.SourceType == ApprovalSourceTypes.TransferDiscrepancy && publicas.Contains(r.SourcePublicId) && r.Status == ApprovalRequestStatus.Pending)
                .Select(r => new { r.SourcePublicId, r.PublicId }).ToListAsync(ct))
            .GroupBy(r => r.SourcePublicId).ToDictionary(g => g.Key, g => g.First().PublicId);

        return diferencias.Select(d =>
        {
            var despacho = despachos.GetValueOrDefault(d.DispatchDocumentId);
            var documento = d.ResolutionDocumentId is int r ? documentos.GetValueOrDefault(r) : null;
            return new TransferDiscrepancyDto(
                d.PublicId, d.Kind,
                new TransferRefDto(despacho?.PublicId ?? Guid.Empty, despacho is null ? null : VistaDeDocumentos.NumeroVisible(despacho.Prefix, despacho.Number)),
                productos.GetValueOrDefault(d.ProductId) ?? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty),
                d.QuantityBase, d.Pendiente(),
                costos && d.UnitCost is { } c ? Math.Round(d.QuantityBase * c, 2, MidpointRounding.AwayFromZero) : null,
                d.Estado, d.Resolution,
                documento is null ? null : new ResolvingDocumentDto(documento.PublicId, documento.Class, VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number), documento.Status),
                solicitudes.TryGetValue(d.PublicId, out var s) ? s : null,
                despacho?.DestinationWarehouseId is int b ? bodegas.GetValueOrDefault(b) : null,
                d.CreatedAt);
        }).ToList();
    }
}

// ------------------------------------------------------------------------------------------------ lista --

/// <summary>
/// <c>GET /api/inventory/transfers</c> (feature 012, US10, T374; contracts/api.md §11): los despachos (sin descartados) de las bodegas
/// del alcance —origen <b>o</b> destino—, con su estado derivado, lo despachado y lo recibido, y las diferencias pendientes. Orden:
/// fecha y número, descendente. (nuevo)
/// </summary>
public sealed record ListTransfersQuery(
    string? State = null,
    Guid? OriginWarehousePublicId = null,
    Guid? DestinationWarehousePublicId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    PageRequest? Pagina = null) : IRequest<Result<PagedResult<TransferSummaryDto>>>;

public sealed class ListTransfersQueryValidator : AbstractValidator<ListTransfersQuery>
{
    public ListTransfersQueryValidator()
    {
        RuleFor(x => x.State).Must(s => s is null || EstadosDeTraslado.Todos.Contains(s))
            .WithMessage($"El estado es uno de: {string.Join(", ", EstadosDeTraslado.Todos)}.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithMessage("La fecha inicial es posterior a la final.");
    }
}

public sealed class ListTransfersQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IMaestrosDelDocumento maestros,
    VistaDeDocumentos vista,
    VistaDeTraslados traslados)
    : IRequestHandler<ListTransfersQuery, Result<PagedResult<TransferSummaryDto>>>
{
    public async Task<Result<PagedResult<TransferSummaryDto>>> Handle(ListTransfersQuery request, CancellationToken ct)
    {
        var pagina = (request.Pagina ?? new PageRequest()).SafePage;
        var tamano = (request.Pagina ?? new PageRequest()).SafePageSize;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var consulta = db.InventoryDocuments.AsNoTracking().Include(d => d.Lines)
            .Where(d => d.Class == DocumentClass.TransferDispatch && d.Status != DocumentStatus.Discarded)
            .DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());
        if (request.From is { } desde) consulta = consulta.Where(d => d.OperationDate >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(d => d.OperationDate <= hasta);
        var pedidas = new[] { request.OriginWarehousePublicId, request.DestinationWarehousePublicId }.OfType<Guid>().ToList();
        var bodegasPedidas = (await maestros.BodegasAsync(pedidas, ct)).ToDictionary(b => b.PublicId, b => b.Id);
        if (request.OriginWarehousePublicId is { } o)
        {
            var origenId = bodegasPedidas.GetValueOrDefault(o, -1);
            consulta = consulta.Where(d => d.WarehouseId == origenId);
        }
        if (request.DestinationWarehousePublicId is { } de)
        {
            var destinoId = bodegasPedidas.GetValueOrDefault(de, -1);
            consulta = consulta.Where(d => d.DestinationWarehouseId == destinoId);
        }

        var despachos = await consulta.OrderByDescending(d => d.OperationDate).ThenByDescending(d => d.Number).ThenByDescending(d => d.Id).ToListAsync(ct);
        var ids = despachos.Select(d => d.Id).ToList();
        var consumos = await traslados.ConsumosAsync(ids, ct);
        var recepciones = VistaDeTraslados.Recepciones(consumos);
        var pendientes = (await db.TransferDiscrepancies.AsNoTracking().Where(x => ids.Contains(x.DispatchDocumentId) && x.ResolvedAt == null)
                .GroupBy(x => x.DispatchDocumentId).Select(g => new { g.Key, Cantidad = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Cantidad);

        var conEstado = despachos
            .Select(d => (Despacho: d, Estado: VistaDeTraslados.EstadoDe(d, recepciones.ContainsKey(d.Id), pendientes.GetValueOrDefault(d.Id))))
            .Where(x => request.State is null || x.Estado == request.State)
            .ToList();
        var enLaPagina = conEstado.Skip((pagina - 1) * tamano).Take(tamano).ToList();

        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);
        var bodegas = (await maestros.BodegasPorIdAsync(enLaPagina.SelectMany(x => new[] { x.Despacho.WarehouseId, x.Despacho.DestinationWarehouseId, x.Despacho.TransitWarehouseId })
                .OfType<int>().Distinct().ToList(), ct)).ToDictionary(b => b.Id, b => new ReferenciaDto(b.PublicId, b.Code, b.Name));
        var items = enLaPagina.Select(x =>
        {
            var d = x.Despacho;
            var recibido = consumos.Where(c => c.DespachoId == d.Id && c.Clase == DocumentClass.TransferReceipt && c.Kind == DocumentLinkKind.ReceiptOf
                                               && c.Estado == DocumentStatus.Confirmed).Sum(c => c.QuantityBase);
            return new TransferSummaryDto(d.PublicId, VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number), x.Estado, d.OperationDate,
                Ref(d.WarehouseId), Ref(d.DestinationWarehouseId), Ref(d.TransitWarehouseId),
                d.Lines.Count(l => !l.IsDeleted), d.Lines.Where(l => !l.IsDeleted).Sum(l => l.QuantityBase), recibido,
                pendientes.GetValueOrDefault(d.Id), costos ? d.CostTotal : null);
        }).ToList();
        return Result.Success(new PagedResult<TransferSummaryDto>(items, pagina, tamano, conEstado.Count));

        ReferenciaDto? Ref(int? id) => id is int b && bodegas.TryGetValue(b, out var r) ? r : null;
    }
}

// ---------------------------------------------------------------------------------------------- detalle --

/// <summary>
/// <c>GET /api/inventory/transfers/{id}</c> (T374; §11): el despacho y sus recepciones como documentos, lo despachado, recibido,
/// faltante, sobrante y pendiente por línea, y las diferencias. Visible desde el origen o el destino (si no, 404). (nuevo)
/// </summary>
public sealed record GetTransferQuery(Guid DispatchPublicId) : IRequest<Result<TransferDto>>;

public sealed class GetTransferQueryValidator : AbstractValidator<GetTransferQuery>
{
    public GetTransferQueryValidator() => RuleFor(x => x.DispatchPublicId).NotEmpty();
}

/// <summary>El alcance lo aplica <see cref="VistaDeDocumentos.BuscarAsync"/> con <see cref="IAlcanceDeInventario"/>.</summary>
public sealed class GetTransferQueryHandler(
    IApplicationDbContext db,
    VistaDeDocumentos vista,
    VistaDeTraslados traslados)
    : IRequestHandler<GetTransferQuery, Result<TransferDto>>
{
    public async Task<Result<TransferDto>> Handle(GetTransferQuery request, CancellationToken ct)
    {
        var despacho = await vista.BuscarAsync(request.DispatchPublicId, DocumentClassGroup.Transfers, seguir: false, ct);
        if (despacho is null || despacho.Class != DocumentClass.TransferDispatch) return Result.Failure<TransferDto>(InventoryErrors.DocumentNotFound());

        var consumos = await traslados.ConsumosAsync([despacho.Id], ct);
        var recepcionIds = consumos.Where(c => c.Clase == DocumentClass.TransferReceipt && c.Estado != DocumentStatus.Discarded)
            .Select(c => c.DocumentoId).Distinct().ToList();
        var recepciones = await db.InventoryDocuments.AsNoTracking().Include(d => d.Lines).Include(d => d.DocumentType!).ThenInclude(t => t.Warehouses)
            .Where(d => recepcionIds.Contains(d.Id)).OrderBy(d => d.Id).ToListAsync(ct);
        var diferencias = await db.TransferDiscrepancies.AsNoTracking().Where(x => x.DispatchDocumentId == despacho.Id).OrderBy(x => x.Id).ToListAsync(ct);

        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => despacho.Lines.Select(l => l.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new ReferenciaDto(p.PublicId, p.Code, p.Name), ct);
        var unidades = await db.UnitsOfMeasure.AsNoTracking().IgnoreQueryFilters().Where(u => despacho.Lines.Select(l => l.UnitId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UnidadDto(u.PublicId, u.Code), ct);

        var lineas = despacho.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).Select(l =>
        {
            var deLinea = consumos.Where(c => c.LineaDeDespachoId == l.Id).ToList();
            var recibido = deLinea.Where(c => c.Clase == DocumentClass.TransferReceipt && c.Kind == DocumentLinkKind.ReceiptOf && c.Estado == DocumentStatus.Confirmed).Sum(c => c.QuantityBase);
            var salio = despacho.Status == DocumentStatus.Confirmed
                ? deLinea.Where(c => c.Estado == DocumentStatus.Confirmed).Sum(c => c.QuantityBase)
                : l.QuantityBase;
            return new TransferLineDto(l.PublicId,
                productos.GetValueOrDefault(l.ProductId) ?? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty),
                unidades.GetValueOrDefault(l.UnitId) ?? new UnidadDto(Guid.Empty, string.Empty),
                l.QuantityBase, recibido,
                diferencias.Where(x => x.DispatchLineId == l.Id && x.Kind == TransferDiscrepancyKind.Shortage).Sum(x => x.QuantityBase),
                diferencias.Where(x => x.DispatchLineId == l.Id && x.Kind == TransferDiscrepancyKind.Surplus).Sum(x => x.QuantityBase),
                l.QuantityBase - salio);
        }).ToList();

        var detalleDespacho = await vista.DetalleAsync(despacho, [], ct);
        var detalleRecepciones = new List<InventoryDocumentDto>(recepciones.Count);
        foreach (var r in recepciones) detalleRecepciones.Add(await vista.DetalleAsync(r, [], ct));
        return Result.Success(new TransferDto(detalleDespacho, detalleRecepciones, lineas, await traslados.DiferenciasAsync(diferencias, ct)));
    }
}

// ------------------------------------------------------------------------------------------- diferencias --

/// <summary>
/// <c>GET /api/inventory/transfers/discrepancies</c> (T374; §11): los faltantes y sobrantes de los traslados del alcance (origen o
/// destino), por estado (<c>Pending</c>, <c>InApproval</c>, <c>Resolved</c>) y bodega; los más recientes primero. (nuevo)
/// </summary>
public sealed record ListTransferDiscrepanciesQuery(string? State = null, Guid? WarehousePublicId = null, PageRequest? Pagina = null)
    : IRequest<Result<PagedResult<TransferDiscrepancyDto>>>;

public sealed class ListTransferDiscrepanciesQueryValidator : AbstractValidator<ListTransferDiscrepanciesQuery>
{
    private static readonly string[] Estados = [TransferDiscrepancy.EstadoPendiente, TransferDiscrepancy.EstadoEnAprobacion, TransferDiscrepancy.EstadoResuelta];

    public ListTransferDiscrepanciesQueryValidator() =>
        RuleFor(x => x.State).Must(s => s is null || Estados.Contains(s)).WithMessage($"El estado es uno de: {string.Join(", ", Estados)}.");
}

public sealed class ListTransferDiscrepanciesQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IMaestrosDelDocumento maestros,
    VistaDeTraslados traslados)
    : IRequestHandler<ListTransferDiscrepanciesQuery, Result<PagedResult<TransferDiscrepancyDto>>>
{
    public async Task<Result<PagedResult<TransferDiscrepancyDto>>> Handle(ListTransferDiscrepanciesQuery request, CancellationToken ct)
    {
        var pagina = (request.Pagina ?? new PageRequest()).SafePage;
        var tamano = (request.Pagina ?? new PageRequest()).SafePageSize;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var despachos = db.InventoryDocuments.AsNoTracking().Where(d => d.Class == DocumentClass.TransferDispatch)
            .DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());
        if (request.WarehousePublicId is { } b)
        {
            var bodega = (await maestros.BodegasAsync([b], ct)).FirstOrDefault();
            if (bodega is null) return Result.Success(new PagedResult<TransferDiscrepancyDto>([], pagina, tamano, 0));
            despachos = despachos.Where(d => d.WarehouseId == bodega.Id || d.DestinationWarehouseId == bodega.Id);
        }
        var consulta = db.TransferDiscrepancies.AsNoTracking().Where(x => despachos.Any(d => d.Id == x.DispatchDocumentId));
        consulta = request.State switch
        {
            TransferDiscrepancy.EstadoResuelta => consulta.Where(x => x.ResolvedAt != null),
            TransferDiscrepancy.EstadoEnAprobacion => consulta.Where(x => x.ResolvedAt == null && x.Resolution != null),
            TransferDiscrepancy.EstadoPendiente => consulta.Where(x => x.ResolvedAt == null && x.Resolution == null),
            _ => consulta,
        };

        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        return Result.Success(new PagedResult<TransferDiscrepancyDto>(await traslados.DiferenciasAsync(filas, ct), pagina, tamano, total));
    }
}
