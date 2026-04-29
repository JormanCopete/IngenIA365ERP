using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.WithdrawalReasons.Commands.DeleteWithdrawalReason;

public record DeleteWithdrawalReasonCommand(Guid PublicId) : IRequest<Result>;

public class DeleteWithdrawalReasonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteWithdrawalReasonCommand, Result>
{
    public async Task<Result> Handle(
        DeleteWithdrawalReasonCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithdrawalReasons
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
