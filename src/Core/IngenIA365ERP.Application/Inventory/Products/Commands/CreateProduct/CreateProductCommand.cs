using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Products.Commands.CreateProduct;

public record CreateProductCommand : IRequest<Result<Guid>>
{
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
    public bool IsActive { get; init; } = true;
    public string? Barcode { get; init; }
    public decimal OtherTax { get; init; }
    public bool ControlsStock { get; init; }
    public bool RestrictsLimit { get; init; }
    public int MaxSalesQuantity { get; init; }
}

public class CreateProductCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new Product
        {
            ProductCode = request.ProductCode,
            Name = request.Name,
            ShortName = request.ShortName,
            GroupId = request.GroupId,
            DiscountTypeId = request.DiscountTypeId,
            UnitOfMeasure = request.UnitOfMeasure,
            CostPrice = request.CostPrice,
            SalePrice = request.SalePrice,
            VatRate = request.VatRate,
            MinStock = request.MinStock,
            MaxStock = request.MaxStock,
            IsActive = request.IsActive,
            Barcode = request.Barcode,
            OtherTax = request.OtherTax,
            ControlsStock = request.ControlsStock,
            RestrictsLimit = request.RestrictsLimit,
            MaxSalesQuantity = request.MaxSalesQuantity,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Products.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(80).WithMessage("Short name must not exceed 80 characters.");

        RuleFor(x => x.UnitOfMeasure)
            .MaximumLength(10).WithMessage("Unit of measure must not exceed 10 characters.");

        RuleFor(x => x.Barcode)
            .MaximumLength(30).WithMessage("Barcode must not exceed 30 characters.");
    }
}
