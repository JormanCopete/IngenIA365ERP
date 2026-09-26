using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Inventory.GoLive;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.GoLive;

/// <summary>
/// Los lotes de cifras de SOLIDO (feature 012, T312; api.md §13.2 <c>GET /api/inventory/legacy-figures?asOf=&amp;warehouseCode=</c>):
/// uno por (lote, fecha, bodega), con filas, resueltas, sin resolver, cantidad, valor, quién y cuándo, y si otro lote posterior
/// del mismo par lo dejó de baja (<c>superseded</c>). Sólo las bodegas del alcance. (nuevo)
/// </summary>
public sealed record ListLegacyFigureBatchesQuery(DateOnly? AsOf = null, string? WarehouseCode = null) : IRequest<Result<IReadOnlyList<LoteDeCifrasDto>>>;

/// <summary>
/// Las filas de un lote (T312; <c>GET /{batchId}/rows?unresolvedOnly=&amp;page=&amp;pageSize=</c>), paginadas, con los códigos
/// crudos y los Id resueltos. (nuevo)
/// </summary>
public sealed record ListLegacyFigureRowsQuery(Guid BatchPublicId, bool UnresolvedOnly = false, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<FilaDeCifraDto>>>;

public sealed record LoteDeCifrasDto(
    Guid BatchPublicId,
    DateOnly AsOf,
    string WarehouseCode,
    Guid? WarehousePublicId,
    int Rows,
    int ResolvedRows,
    int UnresolvedRows,
    decimal Quantity,
    decimal Value,
    DateTime ImportedAt,
    string? ImportedBy,
    bool Superseded,
    string SourceFileName);

public sealed record FilaDeCifraDto(
    string WarehouseCode,
    string ProductCode,
    Guid? WarehousePublicId,
    Guid? ProductPublicId,
    string? AccountingGroupCode,
    DateOnly AsOf,
    decimal? Quantity,
    decimal? Value);

public sealed class ListLegacyFigureBatchesQueryValidator : AbstractValidator<ListLegacyFigureBatchesQuery>
{
    public ListLegacyFigureBatchesQueryValidator() =>
        RuleFor(x => x.WarehouseCode).MaximumLength(LegacyFigure.LargoDelCodigoDeBodega);
}

public sealed class ListLegacyFigureRowsQueryValidator : AbstractValidator<ListLegacyFigureRowsQuery>
{
    public ListLegacyFigureRowsQueryValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}

public sealed class ListLegacyFigureBatchesQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<ListLegacyFigureBatchesQuery, Result<IReadOnlyList<LoteDeCifrasDto>>>
{
    public async Task<Result<IReadOnlyList<LoteDeCifrasDto>>> Handle(ListLegacyFigureBatchesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.LegacyFigures.AsNoTracking().IgnoreQueryFilters();
        if (request.AsOf is { } fecha) consulta = consulta.Where(x => x.AsOfDate == fecha);
        if (!string.IsNullOrWhiteSpace(request.WarehouseCode))
        {
            var codigo = request.WarehouseCode.Trim().ToUpperInvariant();
            consulta = consulta.Where(x => x.WarehouseCodeRaw == codigo);
        }

        var filas = await consulta
            .Select(x => new { x.ImportBatchPublicId, x.AsOfDate, x.WarehouseCodeRaw, x.WarehouseId, x.ProductId, x.Quantity, x.Value, x.CreatedAt, x.CreatedBy, x.IsDeleted, x.SourceFileName })
            .ToListAsync(ct);
        filas = filas.Where(x => x.WarehouseId is int w && alcance.IncluyeBodega(w)).ToList();

        var bodegaIds = filas.Select(x => x.WarehouseId!.Value).Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => bodegaIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.PublicId, ct);

        IReadOnlyList<LoteDeCifrasDto> lotes = filas
            .GroupBy(x => (x.ImportBatchPublicId, x.AsOfDate, x.WarehouseCodeRaw, x.WarehouseId))
            .Select(g => new LoteDeCifrasDto(
                g.Key.ImportBatchPublicId, g.Key.AsOfDate, g.Key.WarehouseCodeRaw, bodegas.GetValueOrDefault(g.Key.WarehouseId!.Value),
                g.Count(), g.Count(x => x.ProductId != null), g.Count(x => x.ProductId == null),
                g.Sum(x => x.Quantity ?? 0m), g.Sum(x => x.Value ?? 0m),
                g.Min(x => x.CreatedAt), g.First().CreatedBy, g.All(x => x.IsDeleted), g.First().SourceFileName))
            .OrderByDescending(l => l.AsOf).ThenBy(l => l.WarehouseCode, StringComparer.Ordinal).ThenByDescending(l => l.ImportedAt)
            .ToList();
        return Result.Success(lotes);
    }
}

public sealed class ListLegacyFigureRowsQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<ListLegacyFigureRowsQuery, Result<PagedResult<FilaDeCifraDto>>>
{
    public async Task<Result<PagedResult<FilaDeCifraDto>>> Handle(ListLegacyFigureRowsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var filas = (await db.LegacyFigures.AsNoTracking().IgnoreQueryFilters()
                .Where(x => x.ImportBatchPublicId == request.BatchPublicId && (!request.UnresolvedOnly || x.ProductId == null))
                .ToListAsync(ct))
            .Where(x => x.WarehouseId is int w && alcance.IncluyeBodega(w))
            .OrderBy(x => x.AsOfDate).ThenBy(x => x.WarehouseCodeRaw, StringComparer.Ordinal).ThenBy(x => x.ProductCodeRaw, StringComparer.Ordinal)
            .ToList();
        if (filas.Count == 0 && !await db.LegacyFigures.IgnoreQueryFilters().AnyAsync(x => x.ImportBatchPublicId == request.BatchPublicId, ct))
            return Result.Failure<PagedResult<FilaDeCifraDto>>(Error.NotFound);

        var pagina = new PageRequest(request.Page, request.PageSize);
        var trozo = filas.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();
        var bodegaIds = trozo.Select(x => x.WarehouseId).OfType<int>().Distinct().ToList();
        var productoIds = trozo.Select(x => x.ProductId).OfType<int>().Distinct().ToList();
        var grupoIds = trozo.Select(x => x.AccountingGroupId).OfType<int>().Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => bodegaIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.PublicId, ct);
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var grupos = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().Where(g => grupoIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Code, ct);

        var items = trozo.Select(x => new FilaDeCifraDto(
                x.WarehouseCodeRaw, x.ProductCodeRaw,
                x.WarehouseId is int w ? bodegas.GetValueOrDefault(w) : null,
                x.ProductId is int p ? productos.GetValueOrDefault(p) : null,
                x.AccountingGroupId is int g ? grupos.GetValueOrDefault(g) : null,
                x.AsOfDate, x.Quantity, x.Value))
            .ToList();
        return Result.Success(new PagedResult<FilaDeCifraDto>(items, pagina.SafePage, pagina.SafePageSize, filas.Count));
    }
}
