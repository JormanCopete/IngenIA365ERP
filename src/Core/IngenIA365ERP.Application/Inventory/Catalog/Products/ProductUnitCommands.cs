using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// Agregar una unidad alterna a un producto (feature 012, T219; contracts/api.md §3.6.1, <c>POST /products/{id}/units</c>;
/// FR-025): distinta de la base (<c>Inventory.ProductUnit.IsBaseUnit</c>) y sin repetir (<c>.Duplicate</c>), con factor
/// mayor que cero de hasta 6 decimales. Marcarla por defecto de compra o de venta desmarca la anterior (una sola de cada
/// lado); sin marca, la primera de cada lado queda por defecto. (nuevo)
/// </summary>
public sealed record AddProductUnitCommand(
    Guid ProductPublicId, Guid UnitPublicId, decimal Factor, ProductUnitUsage Usage, bool? IsDefaultPurchase = null, bool? IsDefaultSale = null)
    : IRequest<Result<ProductUnitDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AddProductUnitCommandValidator : AbstractValidator<AddProductUnitCommand>
{
    public AddProductUnitCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.UnitPublicId).NotEmpty();
        RuleFor(x => x.Factor).Must(ValidacionDeProducto.FactorValido).WithMessage("El factor es mayor que cero, con hasta 6 decimales.");
        RuleFor(x => x.Usage).IsInEnum();
    }
}

public sealed class AddProductUnitCommandHandler(IApplicationDbContext db) : IRequestHandler<AddProductUnitCommand, Result<ProductUnitDto>>
{
    public async Task<Result<ProductUnitDto>> Handle(AddProductUnitCommand request, CancellationToken ct)
    {
        var producto = await db.Products.Include(p => p.Units).FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Result.Failure<ProductUnitDto>(CatalogErrors.ProductNotFound());
        var unidad = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.PublicId == request.UnitPublicId, ct);
        if (unidad is null) return Result.Failure<ProductUnitDto>(CatalogErrors.UnitNotFound());
        if (unidad.Id == producto.BaseUnitId) return Result.Failure<ProductUnitDto>(CatalogErrors.ProductUnitIsBaseUnit(unidad.Code));
        var vivas = producto.Units.Where(u => !u.IsDeleted).ToList();
        if (vivas.Any(u => u.UnitId == unidad.Id)) return Result.Failure<ProductUnitDto>(CatalogErrors.ProductUnitDuplicate(unidad.Code));

        var alterna = new ProductUnit { Product = producto, UnitId = unidad.Id, Unit = unidad, Factor = request.Factor };
        alterna.FijarUso(request.Usage);
        UnidadesPorDefecto.Fijar(alterna, vivas, request.IsDefaultPurchase ?? vivas.All(u => !u.IsDefaultPurchase),
            request.IsDefaultSale ?? vivas.All(u => !u.IsDefaultSale));
        producto.Units.Add(alterna);
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeProductos.Unidad(alterna));
    }
}

/// <summary>
/// Cambiar el factor, el uso o la marca por defecto de una unidad alterna (T219; §3.6.1, <c>PUT /{productUnitId}</c>). Con
/// movimientos en esa unidad el factor no cambia (<c>Inventory.ProductUnit.FactorLocked</c>: la línea conserva el suyo y
/// se crea otra unidad). (nuevo)
/// </summary>
public sealed record UpdateProductUnitCommand(
    Guid ProductPublicId, Guid ProductUnitPublicId, decimal Factor, ProductUnitUsage Usage, bool? IsDefaultPurchase = null, bool? IsDefaultSale = null)
    : IRequest<Result<ProductUnitDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateProductUnitCommandValidator : AbstractValidator<UpdateProductUnitCommand>
{
    public UpdateProductUnitCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.ProductUnitPublicId).NotEmpty();
        RuleFor(x => x.Factor).Must(ValidacionDeProducto.FactorValido).WithMessage("El factor es mayor que cero, con hasta 6 decimales.");
        RuleFor(x => x.Usage).IsInEnum();
    }
}

