using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Categories;

/// <summary>
/// Alta de una categoría (feature 012, T215; contracts/api.md §3.2; FR-024; data-model §1.2): raíz o hija de otra, hasta el
/// nivel 5 (<c>Inventory.Category.TooDeep</c>). La ruta materializada (<c>/3/17/42/</c>) necesita el Id, así que se guarda
/// en dos pasos dentro de la misma operación. (nuevo)
/// </summary>
public sealed record CreateProductCategoryCommand(string Code, string Name, Guid? ParentPublicId)
    : IRequest<Result<CategoryDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateProductCategoryCommandValidator : AbstractValidator<CreateProductCategoryCommand>
{
    public CreateProductCategoryCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}

public sealed class CreateProductCategoryCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateProductCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(CreateProductCategoryCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.ProductCategories.Where(c => c.Code == codigo).Select(c => new { c.PublicId, c.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Failure<CategoryDto>(CodigoDeCatalogo.Duplicado("una categoría", codigo, existente.Name, existente.PublicId));

        ProductCategory? padre = null;
        if (request.ParentPublicId is { } p)
        {
            padre = await db.ProductCategories.FirstOrDefaultAsync(c => c.PublicId == p, ct);
            if (padre is null) return Result.Failure<CategoryDto>(CatalogErrors.CategoryNotFound());
            if (padre.Level + 1 > ProductCategory.MaxLevel) return Result.Failure<CategoryDto>(CatalogErrors.CategoryTooDeep());
        }

        var categoria = new ProductCategory
        {
            Code = codigo,
            Name = request.Name.Trim(),
            ParentId = padre?.Id,
            Level = (byte)((padre?.Level ?? 0) + 1),
            IsActive = true,
        };
        db.ProductCategories.Add(categoria);
        await db.SaveChangesAsync(ct);
        categoria.Path = ProductCategory.RutaDe(padre?.Path, categoria.Id);
        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDeCategorias.UnaAsync(db, categoria, ct));
    }
}

/// <summary>
/// Edición de una categoría (T215; §3.2, <c>PUT /{id}</c>): nombre y padre (mover la rama). La rama no puede quedar debajo
/// de sí misma (<c>Inventory.Category.Cycle</c>) ni su subcategoría más profunda pasar del nivel 5 (<c>.TooDeep</c>); al
/// moverla, la ruta y el nivel de todos los descendientes se reescriben en el mismo guardado. (nuevo)
/// </summary>
public sealed record UpdateProductCategoryCommand(Guid CategoryPublicId, string Name, Guid? ParentPublicId)
    : IRequest<Result<CategoryDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateProductCategoryCommandValidator : AbstractValidator<UpdateProductCategoryCommand>
{
    public UpdateProductCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}

public sealed class UpdateProductCategoryCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateProductCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(UpdateProductCategoryCommand request, CancellationToken ct)
    {
        var categoria = await db.ProductCategories.FirstOrDefaultAsync(c => c.PublicId == request.CategoryPublicId, ct);
        if (categoria is null) return Result.Failure<CategoryDto>(CatalogErrors.CategoryNotFound());

        ProductCategory? padre = null;
        if (request.ParentPublicId is { } p)
        {
            padre = await db.ProductCategories.FirstOrDefaultAsync(c => c.PublicId == p, ct);
            if (padre is null) return Result.Failure<CategoryDto>(CatalogErrors.CategoryNotFound());
        }

        categoria.Name = request.Name.Trim();
        if (padre?.Id != categoria.ParentId)
        {
            var movida = await MoverAsync(categoria, padre, ct);
            if (movida.IsFailure) return Result.Failure<CategoryDto>(movida.Error);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDeCategorias.UnaAsync(db, categoria, ct));
    }

    private async Task<Result> MoverAsync(ProductCategory categoria, ProductCategory? padre, CancellationToken ct)
    {
        if (padre is not null && (padre.Id == categoria.Id || padre.Path.StartsWith(categoria.Path, StringComparison.Ordinal)))
            return Result.Failure(CatalogErrors.CategoryCycle());

        var rama = await db.ProductCategories.Where(c => c.Path.StartsWith(categoria.Path)).ToListAsync(ct);
        var nivelNuevo = (padre?.Level ?? 0) + 1;
        var desplazamiento = nivelNuevo - categoria.Level;
        var masProfunda = rama.Select(c => (int)c.Level).DefaultIfEmpty(categoria.Level).Max();
        if (masProfunda + desplazamiento > ProductCategory.MaxLevel) return Result.Failure(CatalogErrors.CategoryTooDeep());

        var rutaVieja = categoria.Path;
        var rutaNueva = ProductCategory.RutaDe(padre?.Path, categoria.Id);
        foreach (var c in rama)
        {
            c.Path = rutaNueva + c.Path[rutaVieja.Length..];
            c.Level = (byte)(c.Level + desplazamiento);
        }
        categoria.ParentId = padre?.Id;
        categoria.Parent = padre;
        return Result.Success();
    }
}

