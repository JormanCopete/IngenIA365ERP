using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Common.Text;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Kardex;

/// <summary>
/// El valor de la existencia por bodega (feature 012, T256; T18; data-model §3.4): cantidad de la bodega × promedio de su ámbito
/// de costo, con el residuo repartido por <c>Redondeo.Residuo</c> (<see cref="Redondeo.ValorPorBodega"/>), de modo que la suma
/// de las bodegas de un ámbito es el valor del ámbito. <b>Nunca</b> Σ <c>TotalCost</c> del kardex de la bodega: en ámbito
/// cooperativa eso deriva. Lo usan las consultas de existencias y las vistas de informe. (nuevo)
/// </summary>
public sealed class ValorDeExistencias(IApplicationDbContext db, ILectorDeParametros parametros, IDateTimeService reloj)
{
    /// <summary>El promedio del ámbito y el valor de cada (producto, bodega) de <paramref name="productos"/>, hoy.</summary>
    public async Task<IReadOnlyDictionary<(int ProductId, int WarehouseId), (decimal Promedio, decimal Valor)>> PorBodegaAsync(
        IReadOnlyCollection<int> productos, CancellationToken ct)
    {
        var resultado = new Dictionary<(int, int), (decimal, decimal)>();
        if (productos.Count == 0) return resultado;

        var hoy = reloj.HoyLocal;
        var ambito = await TextoAsync(ParametrosDeInventario.CosteoAmbito, "Cooperativa", hoy, ct);
        var montos = Redondeo.MontosDesde(await TextoAsync(ParametrosDeInventario.RedondeoMontos, "Centavo", hoy, ct));
        var residuo = Redondeo.ResiduoDesde(await TextoAsync(ParametrosDeInventario.RedondeoResiduo, "MayorValor", hoy, ct));
        var porBodega = ambito == "Bodega";

        var existencias = await db.StockBalances.AsNoTracking().Where(s => productos.Contains(s.ProductId)).ToListAsync(ct);
        var estados = await db.CostStates.AsNoTracking().Where(c => productos.Contains(c.ProductId)).ToListAsync(ct);

        foreach (var grupo in existencias.GroupBy(e => (e.ProductId, Ambito: porBodega ? e.WarehouseId : 0)))
        {
            var estado = estados.FirstOrDefault(c => c.ProductId == grupo.Key.ProductId && c.ScopeWarehouseId == grupo.Key.Ambito);
            var filas = grupo.OrderBy(e => e.WarehouseId).ToList();
            var promedio = estado is null ? 0m : (estado.Quantity > 0m ? estado.AverageCost : estado.LastUnitCost);
            var valores = estado is null
                ? filas.Select(_ => 0m).ToList()
                : Redondeo.ValorPorBodega(estado.Value, promedio, filas.Select(f => f.Physical).ToList(), montos, residuo);
            for (var i = 0; i < filas.Count; i++) resultado[(filas[i].ProductId, filas[i].WarehouseId)] = (promedio, valores[i]);
        }
        return resultado;
    }

    /// <summary>El ámbito de costo vigente hoy.</summary>
    public async Task<CostScope> AmbitoAsync(CancellationToken ct) =>
        await TextoAsync(ParametrosDeInventario.CosteoAmbito, "Cooperativa", reloj.HoyLocal, ct) == "Bodega" ? CostScope.Warehouse : CostScope.Cooperative;

    private async Task<string> TextoAsync(string clave, string defecto, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, fecha, ct: ct);
        return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto : defecto;
    }
}

// -------------------------------------------------------------------------------------------- GET /stock --

/// <summary>
/// <c>GET /api/inventory/stock</c> (feature 012, T256; contracts/api.md §5; FR-033): existencia física, reservada (0 hasta I6),
/// disponible y en tránsito hacia la bodega por producto y bodega, del alcance de quien pregunta. Valores sólo con
/// <c>Inventory.Costs.Read</c>; mínimo, punto de reorden, máximo y posición de <c>INV_ReorderPolicies</c> y
/// <see cref="PosicionDeReposicion"/>. (nuevo)
/// </summary>
public sealed record GetStockQuery(
    Guid? WarehousePublicId = null,
    Guid? ProductPublicId = null,
    Guid? CategoryPublicId = null,
    Guid? LocationPublicId = null,
    string? Search = null,
    bool OnlyWithStock = false,
    bool BelowReorderPoint = false,
    PageRequest? Pagina = null) : IRequest<Result<PagedResult<StockRowDto>>>;

