using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.PriceListTypes.Commands.CreatePriceListType;

public record CreatePriceListTypeCommand : IRequest<Result<Guid>>
{
    public int TypeCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? PriceClass { get; init; }
}

public class CreatePriceListTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePriceListTypeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePriceListTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new PriceListType
        {
            TypeCode = request.TypeCode,
            Name = request.Name,
            ShortName = request.ShortName,
            PriceClass = request.PriceClass,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PriceListTypes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePriceListTypeCommandValidator : AbstractValidator<CreatePriceListTypeCommand>
{
    public CreatePriceListTypeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");
    }
}
