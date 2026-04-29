using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.DiscountTypes.Commands.CreateDiscountType;

public record CreateDiscountTypeCommand : IRequest<Result<Guid>>
{
    public int TypeCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? DiscountClass { get; init; }
}

public class CreateDiscountTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateDiscountTypeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateDiscountTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new DiscountType
        {
            TypeCode = request.TypeCode,
            Name = request.Name,
            ShortName = request.ShortName,
            DiscountClass = request.DiscountClass,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.DiscountTypes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateDiscountTypeCommandValidator : AbstractValidator<CreateDiscountTypeCommand>
{
    public CreateDiscountTypeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");
    }
}
