using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pos;

// Las consultas del POS (feature 012, I3, T604 y T605; contracts/api.md §20.2). Son lecturas: no llevan clave ni emiten eventos.

/// <summary>
/// El lector (<c>GET /pos/lookup?code=&amp;cashSession=</c>): búsqueda <b>exacta</b> por código de barras (el de empaque trae su unidad
/// y factor) o por código de producto, con el precio que tendría para el consumidor final en el punto y el disponible de la bodega de
/// la caja. Sin coincidencia → 404 <c>Inventory.Product.NotFound</c> y la pantalla abre la búsqueda. Punto sin POS →
/// <c>Inventory.Pos.NotEnabled</c> (T605). (nuevo)
/// </summary>
public sealed record LookupPosProductQuery(string Code, Guid CashSessionPublicId) : IRequest<Result<PosLookupDto>>;

public sealed class LookupPosProductQueryHandler(IApplicationDbContext db, BorradorDelPos pos, PrecificacionDeVenta precificacion)
    : IRequestHandler<LookupPosProductQuery, Result<PosLookupDto>>
{
    public async Task<Result<PosLookupDto>> Handle(LookupPosProductQuery request, CancellationToken ct)
    {
        var sesion = await pos.SesionAbiertaAsync(request.CashSessionPublicId, exigirPos: true, ct);
        if (sesion.IsFailure) return Result.Failure<PosLookupDto>(sesion.Error);
        var (s, caja, punto) = sesion.Value;

        var (_, codigo) = BorradorDelPos.Multiplicador(request.Code ?? string.Empty, null);
        var leido = await pos.LeerAsync(codigo, ct);
        if (leido.IsFailure) return Result.Failure<PosLookupDto>(leido.Error);
        var l = leido.Value;

        var usuario = (await pos.UsuarioAsync(ct)).Value.UserId;
        var precio = await precificacion.PrecificarAsync(new PedidoDePrecificacion(s.OperatingDate, await pos.ConsumidorFinalAsync(s.OperatingDate, ct),
            punto.SalesChannelId, punto.BranchId, caja.WarehouseId, usuario, [new LineaAPrecificar(1, l.Producto.Id, l.UnitId, 1m, l.Factor)]), ct);
        if (precio.IsFailure) return Result.Failure<PosLookupDto>(precio.Error);
        var linea = precio.Value.Lineas[0];
        var lista = linea.PriceListId is int id
            ? await db.PriceLists.AsNoTracking().Where(p => p.Id == id).Select(p => new PosRefDto(p.PublicId, p.Code, p.Name)).FirstOrDefaultAsync(ct)
            : null;
        var unidad = await db.UnitsOfMeasure.AsNoTracking().Where(u => u.Id == l.UnitId).Select(u => new PosRefDto(u.PublicId, u.Code, u.Name)).FirstAsync(ct);
        return Result.Success(new PosLookupDto(new PosRefDto(l.Producto.PublicId, l.Producto.Code, l.Producto.Name), l.Producto.Status, unidad, l.Factor,
            linea.ListPrice, lista, linea.ListPriceIncludesTaxes, linea.Available));
    }
}

/// <summary>
/// La venta (<c>GET /pos/drafts/{id}</c>): el <see cref="PosDraftDto"/> con los medios ofrecidos, las aprobaciones pendientes y los
/// avisos. En borrador, los impuestos se calculan sin guardar nada; confirmada, salen de <c>INV_DocumentTaxLines</c> con el mismo id.
/// (nuevo)
/// </summary>
public sealed record GetPosDraftQuery(Guid DraftPublicId) : IRequest<Result<PosDraftDto>>;

public sealed class GetPosDraftQueryHandler(BorradorDelPos pos) : IRequestHandler<GetPosDraftQuery, Result<PosDraftDto>>
{
    public async Task<Result<PosDraftDto>> Handle(GetPosDraftQuery request, CancellationToken ct)
    {
        var venta = await pos.VentaAsync(request.DraftPublicId, soloBorrador: false, ct);
        if (venta.IsFailure) return Result.Failure<PosDraftDto>(venta.Error);
        VentaPrecificada? precificada = null;
        if (venta.Value.Status == DocumentStatus.Draft)
        {
            var r = await pos.PrecificarAsync(venta.Value, null, resolverListas: false, null, aplicar: false, ct);
            if (r.IsSuccess) precificada = r.Value;
        }
        return Result.Success(await pos.DtoAsync(venta.Value, precificada, null, null, ct));
    }
}

/// <summary>
/// Las ventas en borrador del POS (<c>GET /pos/drafts?cashSession=&amp;pointOfSale=&amp;suspended=</c>). Con <c>suspended=true</c>, las
/// suspendidas del punto, que cualquier cajero con alcance puede recuperar. Sólo puntos del alcance del usuario. (nuevo)
/// </summary>
public sealed record ListPosDraftsQuery(Guid? CashSessionPublicId = null, Guid? PointOfSalePublicId = null, bool? Suspended = null)
    : IRequest<Result<IReadOnlyList<PosDraftSummaryDto>>>;

public sealed class ListPosDraftsQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<ListPosDraftsQuery, Result<IReadOnlyList<PosDraftSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<PosDraftSummaryDto>>> Handle(ListPosDraftsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.InventoryDocuments.AsNoTracking().Where(d => d.PointOfSaleId != null && d.Status == DocumentStatus.Draft);
        if (!alcance.TodosLosPuntos)
        {
            var puntos = alcance.Puntos.ToList();
            consulta = consulta.Where(d => puntos.Contains(d.PointOfSaleId!.Value));
        }
        if (request.PointOfSalePublicId is { } punto)
        {
            var id = await db.PointsOfSale.AsNoTracking().Where(p => p.PublicId == punto).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (id is null) return Result.Success<IReadOnlyList<PosDraftSummaryDto>>([]);
            consulta = consulta.Where(d => d.PointOfSaleId == id);
        }
        if (request.CashSessionPublicId is { } sesion)
        {
            var id = await db.CashSessions.AsNoTracking().Where(s => s.PublicId == sesion).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
            if (id is null) return Result.Success<IReadOnlyList<PosDraftSummaryDto>>([]);
            consulta = consulta.Where(d => d.CashSessionId == id);
        }
        if (request.Suspended is { } suspendida) consulta = consulta.Where(d => d.IsSuspended == suspendida);

        var filas = await consulta.OrderBy(d => d.Id)
            .Select(d => new
            {
                d.PublicId, d.CashSessionId, d.Total, d.IsSuspended, d.SuspendedLabel, d.SuspendedAt, d.UpdatedBy,
                Lineas = d.Lines.Count(l => !l.IsDeleted),
            })
            .Take(500)
            .ToListAsync(ct);
        var sesionIds = filas.Select(f => f.CashSessionId).OfType<int>().Distinct().ToList();
        var sesiones = await db.CashSessions.AsNoTracking().Where(s => sesionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.PublicId, s.CashierName }, ct);
        return Result.Success<IReadOnlyList<PosDraftSummaryDto>>(filas.Select(f =>
        {
            var s = f.CashSessionId is int sid ? sesiones.GetValueOrDefault(sid) : null;
            return new PosDraftSummaryDto(f.PublicId, s?.PublicId, s?.CashierName, f.Lineas, f.Total,
                f.IsSuspended ? new PosSuspendedDto(f.SuspendedLabel, f.SuspendedAt, f.UpdatedBy) : null);
        }).ToList());
    }
}
