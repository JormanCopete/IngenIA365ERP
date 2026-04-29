using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.ChartOfAccounts.Commands.DeleteChartOfAccount;

public record DeleteChartOfAccountCommand(Guid PublicId) : IRequest<Result>;

public class DeleteChartOfAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteChartOfAccountCommand, Result>
{
    public async Task<Result> Handle(
        DeleteChartOfAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ChartOfAccounts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.IsDeleted = true;
        entity.DeletedAt = dateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