/// <summary>
/// Inactivar o reactivar una categoría con motivo (T215; §3.2): con subcategorías activas o productos que no están
/// inactivos, <c>Inventory.Category.InUse</c> con <c>children</c> y <c>products</c>. (nuevo)
/// </summary>
public sealed record SetProductCategoryActiveCommand(Guid CategoryPublicId, bool Active, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetProductCategoryActiveCommandValidator : ValidadorConMotivo<SetProductCategoryActiveCommand>
{
    public SetProductCategoryActiveCommandValidator() => RuleFor(x => x.CategoryPublicId).NotEmpty();
}

public sealed class SetProductCategoryActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetProductCategoryActiveCommand, Result>
{
    public async Task<Result> Handle(SetProductCategoryActiveCommand request, CancellationToken ct) =>
        await ActivacionDeCatalogo.CambiarAsync(
            await db.ProductCategories.FirstOrDefaultAsync(c => c.PublicId == request.CategoryPublicId, ct),
            request.Active, c => c.IsActive, (c, a) => c.IsActive = a, CatalogErrors.CategoryNotFound(),
            async c =>
            {
                var hijas = await db.ProductCategories.CountAsync(h => h.ParentId == c.Id && h.IsActive, ct);
                var productos = await db.Products.CountAsync(p => p.CategoryId == c.Id && p.Status != ProductStatus.Inactive, ct);
                return hijas + productos == 0 ? null : CatalogErrors.CategoryInUse(hijas, productos);
            },
            db.SaveChangesAsync, ct);
}

/// <summary>
/// Las categorías en orden de árbol (T215; §3.2, <c>GET /?includeInactive=</c>) con la ruta legible
/// («ABARROTES › GRANOS») y los productos vivos de cada una. (nuevo)
/// </summary>
public sealed record ListProductCategoriesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<CategoryDto>>>;

public sealed class ListProductCategoriesQueryValidator : AbstractValidator<ListProductCategoriesQuery>;

public sealed class ListProductCategoriesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListProductCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(ListProductCategoriesQuery request, CancellationToken ct)
    {
        var todas = await db.ProductCategories.AsNoTracking().ToListAsync(ct);
        var productos = await VistaDeCategorias.ProductosPorCategoriaAsync(db, ct);
        var vista = todas.Where(c => request.IncludeInactive || c.IsActive)
            .Select(c => VistaDeCategorias.Dto(c, todas, productos.GetValueOrDefault(c.Id)))
            .OrderBy(d => d.Path, StringComparer.Ordinal)
            .ToList();
        return Result.Success<IReadOnlyList<CategoryDto>>(vista);
    }
}

/// <summary>Cómo se muestra una categoría: la ruta legible por nombres y su padre por PublicId. (nuevo)</summary>
public static class VistaDeCategorias
{
    public const string Separador = " › ";

    public static async Task<CategoryDto> UnaAsync(IApplicationDbContext db, ProductCategory categoria, CancellationToken ct)
    {
        var ids = categoria.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        var rama = await db.ProductCategories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        if (rama.All(c => c.Id != categoria.Id)) rama.Add(categoria);
        var productos = await db.Products.CountAsync(p => p.CategoryId == categoria.Id, ct);
        return Dto(categoria, rama, productos);
    }

    public static CategoryDto Dto(ProductCategory c, IReadOnlyCollection<ProductCategory> conocidas, int productos)
    {
        var porId = conocidas.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());
        return new CategoryDto(c.PublicId, c.Code, c.Name,
            c.ParentId is int p && porId.TryGetValue(p, out var padre) ? padre.PublicId : null,
            c.Level, RutaLegible(c, porId), c.IsActive, productos);
    }

    /// <summary>«ABARROTES › GRANOS»: los nombres de la raíz a la categoría.</summary>
    public static string RutaLegible(ProductCategory c, IReadOnlyDictionary<int, ProductCategory> porId)
    {
        var nombres = c.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse)
            .Select(id => porId.TryGetValue(id, out var x) ? x.Name : null).OfType<string>().ToList();
        return nombres.Count == 0 ? c.Name : string.Join(Separador, nombres);
    }

    public static async Task<IReadOnlyDictionary<int, int>> ProductosPorCategoriaAsync(IApplicationDbContext db, CancellationToken ct) =>
        await db.Products.AsNoTracking().GroupBy(p => p.CategoryId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
}
