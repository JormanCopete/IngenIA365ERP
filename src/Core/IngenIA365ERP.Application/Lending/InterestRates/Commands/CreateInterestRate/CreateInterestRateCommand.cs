using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.InterestRates.Commands.CreateInterestRate;

public record CreateInterestRateCommand : IRequest<Result<Guid>>
{
    public int Period { get; init; }
    public string CreditLineCode { get; init; } = string.Empty;
    public decimal PortfolioBalance { get; init; }
    public string CostCenterId { get; init; } = string.Empty;
    public int? PortfolioClass { get; init; }
}

public class CreateInterestRateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateInterestRateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateInterestRateCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new InterestRate
        {
            Period = request.Period,
            CreditLineCode = request.CreditLineCode,
            PortfolioBalance = request.PortfolioBalance,
            CostCenterId = request.CostCenterId,
            PortfolioClass = request.PortfolioClass,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.InterestRates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateInterestRateCommandValidator : AbstractValidator<CreateInterestRateCommand>
{
    public CreateInterestRateCommandValidator()
    {
        RuleFor(x => x.Period)
            .GreaterThan(0).WithMessage("Period must be greater than 0.");

        RuleFor(x => x.CreditLineCode)
            .NotEmpty().WithMessage("Credit line code is required.")
            .MaximumLength(5).WithMessage("Credit line code must not exceed 5 characters.");

        RuleFor(x => x.CostCenterId)
            .MaximumLength(10).WithMessage("Cost center ID must not exceed 10 characters.");

        RuleFor(x => x.PortfolioBalance)
            .GreaterThanOrEqualTo(0).WithMessage("Portfolio balance must be non-negative.");
    }
}