public sealed class GetStockQueryValidator : AbstractValidator<GetStockQuery>
{
    public GetStockQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

/// <summary>Filtra por <see cref="IAlcanceDeInventario"/> (<c>FiltroDeAlcance.PorBodega</c>); una bodega fuera del alcance es 404.</summary>
public sealed class GetStockQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    PosicionDeReposicion posiciones,
    ValorDeExistencias valores)
    : IRequestHandler<GetStockQuery, Result<PagedResult<StockRowDto>>>
{
    public async Task<Result<PagedResult<StockRowDto>>> Handle(GetStockQuery request, CancellationToken ct)
    {
        var pagina = request.Pagina ?? new PageRequest();
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.StockBalances.AsNoTracking().PorBodega(alcance, s => s.WarehouseId);

        if (request.WarehousePublicId is { } wp)
        {
            var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);
            if (bodega is not int b || !alcance.IncluyeBodega(b)) return Result.Failure<PagedResult<StockRowDto>>(ErroresDeAlcance.BodegaInexistente());
            consulta = consulta.Where(s => s.WarehouseId == b);
        }
        if (request.ProductPublicId is { } pp)
        {
            var producto = await db.Products.AsNoTracking().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (producto is not int p) return Result.Failure<PagedResult<StockRowDto>>(ErroresDelDocumento.ProductoInexistente());
            consulta = consulta.Where(s => s.ProductId == p);
        }
        if (request.CategoryPublicId is { } cp)
        {
            var categoria = await db.ProductCategories.AsNoTracking().Where(c => c.PublicId == cp).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(s => db.Products.Any(p => p.Id == s.ProductId && p.CategoryId == categoria));
        }
        if (request.LocationPublicId is { } lp)
        {
            var ubicacion = await db.WarehouseLocations.AsNoTracking().Where(l => l.PublicId == lp).Select(l => new { l.Id, l.WarehouseId }).FirstOrDefaultAsync(ct);
            if (ubicacion is null || !alcance.IncluyeBodega(ubicacion.WarehouseId))
                return Result.Failure<PagedResult<StockRowDto>>(ErroresDelDocumento.UbicacionInexistente());
            consulta = consulta.Where(s => s.WarehouseId == ubicacion.WarehouseId
                && db.StockDetails.Any(d => d.ProductId == s.ProductId && d.WarehouseId == s.WarehouseId && d.LocationId == ubicacion.Id && d.Quantity != 0m));
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var termino = NormalizadorDeBusqueda.Normalizar(request.Search);
            consulta = consulta.Where(s => db.Products.Any(p => p.Id == s.ProductId && (p.SearchText.Contains(termino) || p.Code.Contains(termino))));
        }
        if (request.OnlyWithStock) consulta = consulta.Where(s => s.Physical != 0m || s.Reserved != 0m);

        var ordenada =
            from s in consulta
            join p in db.Products.AsNoTracking() on s.ProductId equals p.Id
            join w in db.Warehouses.AsNoTracking() on s.WarehouseId equals w.Id
            orderby p.Code, w.Code
            select s;

        List<StockBalance> filas;
        long total;
        if (request.BelowReorderPoint)
        {
            // Sólo lo que tiene política: la posición se calcula en memoria sobre ese conjunto.
            filas = await ordenada.Where(s => db.ReorderPolicies.Any(r => r.ProductId == s.ProductId && r.WarehouseId == s.WarehouseId)).ToListAsync(ct);
            total = 0;
        }
        else
        {
            total = await ordenada.LongCountAsync(ct);
            filas = await ordenada.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        }

        var dtos = await FilasAsync(filas, ct);
        if (request.BelowReorderPoint)
        {
            var debajo = dtos.Where(d => d.Position is { } pos && d.ReorderPoint is { } punto && pos <= punto).ToList();
            return Result.Success(new PagedResult<StockRowDto>(
                debajo.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList(), pagina.SafePage, pagina.SafePageSize, debajo.Count));
        }
        return Result.Success(new PagedResult<StockRowDto>(dtos, pagina.SafePage, pagina.SafePageSize, total));
    }

    /// <summary>Las filas con su producto, bodega, tránsito, reorden y (con permiso) valores.</summary>
    private async Task<List<StockRowDto>> FilasAsync(IReadOnlyList<StockBalance> filas, CancellationToken ct)
    {
        if (filas.Count == 0) return [];
        var productoIds = filas.Select(f => f.ProductId).Distinct().ToList();
        var bodegaIds = filas.Select(f => f.WarehouseId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = new StockProductRefDto(p.PublicId, p.Code, p.Name, p.BaseUnit != null ? p.BaseUnit.Code : string.Empty, p.Status) })
            .ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => bodegaIds.Contains(w.Id))
            .Select(w => new { w.Id, Ref = new StockWarehouseRefDto(w.PublicId, w.Code, w.Name, w.Behavior == WarehouseBehavior.Transit) })
            .ToDictionaryAsync(w => w.Id, w => w.Ref, ct);
        var politicas = await db.ReorderPolicies.AsNoTracking()
            .Where(r => productoIds.Contains(r.ProductId) && bodegaIds.Contains(r.WarehouseId))
            .ToListAsync(ct);
        var parejas = filas.Select(f => (f.ProductId, f.WarehouseId)).ToList();
        var posicion = await posiciones.LeerAsync(parejas, ct);
        var conCostos = await permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, ct);
        var valorados = conCostos ? await valores.PorBodegaAsync(productoIds, ct) : null;

        return filas.Select(f =>
        {
            var pos = posicion.GetValueOrDefault((f.ProductId, f.WarehouseId)) ?? Posicion.Cero;
            var politica = politicas.FirstOrDefault(r => r.ProductId == f.ProductId && r.WarehouseId == f.WarehouseId);
            var valor = valorados?.GetValueOrDefault((f.ProductId, f.WarehouseId));
            return new StockRowDto(
                productos[f.ProductId], bodegas[f.WarehouseId],
                f.Physical, f.Reserved, f.Physical - f.Reserved, pos.EnTransito,
                valor?.Promedio, valor?.Valor,
                politica?.MinimumQuantity, politica?.ReorderPoint, politica?.MaximumQuantity,
                politica is null ? null : pos.Valor);
        }).ToList();
    }
}

