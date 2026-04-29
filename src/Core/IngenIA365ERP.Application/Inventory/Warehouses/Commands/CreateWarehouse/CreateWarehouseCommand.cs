using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Warehouses.Commands.CreateWarehouse;

public record CreateWarehouseCommand : IRequest<Result<Guid>>
{
    public int WarehouseCode { get; init; }
    public int LocationId { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
}

public class CreateWarehouseCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWarehouseCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateWarehouseCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new Warehouse
        {
            WarehouseCode = request.WarehouseCode,
            LocationId = request.LocationId,
            Description = request.Description,
            ShortDescription = request.ShortDescription,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Warehouses.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(100).WithMessage("Description must not exceed 100 characters.");

        RuleFor(x => x.ShortDescription)
            .MaximumLength(50).WithMessage("Short description must not exceed 50 characters.");
    }
}
