using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.RiskCategories.Commands.CreateRiskCategory;

public record CreateRiskCategoryCommand : IRequest<Result<Guid>>
{
    public string AccountCode { get; init; } = string.Empty;
    public string PeriodCode { get; init; } = string.Empty;
    public decimal InitialBalance { get; init; }
    public decimal AverageBalance { get; init; }
}

public class CreateRiskCategoryCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateRiskCategoryCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateRiskCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new RiskCategory
        {
            AccountCode = request.AccountCode,
            PeriodCode = request.PeriodCode,
            InitialBalance = request.InitialBalance,
            AverageBalance = request.AverageBalance,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.RiskCategories.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateRiskCategoryCommandValidator : AbstractValidator<CreateRiskCategoryCommand>
{
    public CreateRiskCategoryCommandValidator()
    {
        RuleFor(x => x.AccountCode)
            .NotEmpty().WithMessage("Account code is required.")
            .MaximumLength(20).WithMessage("Account code must not exceed 20 characters.");

        RuleFor(x => x.PeriodCode)
            .NotEmpty().WithMessage("Period code is required.")
            .MaximumLength(10).WithMessage("Period code must not exceed 10 characters.");
    }
}
