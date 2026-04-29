using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.ProductGroups.Commands.CreateProductGroup;

public record CreateProductGroupCommand : IRequest<Result<Guid>>
{
    public int GroupCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? SecondaryGroupId { get; init; }
    public bool RestrictsLimit { get; init; }
    public int MaxSalesQuantity { get; init; }
}

public class CreateProductGroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateProductGroupCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateProductGroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new ProductGroup
        {
            GroupCode = request.GroupCode,
            Name = request.Name,
            ShortName = request.ShortName,
            SecondaryGroupId = request.SecondaryGroupId,
            RestrictsLimit = request.RestrictsLimit,
            MaxSalesQuantity = request.MaxSalesQuantity,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.ProductGroups.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateProductGroupCommandValidator : AbstractValidator<CreateProductGroupCommand>
{
    public CreateProductGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");
    }
}
