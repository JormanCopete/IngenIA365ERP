using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Common.Text;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// La lista de productos (feature 012, T220; contracts/api.md §3.5, <c>GET /api/inventory/products</c>), paginada y por
/// código. <see cref="CategoryPublicId"/> incluye las subcategorías (por la ruta materializada); <see cref="Search"/> busca
/// cada término en el texto de búsqueda, sin tildes ni mayúsculas. (nuevo)
/// </summary>
public sealed record ListProductsQuery(
    string? Search = null,
    Guid? CategoryPublicId = null,
    Guid? BrandPublicId = null,
    Guid? AccountingGroupPublicId = null,
    ProductKind? Kind = null,
    ProductStatus? Status = null,
    PageRequest? Pagina = null)
    : IRequest<Result<PagedResult<ProductListItemDto>>>;

public sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsQueryValidator() => RuleFor(x => x.Search).MaximumLength(100);
}

public sealed class ListProductsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListProductsQuery, Result<PagedResult<ProductListItemDto>>>
{
    public async Task<Result<PagedResult<ProductListItemDto>>> Handle(ListProductsQuery request, CancellationToken ct)
    {
        var pagina = request.Pagina ?? new PageRequest();
        var consulta = db.Products.AsNoTracking();

        if (request.CategoryPublicId is { } cp)
        {
            var ruta = await db.ProductCategories.Where(c => c.PublicId == cp).Select(c => c.Path).FirstOrDefaultAsync(ct);
            if (ruta is null) return Result.Success(new PagedResult<ProductListItemDto>([], pagina.SafePage, pagina.SafePageSize, 0));
            consulta = consulta.Where(p => db.ProductCategories.Any(c => c.Id == p.CategoryId && c.Path.StartsWith(ruta)));
        }
        if (request.BrandPublicId is { } bp) consulta = consulta.Where(p => p.Brand!.PublicId == bp);
        if (request.AccountingGroupPublicId is { } gp) consulta = consulta.Where(p => p.AccountingGroup!.PublicId == gp);
        if (request.Kind is { } k) consulta = consulta.Where(p => p.Kind == k);
        if (request.Status is { } s) consulta = consulta.Where(p => p.Status == s);
        consulta = BusquedaDeProductos.Contiene(consulta, request.Search);

        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderBy(p => p.Code)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .Select(p => new
            {
                p.PublicId, p.Code, p.Name, p.Kind, p.Status,
                Categoria = new CatalogRefDto(p.Category!.PublicId, p.Category.Code, p.Category.Name),
                Marca = p.Brand == null ? null : new CatalogRefDto(p.Brand.PublicId, p.Brand.Code, p.Brand.Name),
                Base = p.BaseUnit!.Code,
                Grupo = p.AccountingGroup == null ? null : p.AccountingGroup.Code,
                Principal = db.ProductBarcodes.Where(b => b.ProductId == p.Id && b.IsPrimary).Select(b => b.Barcode).FirstOrDefault(),
                Movimientos = db.InventoryDocumentLines.Any(l => l.ProductId == p.Id),
            })
            .ToListAsync(ct);

        var items = filas.Select(f => new ProductListItemDto(f.PublicId, f.Code, f.Name, f.Kind, f.Status, f.Categoria, f.Marca, f.Base, f.Grupo,
            f.Principal, f.Movimientos)).ToList();
        return Result.Success(new PagedResult<ProductListItemDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Un producto con todo lo que cuelga de él (T220; §3.5, <c>GET /products/{id}</c>). (nuevo)</summary>
public sealed record GetProductQuery(Guid ProductPublicId) : IRequest<Result<ProductDto>>;

public sealed class GetProductQueryValidator : AbstractValidator<GetProductQuery>
{
    public GetProductQueryValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

public sealed class GetProductQueryHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<GetProductQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(GetProductQuery request, CancellationToken ct)
    {
        var id = await db.Products.Where(p => p.PublicId == request.ProductPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return id is null
            ? Result.Failure<ProductDto>(CatalogErrors.ProductNotFound())
            : Result.Success(await VistaDeProductos.ArmarAsync(db, id.Value, reloj.HoyLocal, ct));
    }
}

/// <summary>Las unidades alternas de un producto (T219; §3.6.1, <c>GET /products/{id}/units</c>). (nuevo)</summary>
public sealed record ListProductUnitsQuery(Guid ProductPublicId) : IRequest<Result<IReadOnlyList<ProductUnitDto>>>;

public sealed class ListProductUnitsQueryValidator : AbstractValidator<ListProductUnitsQuery>
{
    public ListProductUnitsQueryValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

public sealed class ListProductUnitsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListProductUnitsQuery, Result<IReadOnlyList<ProductUnitDto>>>
{
    public async Task<Result<IReadOnlyList<ProductUnitDto>>> Handle(ListProductUnitsQuery request, CancellationToken ct)
    {
        var id = await db.Products.Where(p => p.PublicId == request.ProductPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return id is null
            ? Result.Failure<IReadOnlyList<ProductUnitDto>>(CatalogErrors.ProductNotFound())
            : Result.Success(await VistaDeProductos.UnidadesAsync(db, id.Value, ct));
    }
}

/// <summary>Los códigos de barras vivos de un producto (T219; §3.6.2, <c>GET /products/{id}/barcodes</c>). (nuevo)</summary>
public sealed record ListProductBarcodesQuery(Guid ProductPublicId) : IRequest<Result<IReadOnlyList<ProductBarcodeDto>>>;

public sealed class ListProductBarcodesQueryValidator : AbstractValidator<ListProductBarcodesQuery>
{
    public ListProductBarcodesQueryValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

public sealed class ListProductBarcodesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListProductBarcodesQuery, Result<IReadOnlyList<ProductBarcodeDto>>>
{
    public async Task<Result<IReadOnlyList<ProductBarcodeDto>>> Handle(ListProductBarcodesQuery request, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().Include(p => p.BaseUnit).FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        return producto is null
            ? Result.Failure<IReadOnlyList<ProductBarcodeDto>>(CatalogErrors.ProductNotFound())
            : Result.Success(await VistaDeProductos.CodigosAsync(db, producto, [], ct));
    }
}

/// <summary>
/// La búsqueda de productos (feature 012, T220; FR-020, T43, SC-009; contracts/api.md §3.5, <c>GET /products/search</c>).
/// Primero la lectura <b>exacta</b> —un código de barras vivo o el código del producto—, que es la que usa el lector en
/// modo teclado: si el código es de un empaque, trae su unidad y factor (<c>packUnit</c>, US1-5). Después, mientras se
/// escribe, cada término normalizado (sin tildes, en mayúsculas) tiene que estar en <c>SearchText</c>; orden: prefijo de
/// código, prefijo de nombre, el resto por nombre. Sólo activos salvo <see cref="IncludeInactive"/>; los bloqueados salen
/// con su estado. <c>available</c> sólo con bodega y <c>Inventory.Stock.View</c>; la bodega respeta el alcance
/// (<see cref="IAlcanceDeInventario"/>): fuera, el mismo 404 que si no existiera. (nuevo)
/// </summary>
public sealed record SearchProductsQuery(
    string Q, Guid? WarehousePublicId = null, IReadOnlyList<ProductKind>? Kinds = null, bool IncludeInactive = false, int? Take = null)
    : IRequest<Result<ProductSearchResultDto>>;

public sealed class SearchProductsQueryValidator : AbstractValidator<SearchProductsQuery>
{
    public SearchProductsQueryValidator()
    {
        RuleFor(x => x.Q).Must(q => NormalizadorDeBusqueda.Normalizar(q).Length >= NormalizadorDeBusqueda.MinimoDeCaracteres)
            .WithMessage($"Escriba al menos {NormalizadorDeBusqueda.MinimoDeCaracteres} caracteres para buscar.");
        RuleFor(x => x.Q).MaximumLength(100);
        RuleForEach(x => x.Kinds).IsInEnum();
    }
}

public sealed class SearchProductsQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    IExistenciasParaElCatalogo existencias)
    : IRequestHandler<SearchProductsQuery, Result<ProductSearchResultDto>>
{
    public const int TomaPorDefecto = 20;
    public const int TomaMaxima = 50;
    public const string VerExistencias = "Inventory.Stock.View";

    public async Task<Result<ProductSearchResultDto>> Handle(SearchProductsQuery request, CancellationToken ct)
    {
        int? bodegaId = null;
        if (request.WarehousePublicId is { } wp)
        {
            var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
            bodegaId = await db.Warehouses.Where(w => w.PublicId == wp).Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);
            if (bodegaId is null || !alcance.IncluyeBodega(bodegaId.Value)) return Result.Failure<ProductSearchResultDto>(ErroresDeAlcance.BodegaInexistente());
        }
        var toma = Math.Clamp(request.Take ?? TomaPorDefecto, 1, TomaMaxima);

        var candidatos = db.Products.AsNoTracking();
        if (!request.IncludeInactive) candidatos = candidatos.Where(p => p.Status != ProductStatus.Inactive);
        if (request.Kinds is { Count: > 0 } clases) candidatos = candidatos.Where(p => clases.Contains(p.Kind));

        // 1) Lectura exacta: código de barras vivo (con su empaque) o código del producto.
        var leido = ProductBarcode.Normalizar(request.Q);
        var porBarras = await db.ProductBarcodes.AsNoTracking().Include(b => b.ProductUnit).ThenInclude(u => u!.Unit)
            .Where(b => b.Barcode == leido && candidatos.Any(p => p.Id == b.ProductId))
            .FirstOrDefaultAsync(ct);
        var exactoId = porBarras?.ProductId
            ?? await candidatos.Where(p => p.Code == leido).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);

        // 2) Mientras se escribe: todos los términos, en el orden del contrato.
        var normalizada = NormalizadorDeBusqueda.Normalizar(request.Q);
        var ids = await BusquedaDeProductos.Contiene(candidatos, request.Q)
            .OrderBy(p => p.Code.StartsWith(normalizada) ? 0 : p.Name.ToUpper().StartsWith(normalizada) ? 1 : 2)
            .ThenBy(p => p.Name).ThenBy(p => p.Code)
            .Select(p => p.Id)
            .Take(toma)
            .ToListAsync(ct);

        var todos = ids.Concat(exactoId is int e ? new[] { e } : Array.Empty<int>()).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Include(p => p.Brand).Include(p => p.BaseUnit)
            .Where(p => todos.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        IReadOnlyDictionary<int, decimal> disponibles = new Dictionary<int, decimal>();
        if (bodegaId is int b && await permisos.HasPermissionAsync(VerExistencias, ct))
            disponibles = await existencias.DisponibleAsync(todos, b, ct);
        decimal? Disponible(int id) => bodegaId is null ? null : disponibles.TryGetValue(id, out var d) ? d : null;

        ProductSearchItemDto Item(Product p, ProductBarcode? codigo) => new(
            p.PublicId, p.Code, p.Name, p.Reference, p.Brand?.Name, p.BaseUnit!.Code, p.Status, codigo?.Barcode,
            codigo?.ProductUnit is { } pu ? new PackUnitDto(pu.PublicId, pu.Unit!.Code, pu.Factor) : null,
            Disponible(p.Id));

        var exacto = exactoId is int x && productos.TryGetValue(x, out var px) ? Item(px, porBarras) : null;
        var items = ids.Where(productos.ContainsKey).Select(id => Item(productos[id], null)).ToList();
        return Result.Success(new ProductSearchResultDto(exacto, items));
    }
}

/// <summary>
/// El filtro «contiene cada término» sobre <c>INV_Products.SearchText</c> (T43; research, decisión de búsqueda T006):
/// <c>LIKE '%término%' ESCAPE '\'</c>, portable entre SQL Server y PostgreSQL, sobre el índice de cada motor. (nuevo)
/// </summary>
public static class BusquedaDeProductos
{
    public static IQueryable<Product> Contiene(IQueryable<Product> consulta, string? texto)
    {
        var escape = NormalizadorDeBusqueda.CaracterDeEscape.ToString();
        foreach (var patron in NormalizadorDeBusqueda.PatronesContiene(texto))
            consulta = consulta.Where(p => EF.Functions.Like(p.SearchText, patron, escape));
        return consulta;
    }
}
