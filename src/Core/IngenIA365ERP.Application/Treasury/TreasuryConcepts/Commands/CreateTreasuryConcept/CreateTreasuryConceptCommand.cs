using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Treasury;
using MediatR;

namespace IngenIA365ERP.Application.Treasury.TreasuryConcepts.Commands.CreateTreasuryConcept;

public record CreateTreasuryConceptCommand : IRequest<Result<Guid>>
{
    public string ConceptCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? ConceptType { get; init; }
}

public class CreateTreasuryConceptCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateTreasuryConceptCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateTreasuryConceptCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new TreasuryConcept
        {
            ConceptCode = request.ConceptCode,
            Name = request.Name,
            ShortName = request.ShortName,
            ConceptType = request.ConceptType,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.TreasuryConcepts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateTreasuryConceptCommandValidator : AbstractValidator<CreateTreasuryConceptCommand>
{
    public CreateTreasuryConceptCommandValidator()
    {
        RuleFor(x => x.ConceptCode)
            .NotEmpty().WithMessage("Concept code is required.")
            .MaximumLength(5).WithMessage("Concept code must not exceed 5 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(60).WithMessage("Name must not exceed 60 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(20).WithMessage("Short name must not exceed 20 characters.");

        RuleFor(x => x.ConceptType)
            .MaximumLength(5).WithMessage("Concept type must not exceed 5 characters.");
    }
}
