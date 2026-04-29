using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.AccountingPeriods.Commands.DeleteAccountingPeriod;

public record DeleteAccountingPeriodCommand(Guid PublicId) : IRequest<Result>;

public class DeleteAccountingPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteAccountingPeriodCommand, Result>
{
    public async Task<Result> Handle(
        DeleteAccountingPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.AccountingPeriods
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
