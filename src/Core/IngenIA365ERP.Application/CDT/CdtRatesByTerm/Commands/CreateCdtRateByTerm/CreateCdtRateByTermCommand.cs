using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.CDT;
using MediatR;

namespace IngenIA365ERP.Application.CDT.CdtRatesByTerm.Commands.CreateCdtRateByTerm;

public record CreateCdtRateByTermCommand : IRequest<Result<Guid>>
{
    public int CreditLineId { get; init; }
    public decimal AmountRangeStart { get; init; }
    public decimal AmountRangeEnd { get; init; }
    public int TermStart { get; init; }
    public int TermEnd { get; init; }
    public decimal? InterestRate { get; init; }
}

public class CreateCdtRateByTermCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCdtRateByTermCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCdtRateByTermCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new CdtRateByTerm
        {
            CreditLineId = request.CreditLineId,
            AmountRangeStart = request.AmountRangeStart,
            AmountRangeEnd = request.AmountRangeEnd,
            TermStart = request.TermStart,
            TermEnd = request.TermEnd,
            InterestRate = request.InterestRate,
            LastUpdated = dateTime.UtcNow,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.CdtRatesByTerm.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateCdtRateByTermCommandValidator : AbstractValidator<CreateCdtRateByTermCommand>
{
    public CreateCdtRateByTermCommandValidator()
    {
        RuleFor(x => x.AmountRangeEnd)
            .GreaterThanOrEqualTo(x => x.AmountRangeStart)
            .WithMessage("Amount range end must be greater than or equal to amount range start.");

        RuleFor(x => x.TermEnd)
            .GreaterThanOrEqualTo(x => x.TermStart)
            .WithMessage("Term end must be greater than or equal to term start.");
    }
}