// --------------------------------------------------------------------------------- GET /stock/{productId} --

/// <summary>
/// <c>GET /api/inventory/stock/{productId}</c> (feature 012, T256; §5): el producto en las bodegas del alcance, por ubicación y
/// lote, lo que va en tránsito (despachos con origen o destino en el alcance) y el estado de costo (sólo con
/// <c>Inventory.Costs.Read</c>). La existencia propia de una bodega de tránsito, sólo si está en el alcance (total o asignada). (nuevo)
/// </summary>
public sealed record GetProductStockQuery(Guid ProductPublicId) : IRequest<Result<ProductStockDto>>;

public sealed class GetProductStockQueryValidator : AbstractValidator<GetProductStockQuery>
{
    public GetProductStockQueryValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

/// <summary>Filtra por <see cref="IAlcanceDeInventario"/>; un producto inexistente es 404 <c>Inventory.Product.NotFound</c>.</summary>
public sealed class GetProductStockQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    PosicionDeReposicion posiciones,
    ValorDeExistencias valores)
    : IRequestHandler<GetProductStockQuery, Result<ProductStockDto>>
{
    public async Task<Result<ProductStockDto>> Handle(GetProductStockQuery request, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().Include(p => p.BaseUnit).FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Result.Failure<ProductStockDto>(ErroresDelDocumento.ProductoInexistente());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var existencias = await db.StockBalances.AsNoTracking().PorBodega(alcance, s => s.WarehouseId)
            .Where(s => s.ProductId == producto.Id).ToListAsync(ct);
        var detalles = await db.StockDetails.AsNoTracking().PorBodega(alcance, s => s.WarehouseId)
            .Where(s => s.ProductId == producto.Id && s.Quantity != 0m).ToListAsync(ct);
        var transito = (await posiciones.EnTransitoAsync([producto.Id], null, ct))
            .Where(t => alcance.IncluyeBodega(t.ToWarehouseId) || alcance.IncluyeBodega(t.FromWarehouseId))
            .ToList();

        var bodegaIds = existencias.Select(e => e.WarehouseId).Concat(detalles.Select(d => d.WarehouseId))
            .Concat(transito.SelectMany(t => new[] { t.FromWarehouseId, t.ToWarehouseId })).Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => bodegaIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => new StockWarehouseRefDto(w.PublicId, w.Code, w.Name, w.Behavior == WarehouseBehavior.Transit), ct);
        var ubicacionIds = detalles.Select(d => d.LocationId).Distinct().ToList();
        var ubicaciones = await db.WarehouseLocations.AsNoTracking().Where(l => ubicacionIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new StockLocationRefDto(l.PublicId, l.Code), ct);

        var conCostos = await permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, ct);
        var valorados = conCostos ? await valores.PorBodegaAsync([producto.Id], ct) : null;
        var pos = await posiciones.LeerAsync(existencias.Select(e => (e.ProductId, e.WarehouseId)).ToList(), ct);

        var porBodega = existencias.OrderBy(e => bodegas[e.WarehouseId].Code, StringComparer.Ordinal)
            .Select(e => new ProductStockByWarehouseDto(bodegas[e.WarehouseId], e.Physical, e.Reserved, e.Physical - e.Reserved,
                pos.GetValueOrDefault((e.ProductId, e.WarehouseId))?.EnTransito ?? 0m,
                valorados?.GetValueOrDefault((e.ProductId, e.WarehouseId)).Valor))
            .ToList();
        var porUbicacion = detalles
            .OrderBy(d => bodegas[d.WarehouseId].Code, StringComparer.Ordinal).ThenBy(d => ubicaciones[d.LocationId].Code, StringComparer.Ordinal)
            .Select(d => new ProductStockByLocationDto(bodegas[d.WarehouseId], ubicaciones[d.LocationId], null, d.Quantity))
            .ToList();
        var enTransito = transito.OrderBy(t => t.DispatchedOn)
            .Select(t => new ProductStockInTransitDto(t.TransferPublicId, t.DispatchNumber, bodegas.GetValueOrDefault(t.FromWarehouseId), bodegas[t.ToWarehouseId], t.Quantity, t.DispatchedOn))
            .ToList();

        var totales = new ProductStockTotalsDto(
            porBodega.Sum(b => b.Physical), porBodega.Sum(b => b.Reserved), porBodega.Sum(b => b.Available), enTransito.Sum(t => t.Quantity),
            conCostos ? porBodega.Sum(b => b.Value ?? 0m) : null);

        ProductCostStateDto? estado = null;
        if (conCostos)
        {
            var ambito = await valores.AmbitoAsync(ct);
            var estados = await db.CostStates.AsNoTracking().Where(c => c.ProductId == producto.Id).ToListAsync(ct);
            if (ambito == CostScope.Cooperative)
            {
                if (estados.FirstOrDefault(c => c.ScopeWarehouseId == 0) is { } coop)
                    estado = new ProductCostStateDto(CostScope.Cooperative, coop.Method, coop.Quantity, coop.AverageCost, coop.LastUnitCost, coop.Value);
            }
            else
            {
                var visibles = estados.Where(c => c.ScopeWarehouseId != 0 && alcance.IncluyeBodega(c.ScopeWarehouseId)).ToList();
                if (visibles.Count > 0)
                {
                    var cantidad = visibles.Sum(c => c.Quantity);
                    var valor = visibles.Sum(c => c.Value);
                    estado = new ProductCostStateDto(CostScope.Warehouse, visibles[0].Method, cantidad,
                        cantidad > 0m ? Redondeo.CostoUnitario(valor / cantidad) : visibles.Max(c => c.LastUnitCost),
                        visibles.Max(c => c.LastUnitCost), valor);
                }
            }
        }

        var referencia = new StockProductRefDto(producto.PublicId, producto.Code, producto.Name, producto.BaseUnit?.Code ?? string.Empty, producto.Status);
        return Result.Success(new ProductStockDto(referencia, totales, porBodega, porUbicacion, enTransito, estado));
    }
}

