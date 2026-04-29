using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.WithdrawalStatuses.Commands.DeleteWithdrawalStatus;

public record DeleteWithdrawalStatusCommand(Guid PublicId) : IRequest<Result>;

public class DeleteWithdrawalStatusCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteWithdrawalStatusCommand, Result>
{
    public async Task<Result> Handle(
        DeleteWithdrawalStatusCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithdrawalStatuses
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
