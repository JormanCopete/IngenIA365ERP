using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.CdtRatesByTerm.Commands.UpdateCdtRateByTerm;

public record UpdateCdtRateByTermCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int CreditLineId { get; init; }
    public decimal AmountRangeStart { get; init; }
    public decimal AmountRangeEnd { get; init; }
    public int TermStart { get; init; }
    public int TermEnd { get; init; }
    public decimal? InterestRate { get; init; }
}

public class UpdateCdtRateByTermCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateCdtRateByTermCommand, Result>
{
    public async Task<Result> Handle(
        UpdateCdtRateByTermCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.CdtRatesByTerm
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.CreditLineId = request.CreditLineId;
        entity.AmountRangeStart = request.AmountRangeStart;
        entity.AmountRangeEnd = request.AmountRangeEnd;
        entity.TermStart = request.TermStart;
        entity.TermEnd = request.TermEnd;
        entity.InterestRate = request.InterestRate;
        entity.LastUpdated = dateTime.UtcNow;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
