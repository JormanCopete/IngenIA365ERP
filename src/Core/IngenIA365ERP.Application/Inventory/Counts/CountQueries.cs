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

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// El detalle de un conteo tal como lo ve quien pregunta (feature 012, US11, T399; contracts/api.md §12, <c>PhysicalCountDto</c>): el
/// criterio, los contadores, la foto con lo contado por ronda, la diferencia y si falta el reconteo, y sus ajustes. Dos reglas de
/// visibilidad: en un conteo <b>ciego</b>, el teórico y la diferencia salen nulos para quien sólo captura (sin
/// <c>Inventory.Counts.Open</c> ni <c>.Close</c>); los valores exigen <c>Inventory.Costs.Read</c>. (nuevo)
/// </summary>
public sealed class DetalleDeConteo(IApplicationDbContext db, VistaDeConteos conteos, VistaDeDocumentos vista)
{
    public const string PermisoDeAbrir = "Inventory.Counts.Open";
    public const string PermisoDeCerrar = "Inventory.Counts.Close";

    /// <summary>¿Ve el teórico? Siempre si el conteo no es ciego; si lo es, sólo quien lo abre o lo cierra.</summary>
    public async Task<bool> VeTeoricoAsync(InventoryDocument conteo, CancellationToken ct) =>
        !conteo.IsBlindCount || await vista.TieneAsync(PermisoDeAbrir, ct) || await vista.TieneAsync(PermisoDeCerrar, ct);

    public async Task<PhysicalCountDto> DetalleAsync(InventoryDocument conteo, CancellationToken ct)
    {
        var criterio = CriterioDelConteo.De(conteo.CountScopeJson);
        var veTeorico = await VeTeoricoAsync(conteo, ct);
        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);

        var lineas = await db.CountSnapshotLines.AsNoTracking().Where(l => l.DocumentId == conteo.Id).OrderBy(l => l.Id).ToListAsync(ct);
        var comparadas = lineas.Count == 0
            ? []
            : (await conteos.CompararAsync(conteo, lineas, ct)) is { IsSuccess: true } r ? r.Value.ToDictionary(c => c.Linea) : [];

