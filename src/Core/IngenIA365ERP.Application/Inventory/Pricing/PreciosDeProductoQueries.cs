using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Common.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pricing;

// La pantalla de cambiar precios (2026-11-15, pedido del dueño): un solo buscador flexible —código, nombre, referencia o código de
// barras, como en las ventas, desde tres caracteres— y el producto en una ventana con sus precios por unidad y su historia. (nuevo)

/// <summary>Un producto de la búsqueda con sus precios vigentes en la lista, uno por unidad. (nuevo)</summary>
public sealed record PriceListProductRowDto(Guid ProductPublicId, string Code, string Name, string? Reference, string BaseUnitCode,
    decimal? BasePrice, IReadOnlyList<PriceListUnitPriceDto> Prices);

/// <summary>Un precio vigente de la lista en una unidad. <see cref="IsOrphan"/>: la unidad ya no es del producto y la venta no lo usa. (nuevo)</summary>
public sealed record PriceListUnitPriceDto(Guid UnitPublicId, string UnitCode, decimal Price, bool IsOrphan);

/// <summary>Una unidad en que se vende el producto, con su precio en la lista (nulo si no lo tiene). (nuevo)</summary>
public sealed record PriceListProductUnitDto(Guid UnitPublicId, string UnitCode, string UnitName, bool IsBase, decimal Factor, decimal? Price, bool IsOrphan);

/// <summary>Una fila de la historia del precio. (nuevo)</summary>
public sealed record PriceHistoryEntryDto(DateTime At, string? By, PriceChangeKind Kind, string UnitCode, string? PreviousUnitCode, decimal? PreviousPrice,
    decimal? NewPrice, string Reason);

/// <summary>El producto en la ventana de precios: sus unidades de venta con el precio y la historia en la lista. (nuevo)</summary>
public sealed record PriceListProductDetailDto(Guid ProductPublicId, string Code, string Name, string? Reference, IReadOnlyList<PriceListProductUnitDto> Units,
    IReadOnlyList<PriceHistoryEntryDto> History);

/// <summary>
/// <c>GET /api/inventory/price-lists/{id}/products?q=&amp;page=&amp;pageSize=</c>: con <see cref="Q"/> de tres caracteres o más, los productos
/// vivos (no plantillas) que contienen cada término en su texto de búsqueda —código, nombre, referencia, marca y códigos de barras—,
/// estén o no en la lista; sin texto, los que la lista tiene. Cada uno con sus precios vigentes en la lista. (nuevo)
/// </summary>
public sealed record SearchPriceListProductsQuery(Guid PriceListPublicId, string? Q = null, PageRequest? Pagina = null,
    FiltroDePreciosDeLista Filtro = FiltroDePreciosDeLista.Todos)
    : IRequest<Result<PagedResult<PriceListProductRowDto>>>;

/// <summary>
/// 2026-11-15 (pedido del dueño): qué productos muestra el buscador de la lista. <see cref="SinPrecioBase"/> sin texto recorre todo el
/// catálogo (los que faltan por cargar); los demás, sin texto, los que la lista tiene. (nuevo)
/// </summary>
public enum FiltroDePreciosDeLista
{
    Todos = 0,

    /// <summary>Con precio en su unidad base.</summary>
    ConPrecioBase = 1,

    /// <summary>Sin precio en su unidad base (la venta no lo encuentra).</summary>
    SinPrecioBase = 2,

    /// <summary>Con precio en alguna unidad distinta de la base (alternas o huérfanos).</summary>
    ConOtrasUnidades = 3,
}

public sealed class SearchPriceListProductsQueryValidator : AbstractValidator<SearchPriceListProductsQuery>
{
    public SearchPriceListProductsQueryValidator()
    {
        RuleFor(x => x.PriceListPublicId).NotEmpty();
        RuleFor(x => x.Filtro).IsInEnum();
    }
}

public sealed class SearchPriceListProductsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<SearchPriceListProductsQuery, Result<PagedResult<PriceListProductRowDto>>>
{
    /// <summary>Desde cuántos caracteres filtra el buscador (como en las ventas).</summary>
    public const int MinimoDeCaracteres = 3;

