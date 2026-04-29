using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Locations.Commands.CreateLocation;

public record CreateLocationCommand : IRequest<Result<Guid>>
{
    public int LocationCode { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
}

public class CreateLocationCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateLocationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateLocationCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new Location
        {
            LocationCode = request.LocationCode,
            Description = request.Description,
            ShortDescription = request.ShortDescription,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Locations.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateLocationCommandValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationCommandValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(100).WithMessage("Description must not exceed 100 characters.");

        RuleFor(x => x.ShortDescription)
            .MaximumLength(50).WithMessage("Short description must not exceed 50 characters.");
    }
}
