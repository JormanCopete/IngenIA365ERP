using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.SecondaryGroups.Commands.CreateSecondaryGroup;

public record CreateSecondaryGroupCommand : IRequest<Result<Guid>>
{
    public int GroupCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? PrimaryGroupId { get; init; }
}

public class CreateSecondaryGroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateSecondaryGroupCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateSecondaryGroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new SecondaryGroup
        {
            GroupCode = request.GroupCode,
            Name = request.Name,
            ShortName = request.ShortName,
            PrimaryGroupId = request.PrimaryGroupId,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.SecondaryGroups.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateSecondaryGroupCommandValidator : AbstractValidator<CreateSecondaryGroupCommand>
{
    public CreateSecondaryGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");
    }
}