        var productoIds = lineas.Select(l => l.ProductId).Concat(criterio.Productos).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.Code, p.Name, p.BaseUnitId }).ToDictionaryAsync(p => p.Id, ct);
        var unidadIds = productos.Values.Select(p => p.BaseUnitId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.AsNoTracking().IgnoreQueryFilters().Where(u => unidadIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UnidadDto(u.PublicId, u.Code), ct);
        var ubicacionIds = lineas.Select(l => l.LocationId).Concat(criterio.Ubicaciones).Distinct().ToList();
        var ubicaciones = await db.WarehouseLocations.AsNoTracking().IgnoreQueryFilters().Where(u => ubicacionIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new ReferenciaDto(u.PublicId, u.Code, u.Name), ct);
        var categorias = await db.ProductCategories.AsNoTracking().IgnoreQueryFilters().Where(c => criterio.Categorias.Contains(c.Id))
            .Select(c => new ReferenciaDto(c.PublicId, c.Code, c.Name)).ToListAsync(ct);
        var usuarioIds = criterio.Contadores.Concat(criterio.AbiertoPor is int a ? [a] : []).Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarioIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UsuarioDto(u.PublicId, u.Username), ct);
        var bodega = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => w.Id == conteo.WarehouseId)
            .Select(w => new ReferenciaDto(w.PublicId, w.Code, w.Name)).FirstOrDefaultAsync(ct) ?? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty);
        var tipo = conteo.DocumentType ?? await db.InventoryDocumentTypes.AsNoTracking().FirstAsync(t => t.Id == conteo.DocumentTypeId, ct);

        var cerrado = conteo.Status is DocumentStatus.Confirmed or DocumentStatus.Voided;
        var lineasDto = lineas.Select(l =>
        {
            var c = comparadas.GetValueOrDefault(l.Id);
            var producto = productos.GetValueOrDefault(l.ProductId);
            var contado = cerrado ? l.CountedQuantity ?? 0m : c?.Contado ?? 0m;
            decimal? diferencia = cerrado ? l.Difference : c?.Diferencia;
            decimal? teorico = cerrado ? l.TheoreticalQuantity + (l.MovementsAfterSnapshot ?? 0m) : c?.Teorico ?? l.TheoreticalQuantity;
            return new CountLineDto(
                producto is null ? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty) : new ReferenciaDto(producto.PublicId, producto.Code, producto.Name),
                producto is not null && unidades.TryGetValue(producto.BaseUnitId, out var u) ? u : new UnidadDto(Guid.Empty, string.Empty),
                ubicaciones.GetValueOrDefault(l.LocationId) ?? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty),
                null,
                veTeorico ? teorico : null,
                veTeorico ? (cerrado ? l.MovementsAfterSnapshot : c?.MovimientosPosteriores) : null,
                c?.ContadoPorRonda.TryGetValue(1, out var r1) == true ? r1 : null,
                c?.ContadoPorRonda.TryGetValue(2, out var r2) == true ? r2 : null,
                contado,
                veTeorico ? diferencia : null,
                costos && veTeorico ? l.SnapshotUnitCost : null,
                costos && veTeorico && diferencia is { } d ? Math.Round(d * l.SnapshotUnitCost, 2, MidpointRounding.AwayFromZero) : null,
                !cerrado && (c?.RequiereReconteo ?? false),
                l.AddedDuringCapture);
        }).ToList();

        var ajustes = await conteos.AjustesAsync(conteo.Id, ct);
        var conDiferencias = cerrado ? lineas.Any(l => (l.Difference ?? 0m) != 0m) : lineasDto.Any(l => l.Difference is { } d && d != 0m);
        var ajustesIds = ajustes.Select(x => x.PublicId).ToList();
        var solicitudes = (await db.ApprovalRequests.AsNoTracking()
                .Where(s => s.SourceType == ApprovalSourceTypes.InventoryDocument && ajustesIds.Contains(s.SourcePublicId) && s.Status == ApprovalRequestStatus.Pending)
                .Select(s => new { s.SourcePublicId, s.PublicId }).ToListAsync(ct))
            .GroupBy(s => s.SourcePublicId).ToDictionary(g => g.Key, g => g.First().PublicId);

        return new PhysicalCountDto(
            conteo.PublicId,
            VistaDeDocumentos.NumeroVisible(conteo.Prefix, conteo.Number),
            VistaDeConteos.EstadoDe(conteo, ajustes, conDiferencias),
            new ReferenciaDto(tipo.PublicId, tipo.Code, tipo.Name),
            conteo.CountKind ?? CountKind.Cyclic,
            conteo.CountScope ?? CountScope.All,
            bodega,
            new CountCriteriaDto(
                categorias,
                criterio.Ubicaciones.Select(u => ubicaciones.GetValueOrDefault(u)).OfType<ReferenciaDto>().ToList(),
                criterio.Productos.Select(p => productos.GetValueOrDefault(p)).Where(p => p is not null)
                    .Select(p => new ReferenciaDto(p!.PublicId, p.Code, p.Name)).ToList(),
                criterio.ClaseAbc),
            conteo.IsBlindCount,
            criterio.BloqueaMovimientos ?? false,
            conteo.OperationDate,
            conteo.CountSnapshotAt,
            conteo.CountSnapshotAt is null ? null : conteo.OperationDate,
            conteo.CountRound ?? 0,
            criterio.AbiertoPor is int abrio ? usuarios.GetValueOrDefault(abrio) : null,
            criterio.Contadores.Select(c => usuarios.GetValueOrDefault(c)).OfType<UsuarioDto>().ToList(),
            lineasDto,
            ajustes.Select(x => new CountAdjustmentRefDto(x.PublicId, x.Class, VistaDeDocumentos.NumeroVisible(x.Prefix, x.Number), x.Status,
                x.OperationDate, solicitudes.TryGetValue(x.PublicId, out var s) ? s : null)).ToList(),
            conteo.Notes,
            veTeorico,
            conteo.RowVersion);
    }
}

// ------------------------------------------------------------------------------------------------ lista --

/// <summary>
/// <c>GET /api/inventory/counts</c> (feature 012, US11, T399; contracts/api.md §12): los conteos (sin descartados salvo que se pidan)
/// de las bodegas del alcance, con su estado derivado, cuántas líneas y cuántas con diferencia, y el valor de la diferencia (con
/// <c>Inventory.Costs.Read</c>). Orden: fecha y creación, descendente. (nuevo)
/// </summary>
public sealed record ListPhysicalCountsQuery(
    string? State = null,
    Guid? WarehousePublicId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    PageRequest? Pagina = null) : IRequest<Result<PagedResult<CountSummaryDto>>>;

