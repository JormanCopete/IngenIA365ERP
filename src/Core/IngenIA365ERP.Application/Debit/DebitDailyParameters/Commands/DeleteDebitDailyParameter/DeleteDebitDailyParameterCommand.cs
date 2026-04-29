using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.DebitDailyParameters.Commands.DeleteDebitDailyParameter;

public record DeleteDebitDailyParameterCommand(Guid PublicId) : IRequest<Result>;

public class DeleteDebitDailyParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteDebitDailyParameterCommand, Result>
{
    public async Task<Result> Handle(
        DeleteDebitDailyParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.DebitDailyParameters
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