    public async Task<Result<PagedResult<PriceListProductRowDto>>> Handle(SearchPriceListProductsQuery request, CancellationToken ct)
    {
        var listaId = await db.PriceLists.AsNoTracking().Where(l => l.PublicId == request.PriceListPublicId && !l.IsDeleted)
            .Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        if (listaId is not int lista) return Result.Failure<PagedResult<PriceListProductRowDto>>(ErroresDePrecios.PriceListNotFound());

        var productos = db.Products.AsNoTracking().Where(p => !p.IsDeleted && p.Kind != ProductKind.Template);
        var texto = NormalizadorDeBusqueda.Normalizar(request.Q);
        var vivos = db.PriceListItems.Where(i => i.PriceListId == lista && !i.IsDeleted);
        if (texto.Length >= MinimoDeCaracteres) productos = BusquedaDeProductos.Contiene(productos, request.Q);
        else if (request.Filtro != FiltroDePreciosDeLista.SinPrecioBase) productos = productos.Where(p => vivos.Any(i => i.ProductId == p.Id));
        productos = request.Filtro switch
        {
            FiltroDePreciosDeLista.ConPrecioBase => productos.Where(p => vivos.Any(i => i.ProductId == p.Id && i.UnitId == p.BaseUnitId)),
            FiltroDePreciosDeLista.SinPrecioBase => productos.Where(p => !vivos.Any(i => i.ProductId == p.Id && i.UnitId == p.BaseUnitId)),
            FiltroDePreciosDeLista.ConOtrasUnidades => productos.Where(p => vivos.Any(i => i.ProductId == p.Id && i.UnitId != p.BaseUnitId)),
            _ => productos,
        };

        var pagina = request.Pagina ?? new PageRequest(1, 50);
        var total = await productos.LongCountAsync(ct);
        var filas = await productos.OrderBy(p => p.Code)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .Select(p => new { p.Id, p.PublicId, p.Code, p.Name, p.Reference, p.BaseUnitId, BaseUnitCode = p.BaseUnit!.Code })
            .ToListAsync(ct);
        var ids = filas.Select(f => f.Id).ToList();
        var precios = await PreciosAsync(db, lista, ids, ct);

        var items = filas.Select(f =>
        {
            var suyos = precios.Where(p => p.ProductId == f.Id).ToList();
            return new PriceListProductRowDto(f.PublicId, f.Code, f.Name, f.Reference, f.BaseUnitCode,
                suyos.FirstOrDefault(p => p.UnitId == f.BaseUnitId)?.Price,
                suyos.Select(p => new PriceListUnitPriceDto(p.UnitPublicId, p.UnitCode, p.Price, p.Huerfano)).ToList());
        }).ToList();
        return Result.Success(new PagedResult<PriceListProductRowDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }

    internal sealed record PrecioVivo(int ProductId, int UnitId, Guid UnitPublicId, string UnitCode, decimal Price, bool Huerfano);

    /// <summary>Los precios vivos de la lista para esos productos, con la marca de huérfano (unidad que no es base ni alterna de venta viva).</summary>
    internal static async Task<List<PrecioVivo>> PreciosAsync(IApplicationDbContext db, int lista, IReadOnlyCollection<int> productos, CancellationToken ct)
    {
        var filas = await (from i in db.PriceListItems.AsNoTracking()
                           join u in db.UnitsOfMeasure.AsNoTracking() on i.UnitId equals u.Id
                           join p in db.Products.AsNoTracking() on i.ProductId equals p.Id
                           where i.PriceListId == lista && !i.IsDeleted && productos.Contains(i.ProductId)
                           select new
                           {
                               i.ProductId, i.UnitId, UnitPublicId = u.PublicId, UnitCode = u.Code, i.Price,
                               EsDelProducto = i.UnitId == p.BaseUnitId
                                   || db.ProductUnits.Any(x => x.ProductId == p.Id && x.UnitId == i.UnitId && x.UsedForSale && !x.IsDeleted),
                           }).ToListAsync(ct);
        return filas.OrderBy(f => f.UnitCode).Select(f => new PrecioVivo(f.ProductId, f.UnitId, f.UnitPublicId, f.UnitCode, f.Price, !f.EsDelProducto)).ToList();
    }
}

/// <summary>
/// <c>GET /api/inventory/price-lists/{id}/products/{productId}</c>: el producto en la ventana de precios —la base y sus alternas de venta
/// con el precio de la lista (y un precio huérfano, si quedó)— y su historia en esa lista, la más reciente primero. (nuevo)
/// </summary>
public sealed record GetPriceListProductQuery(Guid PriceListPublicId, Guid ProductPublicId) : IRequest<Result<PriceListProductDetailDto>>;

public sealed class GetPriceListProductQueryValidator : AbstractValidator<GetPriceListProductQuery>
{
    public GetPriceListProductQueryValidator()
    {
        RuleFor(x => x.PriceListPublicId).NotEmpty();
        RuleFor(x => x.ProductPublicId).NotEmpty();
    }
}

public sealed class GetPriceListProductQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPriceListProductQuery, Result<PriceListProductDetailDto>>
{
    /// <summary>Cuántas filas de historia devuelve.</summary>
    public const int FilasDeHistoria = 200;