public sealed class ListPhysicalCountsQueryValidator : AbstractValidator<ListPhysicalCountsQuery>
{
    public ListPhysicalCountsQueryValidator()
    {
        RuleFor(x => x.State).Must(s => s is null || EstadosDeConteo.Todos.Contains(s))
            .WithMessage($"El estado es uno de: {string.Join(", ", EstadosDeConteo.Todos)}.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithMessage("La fecha inicial es posterior a la final.");
    }
}

public sealed class ListPhysicalCountsQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IMaestrosDelDocumento maestros,
    VistaDeDocumentos vista)
    : IRequestHandler<ListPhysicalCountsQuery, Result<PagedResult<CountSummaryDto>>>
{
    public async Task<Result<PagedResult<CountSummaryDto>>> Handle(ListPhysicalCountsQuery request, CancellationToken ct)
    {
        var pagina = (request.Pagina ?? new PageRequest()).SafePage;
        var tamano = (request.Pagina ?? new PageRequest()).SafePageSize;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var consulta = db.InventoryDocuments.AsNoTracking().Where(d => d.Class == DocumentClass.PhysicalCount)
            .DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());
        if (request.State != EstadosDeConteo.Descartado) consulta = consulta.Where(d => d.Status != DocumentStatus.Discarded);
        if (request.From is { } desde) consulta = consulta.Where(d => d.OperationDate >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(d => d.OperationDate <= hasta);
        if (request.WarehousePublicId is { } b)
        {
            var bodegaId = (await maestros.BodegasAsync([b], ct)).FirstOrDefault()?.Id ?? -1;
            consulta = consulta.Where(d => d.WarehouseId == bodegaId);
        }

        var conteos = await consulta.OrderByDescending(d => d.OperationDate).ThenByDescending(d => d.Id).ToListAsync(ct);
        var ids = conteos.Select(d => d.Id).ToList();
        var ajustes = (await db.DocumentLinks.AsNoTracking()
                .Where(l => ids.Contains(l.SourceDocumentId) && l.Kind == DocumentLinkKind.CountAdjustmentOf)
                .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => new { l.SourceDocumentId, Ajuste = d })
                .Where(x => x.Ajuste.Status != DocumentStatus.Discarded)
                .ToListAsync(ct))
            .ToLookup(x => x.SourceDocumentId, x => x.Ajuste);
        var lineas = (await db.CountSnapshotLines.AsNoTracking().Where(l => ids.Contains(l.DocumentId))
                .Select(l => new { l.DocumentId, l.Difference, l.SnapshotUnitCost }).ToListAsync(ct))
            .ToLookup(l => l.DocumentId);

        var conEstado = conteos
            .Select(d => (Conteo: d, Estado: VistaDeConteos.EstadoDe(d, ajustes[d.Id].ToList(), lineas[d.Id].Any(l => (l.Difference ?? 0m) != 0m))))
            .Where(x => request.State is null || x.Estado == request.State)
            .ToList();
        var enLaPagina = conEstado.Skip((pagina - 1) * tamano).Take(tamano).ToList();

        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);
        var bodegas = (await maestros.BodegasPorIdAsync(enLaPagina.Select(x => x.Conteo.WarehouseId).OfType<int>().Distinct().ToList(), ct))
            .ToDictionary(w => w.Id, w => new ReferenciaDto(w.PublicId, w.Code, w.Name));
        var items = enLaPagina.Select(x =>
        {
            var d = x.Conteo;
            var suyas = lineas[d.Id].ToList();
            var cerrado = d.Status is DocumentStatus.Confirmed or DocumentStatus.Voided;
            return new CountSummaryDto(d.PublicId, VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number), x.Estado,
                d.CountKind ?? CountKind.Cyclic, d.CountScope ?? CountScope.All,
                d.WarehouseId is int b && bodegas.TryGetValue(b, out var r) ? r : new ReferenciaDto(Guid.Empty, string.Empty, string.Empty),
                d.OperationDate, d.CountSnapshotAt, suyas.Count,
                cerrado ? suyas.Count(l => (l.Difference ?? 0m) != 0m) : null,
                cerrado && costos ? suyas.Sum(l => Math.Round((l.Difference ?? 0m) * l.SnapshotUnitCost, 2, MidpointRounding.AwayFromZero)) : null);
        }).ToList();
        return Result.Success(new PagedResult<CountSummaryDto>(items, pagina, tamano, conEstado.Count));
    }
}

// ---------------------------------------------------------------------------------------------- detalle --

/// <summary><c>GET /api/inventory/counts/{id}</c> (T399; §12): el conteo con sus reglas de conteo ciego y de costos. (nuevo)</summary>
public sealed record GetPhysicalCountQuery(Guid CountPublicId) : IRequest<Result<PhysicalCountDto>>;

public sealed class GetPhysicalCountQueryValidator : AbstractValidator<GetPhysicalCountQuery>
{
    public GetPhysicalCountQueryValidator() => RuleFor(x => x.CountPublicId).NotEmpty();
}

