using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.InterestRates.Commands.UpdateInterestRate;

public record UpdateInterestRateCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int Period { get; init; }
    public string CreditLineCode { get; init; } = string.Empty;
    public decimal PortfolioBalance { get; init; }
    public string CostCenterId { get; init; } = string.Empty;
    public int? PortfolioClass { get; init; }
}

public class UpdateInterestRateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateInterestRateCommand, Result>
{
    public async Task<Result> Handle(
        UpdateInterestRateCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.InterestRates
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Period = request.Period;
        entity.CreditLineCode = request.CreditLineCode;
        entity.PortfolioBalance = request.PortfolioBalance;
        entity.CostCenterId = request.CostCenterId;
        entity.PortfolioClass = request.PortfolioClass;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
