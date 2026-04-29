using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.TermRates.Commands.CreateTermRate;

public record CreateTermRateCommand : IRequest<Result<Guid>>
{
    public int CreditLineId { get; init; }
    public decimal AmountStart { get; init; }
    public decimal AmountEnd { get; init; }
    public int TermStart { get; init; }
    public int TermEnd { get; init; }
    public int SeniorityStart { get; init; }
    public int SeniorityEnd { get; init; }
    public decimal Rate { get; init; }
    public DateTime UpdateDate { get; init; }
    public int MaxTerm { get; init; }
    public decimal MaxAmount { get; init; }
    public string GuaranteeType { get; init; } = string.Empty;
}

public class CreateTermRateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateTermRateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateTermRateCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new TermRate
        {
            CreditLineId = request.CreditLineId,
            AmountStart = request.AmountStart,
            AmountEnd = request.AmountEnd,
            TermStart = request.TermStart,
            TermEnd = request.TermEnd,
            SeniorityStart = request.SeniorityStart,
            SeniorityEnd = request.SeniorityEnd,
            Rate = request.Rate,
            UpdateDate = request.UpdateDate,
            MaxTerm = request.MaxTerm,
            MaxAmount = request.MaxAmount,
            GuaranteeType = request.GuaranteeType,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.TermRates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateTermRateCommandValidator : AbstractValidator<CreateTermRateCommand>
{
    public CreateTermRateCommandValidator()
    {
        RuleFor(x => x.AmountEnd)
            .GreaterThanOrEqualTo(x => x.AmountStart)
            .WithMessage("AmountEnd must be greater than or equal to AmountStart.");

        RuleFor(x => x.TermEnd)
            .GreaterThanOrEqualTo(x => x.TermStart)
            .WithMessage("TermEnd must be greater than or equal to TermStart.");

        RuleFor(x => x.GuaranteeType)
            .MaximumLength(3).WithMessage("GuaranteeType must not exceed 3 characters.");
    }
}
