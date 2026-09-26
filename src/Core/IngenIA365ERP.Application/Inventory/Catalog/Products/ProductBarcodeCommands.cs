using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// Asignar un código de barras a un producto (feature 012, T219; contracts/api.md §3.6.2; FR-020, FR-024, US1-4): recortado y
/// en mayúsculas, único en la cooperativa entre los vivos. Si ya lo tiene otro producto, <c>Inventory.Barcode.Duplicate</c>
/// nombrándolo; una carrera contra <c>UK_INV_ProductBarcodes_Barcode</c> se traduce al mismo código. El empaque es una unidad
/// alterna del producto (nulo = la base). El primero queda principal. Recalcula el texto de búsqueda. (nuevo)
/// </summary>
public sealed record AddProductBarcodeCommand(Guid ProductPublicId, string Barcode, Guid? ProductUnitPublicId)
    : IRequest<Result<ProductBarcodeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AddProductBarcodeCommandValidator : AbstractValidator<AddProductBarcodeCommand>
{
    public AddProductBarcodeCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.Barcode).Must(ValidacionDeProducto.CodigoDeBarrasValido)
            .WithMessage($"El código de barras tiene hasta {ProductBarcode.MaxLength} letras, dígitos o guiones, sin espacios.");
    }
}

public sealed class AddProductBarcodeCommandHandler(IApplicationDbContext db) : IRequestHandler<AddProductBarcodeCommand, Result<ProductBarcodeDto>>
{
    public async Task<Result<ProductBarcodeDto>> Handle(AddProductBarcodeCommand request, CancellationToken ct)
    {
        var producto = await db.Products.Include(p => p.BaseUnit).FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Falla(CatalogErrors.ProductNotFound());

        var barras = ProductBarcode.Normalizar(request.Barcode);
        if (await CodigosDeBarras.DuenoAsync(db, barras, ct) is { } dueno) return Falla(dueno);

        ProductUnit? empaque = null;
        if (request.ProductUnitPublicId is { } up)
        {
            empaque = await db.ProductUnits.Include(u => u.Unit).FirstOrDefaultAsync(u => u.PublicId == up && u.ProductId == producto.Id, ct);
            if (empaque is null) return Falla(CatalogErrors.ProductUnitNotFound());
        }

        var codigo = new ProductBarcode
        {
            ProductId = producto.Id,
            Barcode = barras,
            ProductUnitId = empaque?.Id,
            IsPrimary = !await db.ProductBarcodes.AnyAsync(b => b.ProductId == producto.Id && b.IsPrimary, ct),
        };
        db.ProductBarcodes.Add(codigo);
        await ReglasDeProducto.RecalcularTextoDeBusquedaAsync(db, producto, ct, agregados: [barras]);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dos personas asignaron el mismo código a la vez: el índice único ganó; se responde como el duplicado.
            db.ProductBarcodes.Remove(codigo);
            if (await CodigosDeBarras.DuenoAsync(db, barras, ct) is { } ganador) return Falla(ganador);
            throw;
        }

        return Result.Success(new ProductBarcodeDto(codigo.PublicId, codigo.Barcode, empaque?.PublicId,
            empaque?.Unit?.Code ?? producto.BaseUnit!.Code, codigo.IsPrimary));
    }

    private static Result<ProductBarcodeDto> Falla(Error e) => Result.Failure<ProductBarcodeDto>(e);
}

/// <summary>
/// Retirar un código de barras (T219; §3.6.2, <c>DELETE /{barcodeId}</c>): baja lógica; el código queda libre para otro
/// producto. Si era el principal, el siguiente pasa a serlo. Recalcula el texto de búsqueda. (nuevo)
/// </summary>
public sealed record RemoveProductBarcodeCommand(Guid ProductPublicId, Guid BarcodePublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RemoveProductBarcodeCommandValidator : AbstractValidator<RemoveProductBarcodeCommand>
{
    public RemoveProductBarcodeCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.BarcodePublicId).NotEmpty();
    }
}

public sealed class RemoveProductBarcodeCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<RemoveProductBarcodeCommand, Result>
{
    public async Task<Result> Handle(RemoveProductBarcodeCommand request, CancellationToken ct)
    {
        var producto = await db.Products.FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        var codigo = producto is null ? null
            : await db.ProductBarcodes.FirstOrDefaultAsync(b => b.PublicId == request.BarcodePublicId && b.ProductId == producto.Id, ct);
        if (producto is null || codigo is null) return Result.Failure(CatalogErrors.BarcodeNotFound());

        codigo.IsDeleted = true;
        codigo.DeletedAt = reloj.UtcNow;
        if (codigo.IsPrimary)
        {
            codigo.IsPrimary = false;
            var siguiente = await db.ProductBarcodes.Where(b => b.ProductId == producto.Id && b.Id != codigo.Id).OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
            if (siguiente is not null) siguiente.IsPrimary = true;
        }
        await ReglasDeProducto.RecalcularTextoDeBusquedaAsync(db, producto, ct, retirados: [codigo.Barcode]);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
