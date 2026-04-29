using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;

namespace IngenIA365ERP.Application.Core.Diseases.Commands.CreateDisease;

public record CreateDiseaseCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
}

public class CreateDiseaseCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateDiseaseCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateDiseaseCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new Disease
        {
            Name = request.Name,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Diseases.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateDiseaseCommandValidator : AbstractValidator<CreateDiseaseCommand>
{
    public CreateDiseaseCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}
