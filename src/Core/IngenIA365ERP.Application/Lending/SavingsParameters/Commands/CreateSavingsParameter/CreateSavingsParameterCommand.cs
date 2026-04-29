using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.SavingsParameters.Commands.CreateSavingsParameter;

public record CreateSavingsParameterCommand : IRequest<Result<Guid>>
{
    public int SavingsLineId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string InterestPaymentPeriod { get; init; } = string.Empty;
    public decimal MinInterestBalance { get; init; }
    public decimal MinTransactionAmount { get; init; }
    public decimal MinAccountBalance { get; init; }
    public decimal InterestPaymentRate { get; init; }
    public decimal MinWithholdingAmount { get; init; }
    public decimal WithholdingRate { get; init; }
    public int ClearingDays { get; init; }
    public decimal MaxWithdrawalAmount { get; init; }
    public decimal TaxRate { get; init; }
}

public class CreateSavingsParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateSavingsParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateSavingsParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new SavingsParameter
        {
            SavingsLineId = request.SavingsLineId,
            Name = request.Name,
            ShortName = request.ShortName ?? string.Empty,
            InterestPaymentPeriod = request.InterestPaymentPeriod,
            MinInterestBalance = request.MinInterestBalance,
            MinTransactionAmount = request.MinTransactionAmount,
            MinAccountBalance = request.MinAccountBalance,
            InterestPaymentRate = request.InterestPaymentRate,
            MinWithholdingAmount = request.MinWithholdingAmount,
            WithholdingRate = request.WithholdingRate,
            ClearingDays = request.ClearingDays,
            MaxWithdrawalAmount = request.MaxWithdrawalAmount,
            TaxRate = request.TaxRate,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.SavingsParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateSavingsParameterCommandValidator : AbstractValidator<CreateSavingsParameterCommand>
{
    public CreateSavingsParameterCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(50).WithMessage("Name must not exceed 50 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(25).WithMessage("Short name must not exceed 25 characters.");

        RuleFor(x => x.InterestPaymentPeriod)
            .MaximumLength(2).WithMessage("Interest payment period must not exceed 2 characters.");

        RuleFor(x => x.MaxWithdrawalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Max withdrawal amount must be non-negative.");
    }
}
