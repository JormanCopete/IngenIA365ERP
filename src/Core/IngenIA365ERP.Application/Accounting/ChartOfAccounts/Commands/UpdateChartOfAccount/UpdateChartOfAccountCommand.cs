using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.ChartOfAccounts.Commands.UpdateChartOfAccount;

public record UpdateChartOfAccountCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public byte Level { get; init; }
    public string Nature { get; init; } = string.Empty;
    public decimal Rate { get; init; }
}

public class UpdateChartOfAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateChartOfAccountCommand, Result>
{
    public async Task<Result> Handle(
        UpdateChartOfAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ChartOfAccounts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.AccountCode = request.AccountCode;
        entity.Name = request.Name;
        entity.Level = request.Level;
        entity.Nature = request.Nature;
        entity.Rate = request.Rate;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
