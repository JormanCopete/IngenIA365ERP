using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Inventory.Tracking;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Lots;

/// <summary>
/// El estado de un lote a hoy (texto de presentación, contracts/api.md §17.1): <see cref="Vigente"/>, <see cref="ProximoAVencer"/> dentro de
/// <c>Informes.DiasProximoAVencer</c>, <see cref="Vencido"/> o <see cref="SinVencimiento"/>. (nuevo)
/// </summary>
public static class EstadosDeLote
{
    public const string Vigente = "Current";
    public const string ProximoAVencer = "ExpiringSoon";
    public const string Vencido = "Expired";
    public const string SinVencimiento = "NoExpiry";
}

/// <summary>
/// Un lote con existencia de un producto (<c>GET /api/inventory/lots</c>, T934): su código, vencimiento y fabricación, la cantidad en la
/// bodega pedida (o en las del alcance), su estado y si es el que se sugiere para la salida (el vigente que vence primero). (nuevo)
/// </summary>
public sealed record LotDto(Guid PublicId, string Code, DateOnly? ExpiryDate, DateOnly? ManufactureDate, decimal Quantity, string State, bool Suggested);

/// <summary>
/// Los lotes con existencia de un producto (feature 012, I6, T934; FR-026, US15-4; contracts/api.md §3, §17.1): por
/// <c>productPublicId</c>, en la bodega <c>warehousePublicId</c> o, sin ella, sumando las del alcance (<see cref="IAlcanceDeInventario"/>;
/// una bodega fuera del alcance es 404 <c>Inventory.Warehouse.NotFound</c>). En orden FEFO por <see cref="SelectorDeLotes.Ordenar"/> —el
/// que vence primero, los sin vencimiento al final—, sin los vencidos salvo <see cref="IncludeExpired"/>; el primero vigente es el
/// sugerido. Es lo que el selector de lote de las pantallas propone en una salida: la regla que decide al confirmar sigue siendo
/// <c>ReglasDeSeguimiento</c> y <c>RegistroDeKardex</c>. (nuevo)
/// </summary>
public sealed record ListLotsQuery(Guid ProductPublicId, Guid? WarehousePublicId = null, bool IncludeExpired = false)
    : IRequest<Result<IReadOnlyList<LotDto>>>;

public sealed class ListLotsQueryValidator : AbstractValidator<ListLotsQuery>
{
    public ListLotsQueryValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

public sealed class ListLotsQueryHandler(
    IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, ILectorDeParametros parametros, IDateTimeService reloj)
    : IRequestHandler<ListLotsQuery, Result<IReadOnlyList<LotDto>>>
{
    public async Task<Result<IReadOnlyList<LotDto>>> Handle(ListLotsQuery request, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().Where(p => p.PublicId == request.ProductPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (producto is not int productoId) return Result.Failure<IReadOnlyList<LotDto>>(ErroresDelDocumento.ProductoInexistente());

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var detalles = db.StockDetails.AsNoTracking().PorBodega(alcance, s => s.WarehouseId)
            .Where(s => s.ProductId == productoId && s.LotId != null && s.Quantity != 0m);
        if (request.WarehousePublicId is { } wp)
        {
            var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);
            if (bodega is not int b || !alcance.IncluyeBodega(b)) return Result.Failure<IReadOnlyList<LotDto>>(ErroresDeAlcance.BodegaInexistente());
            detalles = detalles.Where(s => s.WarehouseId == b);
        }

        var cantidades = await detalles.GroupBy(s => s.LotId!.Value).Select(g => new { LotId = g.Key, Cantidad = g.Sum(s => s.Quantity) }).ToListAsync(ct);
        var ids = cantidades.Where(c => c.Cantidad > 0m).Select(c => c.LotId).ToList();
        var lotes = await db.Lots.AsNoTracking().Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);

        var hoy = reloj.HoyLocal;
        var dias = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.InformesDiasProximoAVencer, hoy, ct: ct);
        var limite = hoy.AddDays(Math.Max(0, dias.IsSuccess ? dias.Value.Como<int>() : 0));

        var ordenados = SelectorDeLotes.Ordenar(
            cantidades.Where(c => lotes.ContainsKey(c.LotId)).Select(c => new LoteDisponible(c.LotId, lotes[c.LotId].Code, lotes[c.LotId].ExpiryDate, c.Cantidad)),
            hoy, PoliticaDeLoteVencido.Bloquear, admiteVencidos: request.IncludeExpired);
        var sugerido = ordenados.FirstOrDefault(o => !o.Vencido)?.Lote.LotId;

