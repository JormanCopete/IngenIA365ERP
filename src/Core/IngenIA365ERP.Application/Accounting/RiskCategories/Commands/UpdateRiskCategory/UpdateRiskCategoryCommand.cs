using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.RiskCategories.Commands.UpdateRiskCategory;

public record UpdateRiskCategoryCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string PeriodCode { get; init; } = string.Empty;
    public decimal InitialBalance { get; init; }
    public decimal AverageBalance { get; init; }
}

public class UpdateRiskCategoryCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateRiskCategoryCommand, Result>
{
    public async Task<Result> Handle(
        UpdateRiskCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.RiskCategories
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.AccountCode = request.AccountCode;
        entity.PeriodCode = request.PeriodCode;
        entity.InitialBalance = request.InitialBalance;
        entity.AverageBalance = request.AverageBalance;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
