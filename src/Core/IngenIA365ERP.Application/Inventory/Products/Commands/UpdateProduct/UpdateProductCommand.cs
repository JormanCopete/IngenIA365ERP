using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Products.Commands.UpdateProduct;

public record UpdateProductCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int ProductCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? GroupId { get; init; }
    public int? DiscountTypeId { get; init; }
    public string? UnitOfMeasure { get; init; }
    public decimal CostPrice { get; init; }
    public decimal SalePrice { get; init; }
    public decimal VatRate { get; init; }
    public int MinStock { get; init; }
    public int MaxStock { get; init; }
    public bool IsActive { get; init; }
    public string? Barcode { get; init; }
    public decimal OtherTax { get; init; }
    public bool ControlsStock { get; init; }
    public bool RestrictsLimit { get; init; }
    public int MaxSalesQuantity { get; init; }
}

public class UpdateProductCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateProductCommand, Result>
{
    public async Task<Result> Handle(
        UpdateProductCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Products
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ProductCode = request.ProductCode;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.GroupId = request.GroupId;
        entity.DiscountTypeId = request.DiscountTypeId;
        entity.UnitOfMeasure = request.UnitOfMeasure;
        entity.CostPrice = request.CostPrice;
        entity.SalePrice = request.SalePrice;
        entity.VatRate = request.VatRate;
        entity.MinStock = request.MinStock;
        entity.MaxStock = request.MaxStock;
        entity.IsActive = request.IsActive;
        entity.Barcode = request.Barcode;
        entity.OtherTax = request.OtherTax;
        entity.ControlsStock = request.ControlsStock;
        entity.RestrictsLimit = request.RestrictsLimit;
        entity.MaxSalesQuantity = request.MaxSalesQuantity;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