    public async Task<Result<PriceListProductDetailDto>> Handle(GetPriceListProductQuery request, CancellationToken ct)
    {
        var lista = await db.PriceLists.AsNoTracking().Where(l => l.PublicId == request.PriceListPublicId && !l.IsDeleted)
            .Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        if (lista is not int listaId) return Result.Failure<PriceListProductDetailDto>(ErroresDePrecios.PriceListNotFound());
        var producto = await db.Products.AsNoTracking().Include(p => p.BaseUnit)
            .Include(p => p.Units.Where(u => !u.IsDeleted && u.UsedForSale)).ThenInclude(u => u.Unit)
            .FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId && !p.IsDeleted, ct);
        if (producto?.BaseUnit is not { } unidadBase) return Result.Failure<PriceListProductDetailDto>(Error.NotFound);

        var precios = await SearchPriceListProductsQueryHandler.PreciosAsync(db, listaId, [producto.Id], ct);
        decimal? PrecioEn(int unidad) => precios.FirstOrDefault(p => p.UnitId == unidad)?.Price;

        var unidades = new List<PriceListProductUnitDto>
        {
            new(unidadBase.PublicId, unidadBase.Code, unidadBase.Name, true, 1m, PrecioEn(unidadBase.Id), false),
        };
        unidades.AddRange(producto.Units.Where(u => u.Unit is not null).OrderBy(u => u.Unit!.Code)
            .Select(u => new PriceListProductUnitDto(u.Unit!.PublicId, u.Unit.Code, u.Unit.Name, false, u.Factor, PrecioEn(u.UnitId), false)));
        unidades.AddRange(precios.Where(p => p.Huerfano)
            .Select(p => new PriceListProductUnitDto(p.UnitPublicId, p.UnitCode, p.UnitCode, false, 0m, p.Price, true)));

        var historia = await (from h in db.PriceListItemHistory.AsNoTracking()
                              join u in db.UnitsOfMeasure.AsNoTracking() on h.UnitId equals u.Id
                              join a in db.UnitsOfMeasure.AsNoTracking() on h.PreviousUnitId equals a.Id into anteriores
                              from a in anteriores.DefaultIfEmpty()
                              where h.PriceListId == listaId && h.ProductId == producto.Id
                              orderby h.CreatedAt descending, h.Id descending
                              select new PriceHistoryEntryDto(h.CreatedAt, h.CreatedBy, h.Kind, u.Code, a == null ? null : a.Code, h.PreviousPrice, h.NewPrice, h.Reason))
            .Take(FilasDeHistoria).ToListAsync(ct);

        return Result.Success(new PriceListProductDetailDto(producto.PublicId, producto.Code, producto.Name, producto.Reference, unidades, historia));
    }
}
