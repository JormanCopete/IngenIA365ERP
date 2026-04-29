using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.SecondaryGroups.Commands.DeleteSecondaryGroup;

public record DeleteSecondaryGroupCommand(Guid PublicId) : IRequest<Result>;

public class DeleteSecondaryGroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteSecondaryGroupCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSecondaryGroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.SecondaryGroups
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
