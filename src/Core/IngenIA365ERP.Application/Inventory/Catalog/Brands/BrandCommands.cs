using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Brands;

/// <summary>Alta de una marca (feature 012, T216; contracts/api.md §3.3). (nuevo)</summary>
public sealed record CreateBrandCommand(string Code, string Name) : IRequest<Result<BrandDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}

public sealed class CreateBrandCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateBrandCommand, Result<BrandDto>>
{
    public async Task<Result<BrandDto>> Handle(CreateBrandCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.Brands.Where(b => b.Code == codigo).Select(b => new { b.PublicId, b.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Failure<BrandDto>(CodigoDeCatalogo.Duplicado("una marca", codigo, existente.Name, existente.PublicId));

        var marca = new Brand { Code = codigo, Name = request.Name.Trim(), IsActive = true };
        db.Brands.Add(marca);
        await db.SaveChangesAsync(ct);
        return Result.Success(new BrandDto(marca.PublicId, marca.Code, marca.Name, marca.IsActive, 0));
    }
}

/// <summary>
/// Renombrar una marca (T216; §3.3, <c>PUT /{id}</c>; data-model §1.3): la búsqueda incluye la marca, así que el texto de
/// búsqueda de sus productos se recalcula en el mismo guardado. (nuevo)
/// </summary>
public sealed record UpdateBrandCommand(Guid BrandPublicId, string Name) : IRequest<Result<BrandDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateBrandCommandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandCommandValidator()
    {
        RuleFor(x => x.BrandPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}

public sealed class UpdateBrandCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateBrandCommand, Result<BrandDto>>
{
    public async Task<Result<BrandDto>> Handle(UpdateBrandCommand request, CancellationToken ct)
    {
        var marca = await db.Brands.FirstOrDefaultAsync(b => b.PublicId == request.BrandPublicId, ct);
        if (marca is null) return Result.Failure<BrandDto>(CatalogErrors.BrandNotFound());

        var nombre = request.Name.Trim();
        var productos = await db.Products.Where(p => p.BrandId == marca.Id).ToListAsync(ct);
        if (marca.Name != nombre)
        {
            marca.Name = nombre;
            var ids = productos.Select(p => p.Id).ToList();
            var codigos = (await db.ProductBarcodes.Where(b => ids.Contains(b.ProductId)).Select(b => new { b.ProductId, b.Barcode }).ToListAsync(ct))
                .ToLookup(b => b.ProductId, b => b.Barcode);
            foreach (var p in productos)
                p.SearchText = ReglasDeProducto.TextoDeBusqueda(p, nombre, codigos[p.Id]);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(new BrandDto(marca.PublicId, marca.Code, marca.Name, marca.IsActive, productos.Count));
    }
}

/// <summary>
/// Inactivar o reactivar una marca con motivo (T216; §3.3): una marca con productos activos se puede inactivar (los
/// productos la conservan). (nuevo)
/// </summary>
public sealed record SetBrandActiveCommand(Guid BrandPublicId, bool Active, string Reason) : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetBrandActiveCommandValidator : ValidadorConMotivo<SetBrandActiveCommand>
{
    public SetBrandActiveCommandValidator() => RuleFor(x => x.BrandPublicId).NotEmpty();
}

public sealed class SetBrandActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetBrandActiveCommand, Result>
{
    public async Task<Result> Handle(SetBrandActiveCommand request, CancellationToken ct) =>
        await ActivacionDeCatalogo.CambiarAsync(
            await db.Brands.FirstOrDefaultAsync(b => b.PublicId == request.BrandPublicId, ct),
            request.Active, b => b.IsActive, (b, a) => b.IsActive = a, CatalogErrors.BrandNotFound(), null, db.SaveChangesAsync, ct);
}

/// <summary>Las marcas (T216; §3.3, <c>GET /?includeInactive=</c>), por código, con sus productos vivos. (nuevo)</summary>
public sealed record ListBrandsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<BrandDto>>>;

public sealed class ListBrandsQueryValidator : AbstractValidator<ListBrandsQuery>;

public sealed class ListBrandsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListBrandsQuery, Result<IReadOnlyList<BrandDto>>>
{
    public async Task<Result<IReadOnlyList<BrandDto>>> Handle(ListBrandsQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<BrandDto>>(await db.Brands.AsNoTracking()
            .Where(b => request.IncludeInactive || b.IsActive)
            .OrderBy(b => b.Code)
            .Select(b => new BrandDto(b.PublicId, b.Code, b.Name, b.IsActive, db.Products.Count(p => p.BrandId == b.Id)))
            .ToListAsync(ct));
}
