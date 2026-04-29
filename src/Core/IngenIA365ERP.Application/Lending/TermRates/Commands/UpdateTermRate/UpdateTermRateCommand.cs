using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.TermRates.Commands.UpdateTermRate;

public record UpdateTermRateCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
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

public class UpdateTermRateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateTermRateCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTermRateCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.TermRates
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.CreditLineId = request.CreditLineId;
        entity.AmountStart = request.AmountStart;
        entity.AmountEnd = request.AmountEnd;
        entity.TermStart = request.TermStart;
        entity.TermEnd = request.TermEnd;
        entity.SeniorityStart = request.SeniorityStart;
        entity.SeniorityEnd = request.SeniorityEnd;
        entity.Rate = request.Rate;
        entity.UpdateDate = request.UpdateDate;
        entity.MaxTerm = request.MaxTerm;
        entity.MaxAmount = request.MaxAmount;
        entity.GuaranteeType = request.GuaranteeType;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