// ------------------------------------------------------------------------ lo que pregunta el catálogo --

/// <summary>
/// La implementación real de <see cref="IExistenciasParaElCatalogo"/> (feature 012, T256) sobre <c>INV_StockBalances</c> e
/// <c>INV_StockDetails</c>: reemplaza a <see cref="ExistenciasSinKardex"/>. Una bodega o ubicación con existencia distinta de
/// cero no se inactiva (US1) y la búsqueda informa el disponible. (nuevo)
/// </summary>
public sealed class ExistenciasEnKardex(IApplicationDbContext db) : IExistenciasParaElCatalogo
{
    public async Task<ExistenciaAgregada> DeBodegaAsync(int warehouseId, CancellationToken ct)
    {
        var filas = await db.StockBalances.AsNoTracking().Where(s => s.WarehouseId == warehouseId && s.Physical != 0m)
            .Select(s => s.Physical).ToListAsync(ct);
        return filas.Count == 0 ? ExistenciaAgregada.Ninguna : new ExistenciaAgregada(filas.Count, filas.Sum());
    }

    public async Task<ExistenciaAgregada> DeUbicacionAsync(int locationId, CancellationToken ct)
    {
        var filas = await db.StockDetails.AsNoTracking().Where(s => s.LocationId == locationId && s.Quantity != 0m)
            .Select(s => new { s.ProductId, s.Quantity }).ToListAsync(ct);
        return filas.Count == 0 ? ExistenciaAgregada.Ninguna : new ExistenciaAgregada(filas.Select(f => f.ProductId).Distinct().Count(), filas.Sum(f => f.Quantity));
    }

    public async Task<IReadOnlyDictionary<int, decimal>> DisponibleAsync(IReadOnlyCollection<int> productIds, int warehouseId, CancellationToken ct) =>
        productIds.Count == 0
            ? new Dictionary<int, decimal>()
            : await db.StockBalances.AsNoTracking().Where(s => s.WarehouseId == warehouseId && productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, s => s.Physical - s.Reserved, ct);
}
