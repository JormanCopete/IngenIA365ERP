using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PortfolioAccounts.Commands.UpdatePortfolioAccount;

public record UpdatePortfolioAccountCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string RecordClass { get; init; } = string.Empty;
    public int Category { get; init; }
    public int GuaranteeType { get; init; }
    public int DeductionClass { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
}

public class UpdatePortfolioAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePortfolioAccountCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePortfolioAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PortfolioAccounts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.AccountCode = request.AccountCode;
        entity.RecordClass = request.RecordClass;
        entity.Category = request.Category;
        entity.GuaranteeType = request.GuaranteeType;
        entity.DeductionClass = request.DeductionClass;
        entity.RiskLevel = request.RiskLevel;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