public sealed class UpdateProductUnitCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateProductUnitCommand, Result<ProductUnitDto>>
{
    public async Task<Result<ProductUnitDto>> Handle(UpdateProductUnitCommand request, CancellationToken ct)
    {
        var producto = await db.Products.Include(p => p.Units).ThenInclude(u => u.Unit).FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        var alterna = producto?.Units.FirstOrDefault(u => !u.IsDeleted && u.PublicId == request.ProductUnitPublicId);
        if (producto is null || alterna is null) return Result.Failure<ProductUnitDto>(CatalogErrors.ProductUnitNotFound());

        if (alterna.Factor != request.Factor
            && await db.InventoryDocumentLines.IgnoreQueryFilters().AnyAsync(l => l.ProductId == producto.Id && l.UnitId == alterna.UnitId, ct))
            return Result.Failure<ProductUnitDto>(CatalogErrors.ProductUnitFactorLocked(alterna.Unit!.Code));

        alterna.Factor = request.Factor;
        alterna.FijarUso(request.Usage);
        var otras = producto.Units.Where(u => !u.IsDeleted && u != alterna).ToList();
        UnidadesPorDefecto.Fijar(alterna, otras, request.IsDefaultPurchase ?? alterna.IsDefaultPurchase, request.IsDefaultSale ?? alterna.IsDefaultSale);
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeProductos.Unidad(alterna));
    }
}

/// <summary>
/// Retirar una unidad alterna (T219; §3.6.1, <c>DELETE /{productUnitId}</c>): baja lógica. No se retira si la cita un
/// borrador o un código de barras vivo (las listas de precios llegan con I3): <c>Inventory.ProductUnit.InUse</c>. (nuevo)
/// </summary>
public sealed record RemoveProductUnitCommand(Guid ProductPublicId, Guid ProductUnitPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RemoveProductUnitCommandValidator : AbstractValidator<RemoveProductUnitCommand>
{
    public RemoveProductUnitCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.ProductUnitPublicId).NotEmpty();
    }
}

public sealed class RemoveProductUnitCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<RemoveProductUnitCommand, Result>
{
    public async Task<Result> Handle(RemoveProductUnitCommand request, CancellationToken ct)
    {
        var producto = await db.Products.Include(p => p.Units).ThenInclude(u => u.Unit).FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        var alterna = producto?.Units.FirstOrDefault(u => !u.IsDeleted && u.PublicId == request.ProductUnitPublicId);
        if (producto is null || alterna is null) return Result.Failure(CatalogErrors.ProductUnitNotFound());

        var enBorrador = await db.InventoryDocumentLines
            .Where(l => l.ProductId == producto.Id && l.UnitId == alterna.UnitId)
            .Join(db.InventoryDocuments, l => l.DocumentId, d => d.Id, (l, d) => d.Status)
            .AnyAsync(s => s == DocumentStatus.Draft || s == DocumentStatus.PendingApproval, ct);
        if (enBorrador) return Result.Failure(CatalogErrors.ProductUnitInUse(alterna.Unit!.Code, "la cita un documento en borrador"));
        if (await db.ProductBarcodes.AnyAsync(b => b.ProductUnitId == alterna.Id, ct))
            return Result.Failure(CatalogErrors.ProductUnitInUse(alterna.Unit!.Code, "la identifica un código de barras vivo"));

        alterna.IsDeleted = true;
        alterna.DeletedAt = reloj.UtcNow;
        alterna.IsDefaultPurchase = false;
        alterna.IsDefaultSale = false;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Una sola unidad por defecto de compra y una de venta por producto (data-model §1.7). (nuevo)</summary>
internal static class UnidadesPorDefecto
{
    public static void Fijar(ProductUnit alterna, IEnumerable<ProductUnit> otras, bool compra, bool venta)
    {
        alterna.IsDefaultPurchase = compra && alterna.UsedForPurchase;
        alterna.IsDefaultSale = venta && alterna.UsedForSale;
        foreach (var otra in otras)
        {
            if (alterna.IsDefaultPurchase) otra.IsDefaultPurchase = false;
            if (alterna.IsDefaultSale) otra.IsDefaultSale = false;
        }
    }
}
