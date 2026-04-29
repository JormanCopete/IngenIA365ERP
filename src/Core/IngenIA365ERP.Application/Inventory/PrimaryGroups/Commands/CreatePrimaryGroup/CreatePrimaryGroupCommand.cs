using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.PrimaryGroups.Commands.CreatePrimaryGroup;

public record CreatePrimaryGroupCommand : IRequest<Result<Guid>>
{
    public int GroupCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public class CreatePrimaryGroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePrimaryGroupCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePrimaryGroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new PrimaryGroup
        {
            GroupCode = request.GroupCode,
            Name = request.Name,
            ShortName = request.ShortName,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PrimaryGroups.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePrimaryGroupCommandValidator : AbstractValidator<CreatePrimaryGroupCommand>
{
    public CreatePrimaryGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");
    }
}
