using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.ScoringParameters.Commands.CreateScoringParameter;

public record CreateScoringParameterCommand : IRequest<Result<Guid>>
{
    public string CriterionCode { get; init; } = string.Empty;
    public string SubItemCode { get; init; } = string.Empty;
    public string CriterionName { get; init; } = string.Empty;
    public string SubItemName { get; init; } = string.Empty;
    public decimal Percentage { get; init; }
}

public class CreateScoringParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateScoringParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateScoringParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new ScoringParameter
        {
            CriterionCode = request.CriterionCode,
            SubItemCode = request.SubItemCode,
            CriterionName = request.CriterionName,
            SubItemName = request.SubItemName,
            Percentage = request.Percentage,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.ScoringParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateScoringParameterCommandValidator : AbstractValidator<CreateScoringParameterCommand>
{
    public CreateScoringParameterCommandValidator()
    {
        RuleFor(x => x.CriterionCode)
            .NotEmpty().WithMessage("Criterion code is required.")
            .MaximumLength(2).WithMessage("Criterion code must not exceed 2 characters.");

        RuleFor(x => x.SubItemCode)
            .NotEmpty().WithMessage("Sub-item code is required.")
            .MaximumLength(3).WithMessage("Sub-item code must not exceed 3 characters.");

        RuleFor(x => x.CriterionName)
            .NotEmpty().WithMessage("Criterion name is required.")
            .MaximumLength(120).WithMessage("Criterion name must not exceed 120 characters.");

        RuleFor(x => x.SubItemName)
            .MaximumLength(120).WithMessage("Sub-item name must not exceed 120 characters.");

        RuleFor(x => x.Percentage)
            .GreaterThanOrEqualTo(0).WithMessage("Percentage must be non-negative.");
    }
}