        return Result.Success<IReadOnlyList<LotDto>>(ordenados.Select(o =>
        {
            var l = lotes[o.Lote.LotId];
            var estado = l.ExpiryDate is not { } vence ? EstadosDeLote.SinVencimiento
                : o.Vencido ? EstadosDeLote.Vencido
                : vence <= limite ? EstadosDeLote.ProximoAVencer
                : EstadosDeLote.Vigente;
            return new LotDto(l.PublicId, l.Code, l.ExpiryDate, l.ManufactureDate, o.Lote.Disponible, estado, o.Lote.LotId == sugerido);
        }).ToList());
    }
}

/// <summary>Una serie de un producto (<c>GET /api/inventory/serials</c>, T934): dónde está si está en existencia y su lote. (nuevo)</summary>
public sealed record SerialDto(Guid PublicId, string SerialNumber, string? LotCode, StockWarehouseRefDto? Warehouse, StockLocationRefDto? Location, bool InStock);

/// <summary>
/// Las series de un producto (feature 012, I6, T934; FR-026, US15-5; contracts/api.md §3, §17.1): con <c>warehousePublicId</c>, las que están
/// en esa bodega (fuera del alcance, 404); sin ella, las que están en una bodega del alcance y las que ya no están en existencia (no tienen
/// bodega: la vendida o dada de baja). <see cref="InStock"/> filtra por lo uno o lo otro. La serie de una bodega fuera del alcance no se ve
/// (<see cref="IAlcanceDeInventario"/>). Por número. (nuevo)
/// </summary>
public sealed record ListSerialsQuery(Guid ProductPublicId, Guid? WarehousePublicId = null, bool? InStock = null)
    : IRequest<Result<IReadOnlyList<SerialDto>>>;

public sealed class ListSerialsQueryValidator : AbstractValidator<ListSerialsQuery>
{
    public ListSerialsQueryValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

public sealed class ListSerialsQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<ListSerialsQuery, Result<IReadOnlyList<SerialDto>>>
{
    public async Task<Result<IReadOnlyList<SerialDto>>> Handle(ListSerialsQuery request, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().Where(p => p.PublicId == request.ProductPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (producto is not int productoId) return Result.Failure<IReadOnlyList<SerialDto>>(ErroresDelDocumento.ProductoInexistente());

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var series = await db.Serials.AsNoTracking().Include(s => s.Lot).Where(s => s.ProductId == productoId).ToListAsync(ct);

        int? bodegaFiltro = null;
        if (request.WarehousePublicId is { } wp)
        {
            var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);
            if (bodega is not int b || !alcance.IncluyeBodega(b)) return Result.Failure<IReadOnlyList<SerialDto>>(ErroresDeAlcance.BodegaInexistente());
            bodegaFiltro = b;
        }

        var visibles = series.Where(s => s.InStockWarehouseId is not int w
                ? bodegaFiltro is null
                : bodegaFiltro is int f ? w == f : alcance.IncluyeBodega(w))
            .Where(s => request.InStock is not bool enExistencia || (s.InStockWarehouseId is not null) == enExistencia)
            .OrderBy(s => s.SerialNumber, StringComparer.Ordinal)
            .ToList();

        var bodegaIds = visibles.Select(s => s.InStockWarehouseId).OfType<int>().Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => bodegaIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => new StockWarehouseRefDto(w.PublicId, w.Code, w.Name, w.Behavior == WarehouseBehavior.Transit), ct);
        var ubicacionIds = visibles.Select(s => s.InStockLocationId).OfType<int>().Distinct().ToList();
        var ubicaciones = await db.WarehouseLocations.AsNoTracking().Where(l => ubicacionIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new StockLocationRefDto(l.PublicId, l.Code), ct);

        return Result.Success<IReadOnlyList<SerialDto>>(visibles.Select(s => new SerialDto(
            s.PublicId, s.SerialNumber, s.Lot?.Code,
            s.InStockWarehouseId is int w ? bodegas.GetValueOrDefault(w) : null,
            s.InStockLocationId is int l ? ubicaciones.GetValueOrDefault(l) : null,
            s.InStockWarehouseId is not null)).ToList());
    }
}
