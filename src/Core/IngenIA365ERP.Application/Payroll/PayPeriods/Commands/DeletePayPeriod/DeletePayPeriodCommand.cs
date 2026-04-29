using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayPeriods.Commands.DeletePayPeriod;

public record DeletePayPeriodCommand(Guid PublicId) : IRequest<Result>;

public class DeletePayPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeletePayPeriodCommand, Result>
{
    public async Task<Result> Handle(
        DeletePayPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PayPeriods
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
