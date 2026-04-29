using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;

namespace IngenIA365ERP.Application.Core.Positions.Commands.CreatePosition;

public record CreatePositionCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public class CreatePositionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePositionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePositionCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new Position
        {
            Name = request.Name,
            ShortName = request.ShortName,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Positions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePositionCommandValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(40).WithMessage("Short name must not exceed 40 characters.");
    }
}