/// <summary>El alcance lo aplica <see cref="VistaDeConteos.BuscarAsync"/> (por <see cref="IAlcanceDeInventario"/>): fuera, 404.</summary>
public sealed class GetPhysicalCountQueryHandler(VistaDeConteos conteos, DetalleDeConteo detalle)
    : IRequestHandler<GetPhysicalCountQuery, Result<PhysicalCountDto>>
{
    public async Task<Result<PhysicalCountDto>> Handle(GetPhysicalCountQuery request, CancellationToken ct)
    {
        var conteo = await conteos.BuscarAsync(request.CountPublicId, seguir: false, ct);
        return conteo is null
            ? Result.Failure<PhysicalCountDto>(ErroresDeConteos.NotFound())
            : Result.Success(await detalle.DetalleAsync(conteo, ct));
    }
}

// --------------------------------------------------------------------------------------------- capturas --

/// <summary>
/// <c>GET /api/inventory/counts/{id}/captures</c> (T399; §12): las tandas registradas, por contador y producto, las más recientes
/// primero. La cantidad va en unidad base (así se guarda: cada tanda suma sus lecturas ya convertidas). (nuevo)
/// </summary>
public sealed record ListCountCapturesQuery(Guid CountPublicId, Guid? CounterUserPublicId = null, Guid? ProductPublicId = null, PageRequest? Pagina = null)
    : IRequest<Result<PagedResult<CountCaptureDto>>>;

public sealed class ListCountCapturesQueryValidator : AbstractValidator<ListCountCapturesQuery>
{
    public ListCountCapturesQueryValidator() => RuleFor(x => x.CountPublicId).NotEmpty();
}

/// <summary>El alcance del conteo lo aplica <see cref="VistaDeConteos.BuscarAsync"/> (por <see cref="IAlcanceDeInventario"/>).</summary>
public sealed class ListCountCapturesQueryHandler(IApplicationDbContext db, VistaDeConteos conteos)
    : IRequestHandler<ListCountCapturesQuery, Result<PagedResult<CountCaptureDto>>>
{
    public async Task<Result<PagedResult<CountCaptureDto>>> Handle(ListCountCapturesQuery request, CancellationToken ct)
    {
        var pagina = (request.Pagina ?? new PageRequest()).SafePage;
        var tamano = (request.Pagina ?? new PageRequest()).SafePageSize;
        var conteo = await conteos.BuscarAsync(request.CountPublicId, seguir: false, ct);
        if (conteo is null) return Result.Failure<PagedResult<CountCaptureDto>>(ErroresDeConteos.NotFound());

        var consulta = db.CountCaptures.AsNoTracking().Where(c => c.DocumentId == conteo.Id)
            .Join(db.CountSnapshotLines.AsNoTracking(), c => c.SnapshotLineId, l => l.Id, (c, l) => new { c, l.ProductId, l.LocationId });
        if (request.CounterUserPublicId is { } u)
        {
            var usuario = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == u).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(x => x.c.CounterUserId == usuario);
        }
        if (request.ProductPublicId is { } p)
        {
            var producto = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(x => x.ProductId == producto);
        }

        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.c.CapturedAt).ThenByDescending(x => x.c.Id)
            .Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);

        var productoIds = filas.Select(f => f.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(x => productoIds.Contains(x.Id))
            .Select(x => new { x.Id, x.PublicId, x.Code, x.Name, x.BaseUnitId }).ToDictionaryAsync(x => x.Id, ct);
        var unidadIds = productos.Values.Select(x => x.BaseUnitId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.AsNoTracking().IgnoreQueryFilters().Where(x => unidadIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new UnidadDto(x.PublicId, x.Code), ct);
        var ubicacionIds = filas.Select(f => f.LocationId).Distinct().ToList();
        var ubicaciones = await db.WarehouseLocations.AsNoTracking().IgnoreQueryFilters().Where(x => ubicacionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new ReferenciaDto(x.PublicId, x.Code, x.Name), ct);
        var usuarioIds = filas.Select(f => f.c.CounterUserId).Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(x => usuarioIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new UsuarioDto(x.PublicId, x.Username), ct);

        var items = filas.Select(f =>
        {
            var producto = productos.GetValueOrDefault(f.ProductId);
            var unidad = producto is not null && unidades.TryGetValue(producto.BaseUnitId, out var un) ? un : new UnidadDto(Guid.Empty, string.Empty);
            return new CountCaptureDto(f.c.PublicId,
                usuarios.GetValueOrDefault(f.c.CounterUserId) ?? new UsuarioDto(null, f.c.CreatedBy ?? string.Empty),
                f.c.Round,
                producto is null ? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty) : new ReferenciaDto(producto.PublicId, producto.Code, producto.Name),
                unidad, f.c.Quantity, f.c.Quantity, f.c.Reads, f.c.IsCorrection,
                ubicaciones.GetValueOrDefault(f.LocationId) ?? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty),
                null, f.c.CapturedAt, f.c.CreatedAt);
        }).ToList();
        return Result.Success(new PagedResult<CountCaptureDto>(items, pagina, tamano, total));
    }
}
