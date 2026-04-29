using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;

namespace IngenIA365ERP.Application.Core.Professions.Commands.CreateProfession;

public record CreateProfessionCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public class CreateProfessionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateProfessionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateProfessionCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new Profession
        {
            Name = request.Name,
            ShortName = request.ShortName,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Professions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateProfessionCommandValidator : AbstractValidator<CreateProfessionCommand>
{
    public CreateProfessionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(40).WithMessage("Short name must not exceed 40 characters.");
    }
}
